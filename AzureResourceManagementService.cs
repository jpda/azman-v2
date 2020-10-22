using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using Azure.ResourceManager.Resources;

using azman_v2.Model;
using azman_v2.Auth;

namespace azman_v2
{
    public class AzureResourceManagementService : IResourceManagementService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AzureResourceManagementService> _log;
        private readonly ITokenProvider _tokenProvider;
        private readonly Azure.Core.TokenCredential _tokenCredential;
        public AzureResourceManagementService(IHttpClientFactory httpFactory, ILoggerFactory loggerFactory, ITokenProvider tokenProvider)
        {
            _httpClient = httpFactory.CreateClient();
            _log = loggerFactory.CreateLogger<AzureResourceManagementService>();
            _tokenProvider = tokenProvider;
            _tokenCredential = new ExternalAzureTokenCredential(_tokenProvider);
        }

        public TagSuiteModel? ProcessAlert(dynamic alert)
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
            return new TagSuiteModel(
                groupName: alert.data.context.activityLog.resourceGroupName,
                subscriptionId: alert.data.context.activityLog.subscriptionId,
                user: alert.data.context.activityLog.caller,
                managementDate: alert.data.context.activityLog.eventTimestamp
            );
        }

        public async Task AddTagSuite(TagSuiteModel request)
        {
            request.GenerateBaseTags();
            await AddTags(request);
        }

        public async Task AddTags(TagModel request)
        {
            var resourceManagerClient = new ResourcesManagementClient(request.SubscriptionId, _tokenCredential);
            var resourceGroupRequest = await resourceManagerClient.ResourceGroups.GetAsync(request.ResourceGroupName);
            if (resourceGroupRequest == null) return;
            var resourceGroup = resourceGroupRequest.Value;
            try
            {
                foreach (var t in request.Tags)
                {
                    resourceGroup.Tags.TryAdd(t.Key, t.Value);
                }
                await resourceManagerClient.ResourceGroups.CreateOrUpdateAsync(resourceGroup.Name, resourceGroup);
            }
            catch (Exception ex)
            {
                // todo: what happens when tagging fails? what's recoverable? what's not?
                _log.LogError(ex, ex.Message);
            }
        }

        public async Task AddTags(string resourceGroup, string subscriptionId, params KeyValuePair<string, string>[] tags)
        {
            await AddTags(new TagModel(resourceGroup, subscriptionId, tags));
        }

        // todo: explore changes required for using resource ID for _any_ resource
        public async Task DeleteResource(string subscriptionId, string resourceGroupName)
        {
            // todo: what-if? --> log deletion, but don't execute
            // connect to azure
            _log.LogInformation($"Request to delete {resourceGroupName} from subscription {subscriptionId}");
            var resourceManagerClient = new ResourcesManagementClient(subscriptionId, _tokenCredential);
            await resourceManagerClient.ResourceGroups.StartDeleteAsync(resourceGroupName);
        }

        public async Task<string> ExportResourceGroupTemplateByName(string subscriptionId, string groupName)
        {
            var token = await _tokenProvider.GetAccessTokenAsync(new[] { "https://management.azure.com/" });
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
            var body = new StringContent("{'resources':[ '*' ]}", System.Text.Encoding.UTF8, "application/json");

            var request = await _httpClient.PostAsync($"https://management.azure.com/subscriptions/{subscriptionId}/resourcegroups/{groupName}/exportTemplate?api-version=2020-06-01", body);
            if (!request.IsSuccessStatusCode) return string.Empty;

            // todo: handle 202 accepted on export request, sigh

            var templateData = await request.Content.ReadAsStringAsync();
            return templateData;


            // POST https://management.azure.com/subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}/exportTemplate?api-version=2020-06-01
            // todo: tweak based on output and ease of re-deploy
            // var resourceManagerClient = new ResourcesManagementClient(subscriptionId, _tokenCredential);
            // var exportedTemplate = await resourceManagerClient.ResourceGroups.StartExportTemplateAsync(groupName,
            // todo: this is required but read-only? hmm
            //new Azure.ResourceManager.Resources.Models.ExportTemplateRequest() { Resources = new[] { "*" } });
            // new Azure.ResourceManager.Resources.Models.ExportTemplateRequest() { });
            // if (exportedTemplate.HasValue) return (string)exportedTemplate.Value.Template; // todo: y tho?
            //return string.Empty;
        }
    }
}