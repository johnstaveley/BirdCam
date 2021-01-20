using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Device.Gpio;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BirdCamRaspberryPi.Model;
using Unosquare.RaspberryIO;

namespace BirdCamRaspberryPi
{

    class Program
    {
        static DeviceClient _deviceClient;

        static async Task Main(string[] args)
        {
            var today = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            Console.WriteLine($"Motion Sensor starting @ {today}");
            var configuration = new AppConfiguration();
            _deviceClient = DeviceClient.CreateFromConnectionString(configuration.IoTHubConnectionString);
            var gpioController = new GpioController(PinNumberingScheme.Board);
            //await _deviceClient.SetReceiveMessageHandlerAsync(OnReceiveMessage, null);
            //await _deviceClient.SetMethodHandlerAsync("doStuff", DoStuff, null);
            var motionSensorPin = 3;
            gpioController.OpenPin(motionSensorPin, PinMode.InputPullDown);
            int captureNumber = 0;
            try
            {
                while (true)
                {
                    if (gpioController.Read(motionSensorPin) == true)
                    {
                        captureNumber++;
                        var tag = $"{today}-{captureNumber}";
                        await CaptureImage(tag);
                        //await SendMessage(tag);
                        await Task.Delay(4000);
                    }
                    await Task.Delay(1000);
                }
            }
            finally
            {
                gpioController.ClosePin(motionSensorPin);
            }

        }

        /// <summary>
        /// Takes an image and either saves it locally or uploads it to Azure IoT Hub
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="saveLocal">Default=False</param>
        /// <returns></returns>
        static async Task CaptureImage(string tag, bool saveLocal = false)
        {
            var pictureBytes = await Pi.Camera.CaptureImageJpegAsync(1280, 960);
            var fileName = $"capture-{tag}.jpg";
            if (saveLocal) {
                var targetPath = $"/home/pi/Pictures/{fileName}";
                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }
                await File.WriteAllBytesAsync(targetPath, pictureBytes);
            }
            await UploadImage(pictureBytes, fileName);
            Console.WriteLine($"Took picture {fileName} with size: {pictureBytes.Length} bytes");
        }

        static async Task UploadImage(byte[] pictureBytes, string fileName)
        {
            var fileUploadSasUriRequest = new FileUploadSasUriRequest
            {
                BlobName = fileName
            };
            FileUploadSasUriResponse sasUri = await _deviceClient.GetFileUploadSasUriAsync(fileUploadSasUriRequest);
            // Pass URL encoded device name and blob name to support special characters
            Uri uploadUri = new Uri($"https://{sasUri.HostName}/{sasUri.ContainerName}/{Uri.EscapeDataString(sasUri.BlobName)}{sasUri.SasToken}");
            //Console.WriteLine($"Successfully got SAS URI ({uploadUri}) from IoT Hub");
            MemoryStream memStream = new MemoryStream();
            memStream.Write(pictureBytes, 0, pictureBytes.Length);
            memStream.Seek(0, SeekOrigin.Begin);
            try
            {
                //Console.WriteLine($"Uploading file {fileName} using the Azure Storage SDK and a SAS URI for authentication");
                // Note that other versions of the Azure Storage SDK can be used here. For the latest version, see
                // https://github.com/Azure/azure-sdk-for-net/tree/master/sdk/storage#azure-storage-libraries-for-net
                var blob = new CloudBlockBlob(uploadUri);
                await blob.UploadFromStreamAsync(memStream);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to upload file to Azure Storage using the Azure Storage SDK due to {ex}");

                var failedFileUploadCompletionNotification = new FileUploadCompletionNotification
                {
                    // Mandatory. Must be the same value as the correlation id returned in the sas uri response
                    CorrelationId = sasUri.CorrelationId,
                    // Mandatory. Will be present when service client receives this file upload notification
                    IsSuccess = false,
                    // Optional, user-defined status code. Will be present when service client receives this file upload notification
                    StatusCode = 500,
                    // Optional, user defined status description. Will be present when service client receives this file upload notification
                    StatusDescription = ex.Message
                };

                // Note that this is done even when the file upload fails. IoT Hub has a fixed number of SAS URIs allowed active
                // at any given time. Once you are done with the file upload, you should free your SAS URI so that other
                // SAS URIs can be generated. If a SAS URI is not freed through this API, then it will free itself eventually
                // based on how long SAS URIs are configured to live on your IoT Hub.
                await _deviceClient.CompleteFileUploadAsync(failedFileUploadCompletionNotification);
                //Console.WriteLine("Notified IoT Hub that the file upload failed and that the SAS URI can be freed");
                return;
            }

            Console.WriteLine("Successfully uploaded the file to Azure Storage");

            var successfulFileUploadCompletionNotification = new FileUploadCompletionNotification
            {
                // Mandatory. Must be the same value as the correlation id returned in the sas uri response
                CorrelationId = sasUri.CorrelationId,
                // Mandatory. Will be present when service client receives this file upload notification
                IsSuccess = true,
                // Optional, user defined status code. Will be present when service client receives this file upload notification
                StatusCode = 200,
                // Optional, user-defined status description. Will be present when service client receives this file upload notification
                StatusDescription = "Success"
            };
            await _deviceClient.CompleteFileUploadAsync(successfulFileUploadCompletionNotification);
            //Console.WriteLine("Notified IoT Hub that the file upload succeeded and that the SAS URI can be freed.");
        }

        static async Task SendMessage(string tag)
        {
            string messageBody = JsonSerializer.Serialize(
                        new
                        {
                            message = $"Received Capture {tag}"
                        });
            using var message = new Message(Encoding.ASCII.GetBytes(messageBody))
            {
                ContentType = "application/json",
                ContentEncoding = "utf-8",
            };

            // Send the telemetry message
            await _deviceClient.SendEventAsync(message);
        }

        private static Task<MethodResponse> DoStuff(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine("IoT Hub invoked the 'doStuff' method.");
            Console.WriteLine("Payload:");
            Console.WriteLine(methodRequest.DataAsJson);
            var responseMessage = "{\"response\": \"OK\"}";
            return Task.FromResult(new MethodResponse(Encoding.ASCII.GetBytes(responseMessage), 200));
        }

        private static async Task OnReceiveMessage(Message message, object userContext)
        {
            var reader = new StreamReader(message.BodyStream);
            var messageContents = await reader.ReadToEndAsync();
            Console.WriteLine($"Message Contents: {messageContents}");
            Console.WriteLine("Message Properties:");
            foreach (var property in message.Properties)
            {
                Console.WriteLine($"Key: {property.Key}, Value: {property.Value}");
            }
            await _deviceClient.CompleteAsync(message);
        }
    }
}
