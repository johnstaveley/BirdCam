using Azure.Storage.Blobs;
using BirdCamFunction.Model;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Microsoft.Azure.Devices;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BirdCamFunction
{
    public static class ImageAnalyser
    {
        public static string ContainerName = "images";
        public static string ProcessedContainerName = "processedimages";

        [FunctionName("Process")]
        public static async Task Process([BlobTrigger("images/{imageName}", Connection = "IoTStorageConnectionString")] Stream myBlob, string imageName, ILogger log)
        {
            log.LogInformation($"C# Blob trigger function Processed image\n Name:{imageName} \n Size: {myBlob.Length} Bytes");
            var configuration = new AppConfiguration();

            // Create a client
            ComputerVisionClient client = Authenticate(configuration.ComputerVisionEndpoint, configuration.ComputerVisionSubscriptionKey);

            // Creating a list that defines the features to be extracted from the image. 
            List<VisualFeatureTypes?> features = new List<VisualFeatureTypes?>
            {
                VisualFeatureTypes.Categories, VisualFeatureTypes.Description, VisualFeatureTypes.Faces, VisualFeatureTypes.ImageType,
                VisualFeatureTypes.Tags, VisualFeatureTypes.Color, VisualFeatureTypes.Objects
            };

            await Task.Delay(200); // Put in delay to stop spamming cognitive services
            ImageAnalysis imageAnalysis = await client.AnalyzeImageInStreamAsync(myBlob, features);

            // Sunmarizes the image content.
            var hasBird = imageAnalysis.Tags.Any(a => a.Name.Contains("bird", StringComparison.InvariantCultureIgnoreCase) && a.Confidence > 0.1) ||
                imageAnalysis.Objects.Any(a => a.ObjectProperty.Contains("bird", StringComparison.InvariantCultureIgnoreCase) && a.Confidence > 0.1);
            var hasCat = imageAnalysis.Tags.Any(a => a.Name.Contains("cat", StringComparison.InvariantCultureIgnoreCase) && a.Confidence > 0.1) ||
                         imageAnalysis.Objects.Any(a => a.ObjectProperty.Contains("cat", StringComparison.InvariantCultureIgnoreCase) && a.Confidence > 0.1);
            log.LogInformation($"Summary of picture: {imageName}");
            foreach (var caption in imageAnalysis.Description.Captions)
            {
                log.LogInformation($"{caption.Text} with confidence {caption.Confidence * 100}%");
            }
            //log.LogInformation("Tags:");
            //foreach (var tag in imageAnalysis.Tags)
            //{
            //    log.LogInformation($"{tag.Name} {tag.Confidence * 100}%");
            //}
            //log.LogInformation("Objects:");
            //foreach (var obj in imageAnalysis.Objects)
            //{
            //    log.LogInformation($"{obj.ObjectProperty} with confidence {obj.Confidence} at location {obj.Rectangle.X}, " +
            //                        $"{obj.Rectangle.X + obj.Rectangle.W}, {obj.Rectangle.Y}, {obj.Rectangle.Y + obj.Rectangle.H}");
            //}
            //log.LogInformation("Color Scheme:");
            //log.LogInformation("Is black and white?: " + imageAnalysis.Color.IsBWImg);
            //log.LogInformation("Accent color: " + imageAnalysis.Color.AccentColor);
            //log.LogInformation("Dominant background color: " + imageAnalysis.Color.DominantColorBackground);
            //log.LogInformation("Dominant foreground color: " + imageAnalysis.Color.DominantColorForeground);
            //log.LogInformation("Dominant colors: " + string.Join(",", imageAnalysis.Color.DominantColors));
            log.LogInformation($"Has Bird: {hasBird}, Has Cat : {hasCat}");
            var newFileName = imageName;
            if (hasBird)
            {
                newFileName = "birds/" + imageName;
            }
            else if (hasCat)
            {
                newFileName = "cats/" + imageName;
                await SendMessageToDevice(configuration, "get them!", log);
            }
            else
            {
                newFileName = "other/" + imageName;
            }
            if (!string.IsNullOrEmpty(configuration.DeviceId)) {
                newFileName = newFileName.Replace(configuration.DeviceId + "/", "");
            }
            MoveBlob(configuration, imageName, newFileName, log);
            var diagnosticsFileName = Path.ChangeExtension(newFileName, "json");
            CreateDiagnosticsFile(configuration, imageAnalysis, diagnosticsFileName, log);

        }
        public static ComputerVisionClient Authenticate(string endpoint, string key)
        {
            return new ComputerVisionClient(new ApiKeyServiceClientCredentials(key)) { Endpoint = endpoint };
        }
        public static void MoveBlob(AppConfiguration configuration, string oldPath, string newPath, ILogger log)
        {
            BlobServiceClient blobClient = new BlobServiceClient(configuration.IoTStorageConnectionString);
            BlobContainerClient sourceContainer = blobClient.GetBlobContainerClient(ContainerName);
            if (!sourceContainer.GetBlobClient(oldPath).Exists())
            {
                log.LogWarning($"Unable to find blob {oldPath} in container {ContainerName}");
                return;
            }
            BlobContainerClient destinationContainer = blobClient.GetBlobContainerClient(ProcessedContainerName);
            if (destinationContainer.GetBlobClient(newPath).Exists())
            {
                log.LogWarning($"Found blob {newPath} in container {ProcessedContainerName} when it should not exist");
                return;
            }
            var blob = sourceContainer.GetBlobClient(oldPath);
            var newBlob = destinationContainer.GetBlobClient(newPath);
            var copyUri = (blobClient.Uri.AbsoluteUri + ContainerName).TrimEnd('/') + '/' + oldPath;
            newBlob.StartCopyFromUri(new Uri(copyUri));
            blob.Delete();
        }
        public static void CreateDiagnosticsFile(AppConfiguration configuration, ImageAnalysis imageAnalysis, string fileName, ILogger log)
        {
            BlobServiceClient blobClient = new BlobServiceClient(configuration.IoTStorageConnectionString);
            BlobContainerClient container = blobClient.GetBlobContainerClient(ProcessedContainerName);
            if (container.GetBlobClient(fileName).Exists())
            {
                log.LogWarning($"Found blob {fileName} in container {ProcessedContainerName} when it should not exist");
                return;
            }
            using (var memoryStream = new MemoryStream())
            {
                using (StreamWriter writer = new StreamWriter(memoryStream))
                {
                    using (JsonTextWriter jsonWriter = new JsonTextWriter(writer))
                    {
                        jsonWriter.Formatting = Formatting.Indented;
                        jsonWriter.Indentation = 3;
                        JsonSerializer ser = new JsonSerializer();
                        ser.Serialize(jsonWriter, imageAnalysis);
                        jsonWriter.Flush();
                        var blob = container.GetBlobClient(fileName);
                        memoryStream.Seek(0, SeekOrigin.Begin);
                        blob.Upload(memoryStream);
                    }
                }
            }
        }
        public static async Task SendMessageToDevice(AppConfiguration configuration, string message, ILogger log)
        {
            var directMethodName = "CatAlert";
            ServiceClient iothubServiceClient = ServiceClient.CreateFromConnectionString(configuration.IoTHubConnectionString);
            var methodRequest = new CloudToDeviceMethod(directMethodName, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
            try {
            var result = await iothubServiceClient.InvokeDeviceMethodAsync(configuration.DeviceId,  methodRequest);
            log.LogInformation($"Call to IoT Hub direct method {directMethodName} returned: {result.Status}");
            }
            catch (Exception exception)
            {
                // Device might not be online etc
                log.LogError(exception, $"Failed to send message to direct method {directMethodName}");
            }
        }
    }
}
