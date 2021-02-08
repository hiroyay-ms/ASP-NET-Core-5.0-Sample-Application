using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace FunctionApp
{
    public static class Functions
    {
        [FunctionName("RunOrchestrator")]
        public static async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var outputs = new List<string>();

            string sasUri = await context.CallActivityAsync<string>("CreateSasToken", context.GetInput<EventGridEvent>());

            outputs.Add(await context.CallActivityAsync<string>("SendMessage", sasUri));

            return outputs;
        }

        [FunctionName("CreateSasToken")]
        public static string CreateSasToken(
            [ActivityTrigger] EventGridEvent eventGridEvent,
            ILogger log)
        {
            var createdEvent = ((JObject)eventGridEvent.Data).ToObject<StorageBlobCreatedEventData>();
            var blobUrl = createdEvent.Url;

            string fileName = blobUrl.Substring(blobUrl.LastIndexOf("/") + 1);

            string connectionString = Environment.GetEnvironmentVariable("YellowtailConnectionString");
            string containerName = Environment.GetEnvironmentVariable("BlobContainerName");

            BlobServiceClient serviceClient = new BlobServiceClient(connectionString);
            BlobContainerClient containerClient = serviceClient.GetBlobContainerClient(containerName);

            BlobClient blobClient = containerClient.GetBlobClient(fileName);

            BlobSasBuilder sasBuilder = new BlobSasBuilder()
            {
                BlobContainerName = containerName,
                BlobName = blobClient.Name,
                Resource = "b"
            };

            sasBuilder.ExpiresOn = DateTimeOffset.Now.AddDays(7);
            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            Uri sasUri = blobClient.GenerateSasUri(sasBuilder);

            log.LogInformation($"FileName: {fileName} - sasUri: {sasUri}");

            return sasUri.ToString();
        }

        [FunctionName("Negotiate")]
        public static SignalRConnectionInfo GetSignalRInfo(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req,
            [SignalRConnectionInfo(HubName = "notifs")] SignalRConnectionInfo connectionInfo)
        {
            return connectionInfo;
        }

        [FunctionName("SendMessage")]
        public static Task SendMessage (
            [ActivityTrigger] string blobUri,
            [SignalR(HubName = "notifs")] IAsyncCollector<SignalRMessage> signalRMessage,
            ILogger log)
        {
            log.LogInformation("Activity Trigger function processed a request.");

            string message = "{\"uri\":\"" + blobUri + "\"}";

            return signalRMessage.AddAsync(
                new SignalRMessage
                {
                    Target = "newMessage",
                    Arguments = new[] { message }
                }
            );
        }

        [FunctionName("Orchestration_Start")]
        public static async Task Run(
            [EventGridTrigger] EventGridEvent eventGridEvent,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            log.LogInformation($"Data: {eventGridEvent.Data.ToString()}");

            var createdEventData = ((JObject)eventGridEvent.Data).ToObject<StorageBlobCreatedEventData>();
            
            string instanceId = await starter.StartNewAsync("RunOrchestrator", eventGridEvent);

            log.LogInformation($"Started orchestration with ID: {instanceId}");
        }
    }
}