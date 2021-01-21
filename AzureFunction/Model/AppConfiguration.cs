using Microsoft.Extensions.Configuration;
using System;

namespace BirdCamFunction.Model
{
    public class AppConfiguration
    {
        /// <summary>
        /// Storage Container for images
        /// </summary>
        public string IoTStorageConnectionString { get; set; }
        public string ComputerVisionSubscriptionKey { get; set; }
        public string ComputerVisionEndpoint { get; set; }
        public AppConfiguration()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddJsonFile("local.settings.json", true, true)
                .AddJsonFile("secret.settings.json", true, true)
                .AddEnvironmentVariables()
                .Build();

            IoTStorageConnectionString = config["IoTStorageConnectionString"];
            ComputerVisionSubscriptionKey = config["ComputerVisionSubscriptionKey"];
            ComputerVisionEndpoint = config["ComputerVisionEndpoint"];
        }
    }
}
