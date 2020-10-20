using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using Azure.Identity;
using Azure.ResourceManager.Resources;

using Microsoft.Azure.Services.AppAuthentication;
using azman_v2.Model;
using Microsoft.Azure.Management.ResourceGraph;
using Microsoft.Azure.Management.ResourceGraph.Models;

namespace azman_v2
{
    public interface IResourceManagementService
    {
        TaggingRequestModel? ProcessAlert(dynamic alert);
        Task TagResource(TaggingRequestModel request);
        Task<string> ExportResourceGroupTemplateByName(string subscriptionId, string groupName);
    }
    public class ResourceManagementService : IResourceManagementService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ResourceManagementService> _log;
        public ResourceManagementService(IHttpClientFactory httpFactory, ILoggerFactory loggerFactory)
        {
            _httpClient = httpFactory.CreateClient();
            _log = loggerFactory.CreateLogger<ResourceManagementService>();
        }

        public TaggingRequestModel? ProcessAlert(dynamic alert)
        {
            if (!string.Equals(alert.data.context.activityLog.status.ToString(), "Succeeded", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(alert.data.context.activityLog.subStatus.ToString(), "Created", StringComparison.OrdinalIgnoreCase))
            {
                _log.LogTrace($"status: {alert.data.context.activityLog.status}");
                _log.LogTrace($"subStatus: {alert.data.context.activityLog.subStatus}");
                //return new OkResult(); // return 200, we're done here since it hasn't succeeded yet
                return null;
            }

            _log.LogTrace($"{alert.data.context.activityLog.resourceGroupName}");

            // handles queueing up new resource groups to be tagged
            return new TaggingRequestModel(
                groupName: alert.data.context.activityLog.resourceGroupName,
                user: alert.data.context.activityLog.caller,
                subscriptionId: alert.data.context.activityLog.subscriptionId,
                created: alert.data.context.activityLog.eventTimestamp
            );
        }

        public async Task TagResource(TaggingRequestModel request)
        {
            // connect to azure
            var resourceManagerClient = new ResourcesManagementClient(request.SubscriptionId, new DefaultAzureCredential());
            var resourceGroupRequest = await resourceManagerClient.ResourceGroups.GetAsync(request.ResourceGroupName);

            if (resourceGroupRequest == null) return;
            var resourceGroup = resourceGroupRequest.Value;

            // todo: move this as default to configuration
            var newDate = request.DateCreated.AddDays(30).Date;

            resourceGroup.Tags.TryAdd("expires", newDate.ToString("yyyy-MM-dd"));
            resourceGroup.Tags.TryAdd("tagged-by", "thrazman");
            resourceGroup.Tags.TryAdd("owner", request.CreatedByUser);

            // service-msft-prod-azman-main-compute
            var groupNamePieces = resourceGroup.Name.Split('-');
            if (groupNamePieces.Count() >= 3)
            {
                resourceGroup.Tags.TryAdd("function", groupNamePieces[0]); // service
                resourceGroup.Tags.TryAdd("customer", groupNamePieces[1]); // msft
                resourceGroup.Tags.TryAdd("env", groupNamePieces[2]); // prod
                resourceGroup.Tags.TryAdd("project", string.Join('-', groupNamePieces.Skip(3))); //azman-main-compute
            }
            await resourceManagerClient.ResourceGroups.CreateOrUpdateAsync(resourceGroup.Name, resourceGroup);
        }

        // todo: explore changes required for using resource ID for _any_ resource
        public async Task DeleteResource(string subscriptionId, string resourceGroupName)
        {
            // todo: what-if? --> log deletion, but don't execute
            // connect to azure
            _log.LogInformation($"Request to delete {resourceGroupName} from subscription {subscriptionId}");
            var resourceManagerClient = new ResourcesManagementClient(subscriptionId, new DefaultAzureCredential());
            await resourceManagerClient.ResourceGroups.StartDeleteAsync(resourceGroupName);
        }

        public async Task<string> ExportResourceGroupTemplateByName(string subscriptionId, string groupName)
        {
            // todo: tweak based on output and ease of re-deploy
            var resourceManagerClient = new ResourcesManagementClient(subscriptionId, new DefaultAzureCredential());
            var exportedTemplate = await resourceManagerClient.ResourceGroups.StartExportTemplateAsync(groupName,
                new Azure.ResourceManager.Resources.Models.ExportTemplateRequest());
            if (exportedTemplate.HasValue) return (string)exportedTemplate.Value.Template; // todo: y tho?
            return string.Empty;
        }
    }

    public interface IScanner
    {
        Task<IEnumerable<ResourceSearchResult>> ScanForUntaggedResources();
        Task<IEnumerable<ResourceSearchResult>> ScanForExpiredResources();
        Task<IEnumerable<ResourceSearchResult>> ScanForExpiredResources(DateTimeOffset expirationDate);
        Task<IEnumerable<ResourceSearchResult>> ScanForExpiredResources(string kustoDateExpression);
    }

    public class Scanner : IScanner
    {
        private readonly ITokenProvider _tokenProvider;
        private readonly HttpClient _httpClient;
        private readonly ILogger<Scanner> _log;

        // todo: configuration for national clouds, e.g., https://management.chinacloudapi.cn
        private readonly string _managementEndpoint = "https://management.azure.com/";
        private readonly string _managementAzureAdResourceId = "https://management.azure.com/";
        // todo: configuration to allow/deny specific subscriptions
        private readonly List<string> _subscriptionIds;

        public Scanner(ITokenProvider tokenProvider, IHttpClientFactory httpFactory, ILoggerFactory loggerFactory)
        {
            _tokenProvider = tokenProvider;
            _httpClient = httpFactory.CreateClient();
            _log = loggerFactory.CreateLogger<Scanner>();
            _subscriptionIds = new List<string>();
        }

        private async Task<IEnumerable<string>> FindAccessibleSubscriptions(bool forceRefresh = false)
        {
            // todo: hack for testing until configurable subscription list is available
            _subscriptionIds.Add("e7048bdb-835c-440f-9304-aa4171382839");
            if (!forceRefresh || _subscriptionIds.Any())
            {
                return _subscriptionIds;
            }

            _log.LogTrace($"Getting subscription list starting at ${DateTime.UtcNow}");
            _log.LogTrace($"Getting access token for ${_managementAzureAdResourceId}");

            var token = await _tokenProvider.GetAccessTokenAsync(_managementAzureAdResourceId, false);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // resource graph is expecting an array of subscriptions, so get the subscription list first
            var subRequest = await _httpClient.GetAsync($"{_managementEndpoint}subscriptions?api-version=2020-01-01");
            if (!subRequest.IsSuccessStatusCode)
            {
                _log.LogError(new EventId((int)subRequest.StatusCode, subRequest.StatusCode.ToString()), await subRequest.Content.ReadAsStringAsync());
                return _subscriptionIds;
            }

            var data = await JsonDocument.ParseAsync(await subRequest.Content.ReadAsStreamAsync());
            var subscriptionArray = data.RootElement.GetProperty("value").EnumerateArray();
            var subscriptions = subscriptionArray.Select(x => x.GetProperty("subscriptionId").ToString());

            _log.LogTrace($"Got subscription IDs: {string.Join(',', subscriptions)}");
            _subscriptionIds.AddRange(subscriptions);

            return _subscriptionIds;
        }

        private async Task<IEnumerable<ResourceSearchResult>> QueryResourceGraph(string queryText)
        {
            var subscriptions = await FindAccessibleSubscriptions();

            var graphClient = new ResourceGraphClient(new Microsoft.Rest.TokenCredentials(await _tokenProvider.GetAccessTokenAsync(_managementAzureAdResourceId)));
            var query = await graphClient.ResourcesAsync(new QueryRequest(subscriptions.ToList(), queryText));

            var resources = new List<ResourceSearchResult>();
            // the ResourceGraphClient uses Newtonsoft under the hood
            if (((dynamic)query.Data).rows is Newtonsoft.Json.Linq.JArray j)
            {
                resources.AddRange(
                    j.Select(x => new ResourceSearchResult()
                    {
                        // I'm sure there is a better way here - looking at the columns property, for example, 
                        // to find the position of the column in the row we're interested in - follows query order
                        // so for now, 0 & 1
                        ResourceId = x.ElementAt(0).ToString(),
                        SubscriptionId = x.ElementAt(1).ToString()
                    }));
            }

            return resources;
        }

        public async Task<IEnumerable<ResourceSearchResult>> ScanForUntaggedResources()
        {
            var untaggedQuery = @"resourcecontainers | where (isnull(tags.['expires'])) and 
                                                       type == 'microsoft.resources/subscriptions/resourcegroups'
                                                     | project name, subscriptionId, id";
            return await QueryResourceGraph(untaggedQuery);
        }

        public async Task<IEnumerable<ResourceSearchResult>> ScanForExpiredResources()
        {
            var expiredQuery = @"resourcecontainers | where (!isnull(tags.['expires'])) 
                                                      and type == 'microsoft.resources/subscriptions/resourcegroups'
                                                      and todatetime(tags['expires']) < now()
                                                    | project name, subscriptionId, id";
            return await QueryResourceGraph(expiredQuery);
        }

        public async Task<IEnumerable<ResourceSearchResult>> ScanForExpiredResources(DateTimeOffset expirationDate)
        {
            // and todatetime(tags.['expires']) < now() + 14d
            var expiredQuery = $@"resourcecontainers | where (!isnull(tags.['expires'])) 
                                                       and type == 'microsoft.resources/subscriptions/resourcegroups'
                                                       and todatetime(tags['expires']) < todatetime({expirationDate})
                                                     | project name, subscriptionId, id";
            return await QueryResourceGraph(expiredQuery);
        }

        public async Task<IEnumerable<ResourceSearchResult>> ScanForExpiredResources(string kustoDateExpression)
        {
            // e.g., and todatetime(tags.['expires']) < now() + 3d
            var expiredQuery = $@"resourcecontainers | where (!isnull(tags.['expires'])) 
                                                       and type == 'microsoft.resources/subscriptions/resourcegroups'
                                                       and todatetime(tags['expires']) < {kustoDateExpression}
                                                     | project name, subscriptionId, id";
            return await QueryResourceGraph(expiredQuery);
        }
    }

    public struct ResourceSearchResult
    {
        public string SubscriptionId { get; set; }
        public string ResourceId { get; set; }
        public ResourceSearchResult(string subscriptionId, string resourceId)
        {
            this.SubscriptionId = subscriptionId;
            this.ResourceId = resourceId;
        }
    }
}