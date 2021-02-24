using System;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FunctionApp
{
    public class Startup : FunctionsStartup
    {
        private IConfiguration configuration { get; }

        public override void ConfigureAppConfiguration(IFunctionsConfigurationBuilder builder)
        {
            builder.ConfigurationBuilder.AddAzureAppConfiguration(option =>
            {
                option.Connect(Environment.GetEnvironmentVariable("AppConfig"))
                    .ConfigureKeyVault(kv =>
                    {
                        kv.SetCredential(new DefaultAzureCredential());
                    });
            });
        }

        public override void Configure(IFunctionsHostBuilder builder)
        {
            var context = builder.GetContext();

            builder.Services.AddSingleton(x =>
                new BlobServiceClient(context.Configuration.GetValue<string>("UserSettings:Yellowtail-ConnectionString"))
            );
        }
    }
}