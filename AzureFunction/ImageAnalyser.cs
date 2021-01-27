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
using System.Threading;
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

            // Create a computer vision client
            ComputerVisionClient client = Authenticate(configuration.ComputerVisionEndpoint, configuration.ComputerVisionSubscriptionKey);

            // Creating a list that defines the features to be extracted from the image. 
            List<VisualFeatureTypes?> features = new List<VisualFeatureTypes?>
            {
                VisualFeatureTypes.Categories, VisualFeatureTypes.Description, VisualFeatureTypes.Faces, VisualFeatureTypes.ImageType,
                VisualFeatureTypes.Tags, VisualFeatureTypes.Color, VisualFeatureTypes.Objects
            };

            ImageAnalysis imageAnalysis = await client.AnalyzeImageInStreamAsync(myBlob, features);

            // Summarises the image content
            var hasBird = imageAnalysis.Tags.Any(a => a.Name.Contains("bird", StringComparison.InvariantCultureIgnoreCase) && a.Confidence > 0.1) ||
                imageAnalysis.Objects.Any(a => a.ObjectProperty.Contains("bird", StringComparison.InvariantCultureIgnoreCase) && a.Confidence > 0.1);
            var hasCat = imageAnalysis.Tags.Any(a => a.Name.Contains("cat", StringComparison.InvariantCultureIgnoreCase) && a.Confidence > 0.1) ||
                         imageAnalysis.Objects.Any(a => a.ObjectProperty.Contains("cat", StringComparison.InvariantCultureIgnoreCase) && a.Confidence > 0.1);
            log.LogInformation($"Summary of picture: {imageName}");
            foreach (var caption in imageAnalysis.Description.Captions)
            {
                log.LogInformation($"{caption.Text} with confidence {caption.Confidence * 100}%");
            }
            log.LogInformation($"Has Bird: {hasBird}, Has Cat : {hasCat}");
            // Decide where to move the file according to its category
            string newFileName = GetNewFileName(hasBird, hasCat, imageName, configuration);
            if (hasCat)
            {
                await SendMessageToDevice(configuration, "get them!", log);
            }
            MoveBlob(configuration, imageName, newFileName, log);
            // Store analysis along with file
            var congnitiveServicesAnalysisFileName = Path.ChangeExtension(newFileName, "json");
            CreateCognitiveServicesAnalysisFile(configuration, imageAnalysis, congnitiveServicesAnalysisFileName, log);
            // Put in small delay to stop spamming cognitive services
            Thread.Sleep(200); 
        }
        private static string GetNewFileName(bool hasBird, bool hasCat, string imageName, AppConfiguration configuration)
        {
            string newFileName;
            if (hasBird)
            {
                newFileName = "birds/" + imageName;
            }
            else if (hasCat)
            {
                newFileName = "cats/" + imageName;
            }
            else
            {
                newFileName = "other/" + imageName;
            }
            if (!string.IsNullOrEmpty(configuration.DeviceId)) {
                newFileName = newFileName.Replace(configuration.DeviceId + "/", "");
            }
            return newFileName;
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
        public static void CreateCognitiveServicesAnalysisFile(AppConfiguration configuration, ImageAnalysis imageAnalysis, string fileName, ILogger log)
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
