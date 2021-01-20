
BirdCam Project
---------------

Images taken from a Raspberry Pi and uploaded to an Azure IotHub. The code for this is in the BirdCamRaspberryPi project.
The IotHub then copies the images to Azure blob storage. 
The incoming images are then processed by the Azure Function Image Analyser via a blob trigger. 
	The code is in the BirdCamFunction project which is sent to Azure Cognitive Services to identify what is in the picture.

TODO: Azure Setup

