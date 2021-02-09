using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Web1.Models;

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Web1.Controllers
{
    [Authorize]
    public class StorageController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration _configuration;

        public StorageController(ILogger<HomeController> logger, IConfiguration configuraiton)
        {
            _logger = logger;
            _configuration = configuraiton;
        }

        public IActionResult FileList()
        {
            BlobServiceClient serviceClient = new BlobServiceClient(_configuration.GetValue<string>("UserSettings:Yellowtail-ConnectionString"));
            BlobContainerClient containerClient = serviceClient.GetBlobContainerClient(_configuration.GetValue<string>("UserSettings:ContainerName"));

            List<FileContent> items = new List<FileContent>();
            foreach(var blobItem in containerClient.GetBlobs())
            {
                FileContent item = new FileContent()
                {
                    Icon = GetIcon(blobItem.Name),
                    Name = blobItem.Name,
                    ContentType = blobItem.Properties.ContentType,
                    ContentLength = GetContentLength(blobItem.Properties.ContentLength),
                    LastModified = blobItem.Properties.LastModified.Value.Date
                };

                items.Add(item);
            }

            ViewData["Blobs"] = items;

            return View();
        }

        private string GetIcon(string fileName)
        {
            string icon;
            string extension = fileName.Substring(fileName.LastIndexOf(".") + 1);
            switch (extension)
            {
                case "pptx":
                    icon = "powerpnt.png";
                    break;
                case "xlsx":
                    icon = "excel.png";
                    break;
                case "docs":
                    icon = "winword.png";
                    break;
                case "pdf":
                    icon = "pdf.png";
                    break;
                case "zip":
                    icon = "zip.png";
                    break;
                case "html":
                    icon = "html.png";
                    break;
                case "json":
                    icon = "json.png";
                    break;
                default:
                    icon = "unknown.png";
                    break;
            }

            return icon;
        }

        private string GetContentLength(long? length)
        {
            string contentLength = string.Empty;

            if (length > 1048576)
            {
                contentLength = $"{(length / 1024 / 1024)} MiB";
            }
            else if (length > 1024)
            {
                contentLength = $"{(length / 1024)} KiB";
            }
            else
            {
                contentLength = $"{length} B";
            }

            return contentLength;
        }

        [HttpPost]
        public IActionResult GetConfigurationValue(string paramName)
        {
            var parameterValue = _configuration.GetValue<string>(paramName);

            return Json(parameterValue);
        }

        [HttpPost]
        public IActionResult FileUpload()
        {
            IFormFile file = Request.Form.Files[0];
            var filePath = Path.GetTempFileName();
            var fileName = file.FileName.Substring(file.FileName.LastIndexOf("\\") + 1);

            using (var stream = new FileStream(filePath, FileMode.Create))
                file.CopyTo(stream);

            var memoryStream = new MemoryStream();
            using (var fileStream = new FileStream(filePath, FileMode.Open))
                fileStream.CopyTo(memoryStream);

            memoryStream.Position = 0;

            BlobServiceClient serviceClient = new BlobServiceClient(_configuration.GetValue<string>("UserSettings:Yellowtail-ConnectionString"));
            BlobContainerClient containerClient = serviceClient.GetBlobContainerClient(_configuration.GetValue<string>("UserSettings:ContainerName"));

            BlobClient blobClient = containerClient.GetBlobClient(fileName);
            blobClient.DeleteIfExists();

            BlobHttpHeaders httpHeaders = new BlobHttpHeaders();
            httpHeaders.ContentType = file.ContentType;

            blobClient.Upload(memoryStream, httpHeaders);

            FileContent item = new FileContent()
            {
                Icon = GetIcon(fileName),
                Name = fileName,
                ContentType = file.ContentType,
                ContentLength = GetContentLength(file.Length),
                LastModified = DateTime.Now.Date,
                Comment = $"{file.Length} bytes uploaded successfully!"
            };

            //Thread.Sleep(3000);

            var result = JsonConvert.SerializeObject(item);

            return Json(result);
        }
    }
}