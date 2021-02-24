using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace FunctionApp
{
    public class Functions
    {
        private readonly IConfiguration _configuration;
        private readonly BlobServiceClient _serviceClient;

        public Functions(IConfiguration configuration, BlobServiceClient serviceClient)
        {
            _configuration = configuration;

            _serviceClient = serviceClient;
        }

        [FunctionName("RunOrchestrator")]
        public async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var outputs = new List<string>();

            string responseMessage = await context.CallActivityAsync<string>("GenerateSasToken", context.GetInput<EventGridEvent>());
            outputs.Add($"Created a SAS Token: {responseMessage}");

            outputs.Add(await context.CallActivityAsync<string>("SendEmail", responseMessage));

            string signalRMessage = await context.CallActivityAsync<string>("SendMessage", responseMessage);
            outputs.Add($"Sent to SignalR: {signalRMessage}");

            return outputs;
        }

        [FunctionName("GenerateSasToken")]
        public string GenerateSasToken(
            [ActivityTrigger] EventGridEvent eventGridEvent,
            ILogger log)
        {
            var createdEvent = ((JObject)eventGridEvent.Data).ToObject<StorageBlobCreatedEventData>();
            var blobUrl = createdEvent.Url;

            DateTimeOffset dto = DateTimeOffset.Now;

            string fileName = blobUrl.Substring(blobUrl.LastIndexOf("/") + 1);
            string containerName = _configuration.GetValue<string>("UserSettings:ContainerName");

            BlobContainerClient containerClient = _serviceClient.GetBlobContainerClient(containerName);

            BlobClient blobClient = containerClient.GetBlobClient(fileName);
            BlobProperties properties = blobClient.GetProperties();

            BlobSasBuilder sasBuilder = new BlobSasBuilder()
            {
                BlobContainerName = containerName,
                BlobName = blobClient.Name,
                Resource = "b"
            };

            sasBuilder.ExpiresOn = dto.AddDays(7);
            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            Uri sasUri = blobClient.GenerateSasUri(sasBuilder);

            log.LogInformation($"FileName: {fileName} - sasUri: {sasUri}");

            string[] words = sasUri.ToString().Split("?");

            var data = new ResponseMessage()
            {
                FileName = fileName,
                FileUrl = words[0],
                SasToken = words[1],
                SasUri = sasUri.ToString(),
                LastModifiedDate = dto.ToString("yyyy/MM/dd"),
                ExpiryDate = dto.AddDays(7).ToString("yyyy/MM/dd"),
                SendTo = properties.Metadata["UploadedBy"]
            };

            return JsonSerializer.Serialize(data);
        }

        [FunctionName("SendEmail")]
        public async Task<string> SendEmail(
            [ActivityTrigger] string message,
            ILogger log
        )
        {
            log.LogInformation("Activity Trigger function(SendEmail) processed a request.");

            var logicAppUrl = _configuration.GetValue<string>("UserSettings:LogicAppUrl");

            var httpClient = new HttpClient();
            var response = await httpClient.PostAsync(logicAppUrl, new StringContent(message, Encoding.UTF8, "application/json"));

            return $"Call logic apps -StatusCode: {response.StatusCode.ToString()}";
        }

        [FunctionName("negotiate")]
        public SignalRConnectionInfo GetSignalRInfo(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req,
            [SignalRConnectionInfo(HubName = "notifs")] SignalRConnectionInfo connectionInfo)
        {
            return connectionInfo;
        }

        [FunctionName("SendMessage")]
        public Task SendMessage (
            [ActivityTrigger] string message,
            [SignalR(HubName = "notifs")] IAsyncCollector<SignalRMessage> signalRMessage,
            ILogger log)
        {
            log.LogInformation("Activity Trigger function(SendMessage) processed a request.");

            return signalRMessage.AddAsync(
                new SignalRMessage
                {
                    Target = "newMessage",
                    Arguments = new[] { message }
                }
            );
        }

        [FunctionName("Orchestration_Start")]
        public async Task Run(
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