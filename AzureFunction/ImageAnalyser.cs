using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using BirdCamFunction.Model;
using Microsoft.Extensions.Configuration;

namespace BirdCamFunction
{
    public static class ImageAnalyser
    {
        [FunctionName("Process")]
        public static async Task Process([BlobTrigger("images/{name}", Connection = "IoTStorage")] Stream myBlob, string name, ILogger log, ExecutionContext context)
        {
            log.LogInformation($"C# Blob trigger function Processed image\n Name:{name} \n Size: {myBlob.Length} Bytes");
            var configuration = new AppConfiguration();

            // Create a client
            ComputerVisionClient client = Authenticate(configuration.ComputerVisionEndpoint, configuration.ComputerVisionSubscriptionKey);

            // Creating a list that defines the features to be extracted from the image. 
            List<VisualFeatureTypes?> features = new List<VisualFeatureTypes?>
            {
                VisualFeatureTypes.Categories, VisualFeatureTypes.Description, VisualFeatureTypes.Faces, VisualFeatureTypes.ImageType, 
                VisualFeatureTypes.Tags, VisualFeatureTypes.Color, VisualFeatureTypes.Objects
            };
            
            ImageAnalysis results = await client.AnalyzeImageInStreamAsync(myBlob, features);
            // Sunmarizes the image content.
            log.LogInformation($"Summary of picture: {name}");
            foreach (var caption in results.Description.Captions)
            {
                log.LogInformation($"{caption.Text} with confidence {caption.Confidence * 100}%");
            }
            log.LogInformation("Tags:");
            foreach (var tag in results.Tags)
            {
                log.LogInformation($"{tag.Name} {tag.Confidence * 100}%");
            }
            log.LogInformation("Objects:");
            foreach (var obj in results.Objects)
            {
                log.LogInformation($"{obj.ObjectProperty} with confidence {obj.Confidence} at location {obj.Rectangle.X}, " +
                                    $"{obj.Rectangle.X + obj.Rectangle.W}, {obj.Rectangle.Y}, {obj.Rectangle.Y + obj.Rectangle.H}");
            }
            log.LogInformation("Color Scheme:");
            log.LogInformation("Is black and white?: " + results.Color.IsBWImg);
            log.LogInformation("Accent color: " + results.Color.AccentColor);
            log.LogInformation("Dominant background color: " + results.Color.DominantColorBackground);
            log.LogInformation("Dominant foreground color: " + results.Color.DominantColorForeground);
            log.LogInformation("Dominant colors: " + string.Join(",", results.Color.DominantColors));

        }
        public static ComputerVisionClient Authenticate(string endpoint, string key)
        {
            return new ComputerVisionClient(new ApiKeyServiceClientCredentials(key)) { Endpoint = endpoint };
        }
    }
}
