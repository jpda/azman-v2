using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using azman_v2.Model;
using Alexa.NET.Request;
using Alexa.NET.Request.Type;
using Alexa.NET;
using Alexa.NET.Response;

namespace azman_v2
{
    public class Functions
    {
        private readonly IScanner _scanner;
        private readonly IResourceManagementService _resourceManager;
        private readonly ILogger<Functions> _log;
        public Functions(IScanner scanner, IResourceManagementService manager, ILoggerFactory loggerFactory)
        {
            _scanner = scanner;
            _resourceManager = manager;
            _log = loggerFactory.CreateLogger<Functions>();
        }

        [FunctionName("OnResourceGroupCreate")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            [Queue("%ResourceGroupCreatedQueueName%", Connection = "MainStorageConnection")] IAsyncCollector<TagSuiteModel> outboundQueue)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            _log.LogTrace($"Received payload: {requestBody}");
            dynamic alert = JsonSerializer.Deserialize<dynamic>(requestBody);
            var tagRequest = _resourceManager.ProcessAlert(alert);
            if (tagRequest.HasValue)
            {
                await outboundQueue.AddAsync(tagRequest.Value);
            }
            // ready to do work
            return new OkObjectResult(new { });
        }

        [FunctionName("TagResourceGroup")]
        public async Task TagResourceGroup(
            [QueueTrigger("%ResourceGroupCreatedQueueName%", Connection = "MainStorageConnection")] TagSuiteModel request
        )
        {
            await _resourceManager.AddTagSuite(request);
        }

        [FunctionName("ScannerUntagged")]
        public async Task FindUntaggedResources([TimerTrigger("0 */5 * * * *")] TimerInfo timer,
            [Queue("%ResourceGroupCreatedQueueName%", Connection = "MainStorageConnection")] IAsyncCollector<TagSuiteModel> outboundQueue
        )
        {
            _log.LogTrace($"ScannerUntagged timer past due: {timer.IsPastDue}; next run: {timer.Schedule.GetNextOccurrence(DateTime.UtcNow)}");
            var untaggedResources = await _scanner.ScanForUntaggedResources();
            // output to tag queue -->
            var resourcesToTag = untaggedResources.Select(x => new TagSuiteModel(
                subscriptionId: x.SubscriptionId,
                groupName: x.ResourceId,
                managementDate: DateTime.UtcNow,
                user: "thrazman"
            ));
            await outboundQueue.AddRangeAsync(resourcesToTag);
        }

        [FunctionName("ScannerExpired")]
        public async Task FindExpired(
            [TimerTrigger("0 11 * * *")] TimerInfo timer,
            [Queue("%ResourceGroupExpiredQueueName%", Connection = "MainStorageConnection")] IAsyncCollector<ResourceSearchResult> outboundQueue
        )
        {
            _log.LogTrace($"ScannerExpired timer past due: {timer.IsPastDue}; next run: {timer.Schedule.GetNextOccurrence(DateTime.UtcNow)}");
            var resourcesPastExpirationDate = await _scanner.ScanForExpiredResources(DateTime.UtcNow);
            // output to expired queue -->
            var resourcesToTag = resourcesPastExpirationDate.Select(x => new ResourceSearchResult(
                subscriptionId: x.SubscriptionId,
                resourceId: x.ResourceId
            ));
            await outboundQueue.AddRangeAsync(resourcesToTag);

        }

        [FunctionName("ScannerUpcomingDeletion")]
        public async Task FindUpcoming(
            [TimerTrigger("%FindUpcomingSchedule%")] TimerInfo timer,
            [Queue("%ResourceGroupNotifyQueueName%", Connection = "MainStorageConnection")] IAsyncCollector<ResourceSearchResult> outboundQueue
        )
        {
            _log.LogTrace($"ScannerUpcomingDeletion timer past due: {timer.IsPastDue}; next run: {timer.Schedule.GetNextOccurrence(DateTime.UtcNow)}");
            var resourcesNearDeletion = await _scanner.ScanForExpiredResources(DateTime.UtcNow);
            // output to notification queue -->
            var resourcesToTag = resourcesNearDeletion.Select(x => new ResourceSearchResult(
               subscriptionId: x.SubscriptionId,
               resourceId: x.ResourceId
           ));
            await outboundQueue.AddRangeAsync(resourcesToTag);
        }

        // todo: best candidate for durable functions
        [FunctionName("ResourceGroupExpired")]
        public async Task ResourceGroupExpired(
            [QueueTrigger("%ResourceGroupExpiredQueueName%", Connection = "MainStorageConnection")] ResourceSearchResult request,
            [Queue("%ResourceGroupPersistQueueName%", Connection = "MainStorageConnection")] IAsyncCollector<ResourceSearchResult> persistQueue
        ) // at this point, the deletion is committed and will happen
        {
            // notify deletion --> this

            // persist resource group template to storage --> that
            await persistQueue.AddAsync(request);

            // queue up for deletion --> don't do this until this and that are done
        }

        [FunctionName("PersistResourceGroupToStorage")]
        public async Task PersistResourceGroupToStorage(
            [QueueTrigger("%ResourceGroupPersistQueueName%", Connection = "MainStorageConnection")] ResourceSearchResult request,
            Binder binder
        )
        {
            _log.LogTrace($"Exporting template for {request.ResourceId} in {request.SubscriptionId}");
            // get template from ARM
            var previouslyExported = await _resourceManager.GetTagValue(request.SubscriptionId, request.ResourceId, "exported", x => bool.Parse(x), () => false);
            _log.LogTrace($"Template for {request.ResourceId} in {request.SubscriptionId} has been exported previously: {previouslyExported}");
            if (previouslyExported) return;

            var templateData = await _resourceManager.
                        ExportResourceGroupTemplateByName(request.SubscriptionId, request.ResourceId);
            if (string.IsNullOrWhiteSpace(templateData)) return;

            var exportFilename = $"thrazman-export/{DateTime.UtcNow:yyyy-MM-dd}/{request.ResourceId}-{Guid.NewGuid().ToString().Substring(0, 8)}.json";
            _log.LogTrace($"Got template data, writing to {exportFilename}");
            // connect up to blob storage
            var attributes = new Attribute[]
            {
                new BlobAttribute(exportFilename),
                new StorageAccountAttribute("MainStorageConnection")
            };

            using var writer = await binder.BindAsync<TextWriter>(attributes).ConfigureAwait(false);
            writer.Write(templateData);
            await _resourceManager.AddTags(
                request.ResourceId, request.SubscriptionId,
                new KeyValuePair<string, string>("exported", "true")
            );
        }

        [FunctionName("AlexaEndpoint")]
        public async Task<IActionResult> AlexaEndpoint(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req)
        {
            _log.LogTrace($"Recieved alexa request");
            string json = await req.ReadAsStringAsync();
            _log.LogTrace($"request: {json}");
            var input = Newtonsoft.Json.JsonConvert.DeserializeObject<SkillRequest>(json);
            //var input = JsonSerializer.Deserialize<SkillRequest>(json);
            var requestType = input.GetRequestType();

            if (requestType == typeof(LaunchRequest))
            {
                return new OkObjectResult(ResponseBuilder.Ask("THE THRAZ MAN COMES.", null));
            }

            var defaultResponse = ResponseBuilder.Ask("Do not anger thraz man with unclear instructions.", null);

            if (!(input.Request is IntentRequest intentRequest)) return new OkObjectResult(defaultResponse);

            switch (intentRequest.Intent.Name)
            {
                case "check_expiring":
                    {
                        var expiring = await _scanner.ScanForExpiredResources("now() + 3d");
                        var expiringNames = expiring.Select(x => x.ResourceId);
                        var response = ResponseBuilder.Tell($"You have {expiring.Count()} resources being nuked into orbit in the next three days: {string.Join(',', expiringNames)}. Why aren't these running in AWS?");
                        return new OkObjectResult(response);
                    }
                case "whats_running":
                    {
                        
                        return new OkObjectResult(ResponseBuilder.Tell("Thraz man knows when thraz man knows. Trust the process"));
                    }
                case "delete_resource":
                    {
                        return new OkObjectResult(ResponseBuilder.Tell("Deleting your entire Azure subscription...J.K. Thraz man isn't ready to delete stuff yet.", null));
                    }
                default:
                    {
                        return new OkObjectResult(defaultResponse);
                    }
            }

            //return new OkObjectResult(defaultResponse);
        }
    }
}