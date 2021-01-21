
BirdCam Project
----

1. Images taken from a Raspberry Pi and are uploaded to an Azure IotHub. The code for this is in the BirdCamRaspberryPi project.
1. The IotHub then copies the images to Azure blob storage. 
1. The incoming images are then processed by the Azure Function Image Analyser via a blob trigger. The code is in the BirdCamFunction project. The image is moved to another container called processedimages and a sub-folder depending on whether it contains a bird, cat or other
1. The function calls Azure Cognitive Services with the image to identify what is in the picture.

The code is very much a proof of concept and I make no apologies for that!

Dev Setup
----

BirdCamRaspberryPi needs an appsettings.json with a single entry in json format: { "IoTHubConnectionString": "" }
BirdCamFunction needs two files:
	local.settings.json: { "IsEncrypted": false, "Values": { "AzureWebJobsStorage": "UseDevelopmentStorage=true", "FUNCTIONS_WORKER_RUNTIME": "dotnet" } }
	secret.settings.json: { "IoTStorageConnectionString": "", "ComputerVisionEndpoint": "", "ComputerVisionSubscriptionKey": "" }
I then use an SFTP client to deploy BirdCamRaspberryPi to the Pi. Instructions are here: https://docs.microsoft.com/en-us/dotnet/iot/deployment
Raspberry Pi Software:
- I use VNC to remote to the Pi and run the code though it is easy to set up to run on boot, enable this in integrations
- If you are deploying using Sftp, enable ssh in integrations
- Create a directory to deploy the BirdCamRaspberryPi code to
- Publish the code (contained in the linux-arm directory)
- Execute chmod a+x BirdCamRaspberryPi
- Execute BirdCamRaspberryPi to run
Raspberry Pi Hardware:
- The usual memory card and USB C power supply
- PIR motion sensor https://shop.pimoroni.com/products/pir-motion-sensor attached to, +ve Pin 1, -ve Pin 6, signal Pin 3
- A suitable case e.g. https://shop.pimoroni.com/products/securepi-case
- Female to Female Cables to attach the motion sensor with e.g. https://shop.pimoroni.com/products/jumper-jerky?variant=348491271
- Raspberry Pi camera https://shop.pimoroni.com/products/raspberry-pi-camera-module-v2-1-with-mount?variant=19833929735 attached to the camera input 

Azure Setup
----
This may be incomplete as I set this up manually. Create the following in Azure, put them all in the same data region if possible.
if you set this up using the free tiers then it should not cost you anything to run at low volumes.

- Resource Group
- Blob storage. StorageV2 with locally redundant storage is ok. Create containers images and processedimages. Grab connection string and put in secret.settings.json above
- Application Insights.
- Iot Hub and go to the following blades:
  - Shared access policy. For device and put in IoTHubConnectionString
  - File Upload and set storage account and container to images
  - IoT Devices. Add your Raspberry Pi
  - Networking. If you want additional security, Set Allow public network access to Selected IP Ranges and enter your IP address
- Function App. Grab publish profile for deploying BirdCamFunction to
- Cognitive Services. Free version. Grab keys and endpoint and put in secret.settings.json above


