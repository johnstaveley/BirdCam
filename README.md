
BirdCam Project
----

1. Images taken from a Raspberry Pi and are uploaded to an Azure IotHub. The code for this is in the BirdCamRaspberryPi project.
1. The IotHub then copies the images to Azure blob storage. 
1. The incoming images are then processed by the Azure Function Image Analyser via a blob trigger. The code is in the BirdCamFunction project
1. The function calls Azure Cognitive Services with the image to identify what is in the picture.

The code is very much a proof of concept and I make no apologies for that!

Dev Setup
----

BirdCamRaspberryPi needs an appsettings.json with a single entry injson format: { "IoTHubConnectionString": "" }
BirdCamFunction needs two files:
	local.settings.json: { "IsEncrypted": false, "Values": { "AzureWebJobsStorage": "UseDevelopmentStorage=true", "FUNCTIONS_WORKER_RUNTIME": "dotnet" } }
	secret.settings.json: { "IoTStorage": "", "ComputerVisionEndpoint": "", "ComputerVisionSubscriptionKey": "" }
I then use an SFTP client to deploy BirdCamRaspberryPi to the Pi. Instructions are here: https://docs.microsoft.com/en-us/dotnet/iot/deployment
On the Raspberry Pi:
- I use VNC to remote to the Pi and run the code though it is easy to set up to run on boot, enable this in integrations
- If you are deploying using Sftp, enable ssh in integrations
- Create a directory to deploy the BirdCamRaspberryPi code to
- Publish the code (contained in the linux-arm directory)
- Execute chmod a+x BirdCamRaspberryPi
- Execute BirdCamRaspberryPi to run

Azure Setup
----
This may be incomplete as I set this up manually. Create the following in Azure, put them all in the same data region if possible.
if you set this up using the free tiers then it should not cost you anything to run at low volumes.

- Resource Group
- Blob storage. StorageV2 with locally redundant storage is ok. Create a container images. Grab connection string and put in secret.settings.json above
- Application Insights.
- Iot Hub and go to the following blades:
  - Shared access policy. For device and put in IoTHubConnectionString
  - File Upload and set storage account and container to images
  - IoT Devices. Add your Raspberry Pi
  - Networking. If you want additional security, Set Allow public network access to Selected IP Ranges and enter your IP address
- Function App. Grab publish profile for deploying BirdCamFunction to
- Cognitive Services. Free version. Grab keys and endpoint and put in secret.settings.json above


