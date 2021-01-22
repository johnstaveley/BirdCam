using Microsoft.Extensions.Configuration;
using System;

namespace BirdCamFunction.Model
{
    public class AppConfiguration
    {
        /// <summary>
        /// As registered in the IoT Hub -> IoT Devices blade e.g. RaspberryPi4
        /// </summary>
        public string DeviceId { get; set; }
        /// <summary>
        /// This is taken from the device blade when it is registered on the IoT Hub
        /// </summary>
        public string IoTHubConnectionString { get; set; }
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

            ComputerVisionEndpoint = config["ComputerVisionEndpoint"];
            ComputerVisionSubscriptionKey = config["ComputerVisionSubscriptionKey"];
            DeviceId = config["DeviceId"];
            IoTHubConnectionString = config["IoTHubConnectionString"];
            IoTStorageConnectionString = config["IoTStorageConnectionString"];
        }
    }
}
