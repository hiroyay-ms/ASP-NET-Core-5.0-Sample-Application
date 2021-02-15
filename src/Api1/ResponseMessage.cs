using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FunctionApp
{
    public class ResponseMessage
    {
        [JsonPropertyName("fileName")]
        public string FileName { get; set; }

        [JsonPropertyName("fileUrl")]
        public string FileUrl { get; set; }

        [JsonPropertyName("sasToken")]
        public string SasToken { get; set; }

        [JsonPropertyName("sasUri")]
        public string SasUri { get; set; }

        [JsonPropertyName("lastModifiedDate")]
        public string LastModifiedDate { get; set; }

        [JsonPropertyName("expiryDate")]
        public string ExpiryDate { get; set; }

        [JsonPropertyName("sendTo")]
        public string SendTo { get; set; }        
    }
}