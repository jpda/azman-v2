using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using Azure.Identity;
using Azure.ResourceManager.Resources;

using Microsoft.Azure.Services.AppAuthentication;
using azman_v2.Model;

namespace azman_v2
{
    public class AzureResourceManagementService : IResourceManagementService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AzureResourceManagementService> _log;
        public AzureResourceManagementService(IHttpClientFactory httpFactory, ILoggerFactory loggerFactory)
        {
            _httpClient = httpFactory.CreateClient();
            _log = loggerFactory.CreateLogger<AzureResourceManagementService>();
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
}