using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Management.ResourceGraph;
using Microsoft.Azure.Management.ResourceGraph.Models;
using azman_v2.Auth;

namespace azman_v2
{
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

            var token = await _tokenProvider.GetAccessTokenAsync(new[] { _managementAzureAdResourceId }, false);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

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
            var token = await _tokenProvider.GetAccessTokenAsync(new[] { _managementAzureAdResourceId });
            var graphClient = new ResourceGraphClient(new Microsoft.Rest.TokenCredentials(token.Token));
            var query = await graphClient.ResourcesAsync(new QueryRequest(subscriptions.ToList(), queryText));

            var resources = new List<ResourceSearchResult>();
            // the ResourceGraphClient uses Newtonsoft under the hood
            if (((dynamic)query.Data).rows is Newtonsoft.Json.Linq.JArray j)
            {
                resources.AddRange(
                    j.Select(x => new ResourceSearchResult(
                        resourceId: x.ElementAt(0).ToString(),
                        subscriptionId: x.ElementAt(1).ToString())));
            }

            return resources;
        }

        private async Task<IEnumerable<T>> QueryResourceGraph<T>(string queryText) where T : SearchResult, new()
        {
            var subscriptions = await FindAccessibleSubscriptions();
            var token = await _tokenProvider.GetAccessTokenAsync(new[] { _managementAzureAdResourceId });
            var graphClient = new ResourceGraphClient(new Microsoft.Rest.TokenCredentials(token.Token));
            var query = await graphClient.ResourcesAsync(new QueryRequest(subscriptions.ToList(), queryText));

            var resources = new List<T>();
            // the ResourceGraphClient uses Newtonsoft under the hood
            if (((dynamic)query.Data).rows is Newtonsoft.Json.Linq.JArray j)
            {
                var result = new T();
                resources.AddRange(
                    j.Select(x => new T()
                    {
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
            var expiredQuery = @"resourcecontainers | where (isnotnull(tags.['expires'])) 
                                                      and type == 'microsoft.resources/subscriptions/resourcegroups'
                                                      and todatetime(tags['expires']) < now()
                                                    | project name, subscriptionId, id";
            return await QueryResourceGraph(expiredQuery);
        }

        public async Task<IEnumerable<ResourceSearchResult>> ScanForExpiredResources(DateTime expirationDate)
        {
            // and todatetime(tags.['expires']) < now() + 14d
            var expiredQuery = $@"resourcecontainers | where (isnotnull(tags.['expires'])) 
                                                       and type == 'microsoft.resources/subscriptions/resourcegroups'
                                                       and todatetime(tags['expires']) < todatetime('{expirationDate:o}')
                                                     | project name, subscriptionId, id";
            return await QueryResourceGraph(expiredQuery);
        }

        public async Task<IEnumerable<ResourceSearchResult>> ScanForExpiredResources(string kustoDateExpression)
        {
            // e.g., and todatetime(tags.['expires']) < now() + 3d
            var expiredQuery = $@"resourcecontainers | where (isnotnull(tags.['expires'])) 
                                                       and type == 'microsoft.resources/subscriptions/resourcegroups'
                                                       and todatetime(tags['expires']) < {kustoDateExpression}
                                                     | project name, subscriptionId, id";
            return await QueryResourceGraph(expiredQuery);
        }

        public async Task<IEnumerable<ResourceSearchResult>> FindSpecificResources(string expression)
        { // https://docs.microsoft.com/en-us/azure/virtual-machines/states-lifecycle
            var runningVmsQuery = $@"resources | where type == 'microsoft.compute/virtualmachines'
                                    | project id, subscriptionId, resourceGroup, name, 
                                    properties.extended.instanceView.powerState.displayStatus";
            // todo: update the return types with more arbitrary data
            return await QueryResourceGraph(runningVmsQuery);
        }
    }
}