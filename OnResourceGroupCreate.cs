using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace azman_v2
{
    public static class OnResourceGroupCreate
    {
        [FunctionName("OnResourceGroupCreate")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,

            [Queue("%ResourceGroupCreatedQueueName%", Connection = "MainQueueConnection")] IAsyncCollector<TaggingRequestModel> outboundQueue,
            ILogger log)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            log.LogTrace($"Received payload: {requestBody}");

            dynamic alert = JsonConvert.DeserializeObject(requestBody);

            if (!string.Equals(alert.data.context.activityLog.status.ToString(), "Succeeded", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(alert.data.context.activityLog.subStatus.ToString(), "Created", StringComparison.OrdinalIgnoreCase))
            {
                log.LogTrace($"status: {alert.data.context.activityLog.status}");
                log.LogTrace($"subStatus: {alert.data.context.activityLog.subStatus}");
                return new OkResult(); // return 200, we're done here since it hasn't succeeded yet
            }

            log.LogTrace($"{alert.data.context.activityLog.resourceGroupName}");

            // handles queueing up new resource groups to be tagged
            await outboundQueue.AddAsync(new TaggingRequestModel()
            {
                ResourceGroupName = alert.data.context.activityLog.resourceGroupName,
                CreatedByUser = alert.data.context.activityLog.caller,
                SubscriptionId = alert.data.context.activityLog.subscriptionId,
                DateCreated = alert.data.context.activityLog.eventTimestamp
            });

            // ready to do work
            return new OkObjectResult(new { });
        }
    }
}