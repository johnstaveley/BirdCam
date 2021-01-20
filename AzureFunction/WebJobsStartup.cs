using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

[assembly: WebJobsStartup(typeof(BirdCamFunction.Startup))]
namespace BirdCamFunction
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddJsonFile("local.settings.json", true, true)
                .AddJsonFile("secret.settings.json", false, true)
                .AddEnvironmentVariables()
                .Build();
                
            builder.Services.AddSingleton<IConfiguration>(config);
            
            if (string.IsNullOrEmpty(config["ComputerVisionSubscriptionKey"])) throw new ArgumentException("ComputerVisionSubscriptionKey not valid");
        }

    }
}