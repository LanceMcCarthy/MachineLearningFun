""" Constants for Azure Cognitive Services """
# API Key for your account, https://azure.microsoft.com/en-us/services/cognitive-services/directory/vision/
OCP_APIM_SUBSCRIPTION_KEY = "YOUR_VSION_API_SERVICE_KEY"

# IMPORTANT!!! Make sure the region is the same as your API key is assigned for
AZURE_REGION = "eastus2"

# URL for all cognitive services.
COGNITIVE_SERVICES_URL = "https://" + AZURE_REGION + ".api.cognitive.microsoft.com/"

# Url for Vision API
VISION_SERVICE_URL = COGNITIVE_SERVICES_URL + "vision/v1.0/"
