""" module with requests to the Azure cognitive services """
import requests
import service_constants

def analyze_image(image_url: str):
    """ Analyse an image for visual features """

    # Analyze method
    analyze_endpoint_url = service_constants.VISION_SERVICE_URL + "analyze"

    headers = {
        # subscription key must accompany every call
        'Ocp-Apim-Subscription-Key': service_constants.OCP_APIM_SUBSCRIPTION_KEY,
        # when using an image URL, set this content-type
        'Content-Type': 'application/json'
    }

    #visualFeatures parameters we want identified in the image
    params = {'visualFeatures': 'Categories,Description,Color'}
    data = {'url': image_url}

    # make the POST request
    response = requests.post(analyze_endpoint_url, headers=headers, params=params, json=data)

    # if an error occurred
    response.raise_for_status()

    # json object from the body
    analysis = response.json()

    return analysis


def recognize_text_with_image_url(image_url: str):
    """ Recognize text using image bytes for data. Returns TextAnalysisResult object. """

    analyze_endpoint_url = service_constants.VISION_SERVICE_URL + "recognizeText"

    headers = {
        # subscription key must accompany every call
        'Ocp-Apim-Subscription-Key': service_constants.OCP_APIM_SUBSCRIPTION_KEY,
        # when using an image URL, set this content-type
        'Content-Type': 'application/json'
    }

    # if the text is handwritten, toggle this flag
    params = {'handwriting': 'false'}

    # the image url
    data = {'url': image_url}

    # make the POST request
    response = requests.post(analyze_endpoint_url, headers=headers, params=params, json=data)

    # if an error occurred
    response.raise_for_status()

    # json object from the body
    analysis = response.json()

    # This is the structure of the result dict
    # result["language"]
    # result["orientation"]
    # result["textAngle"]
    # result["regions"][0]["boundingBox"]
    # result["regions"][0]["lines"][0]["boundingBox"]
    # result["regions"][0]["lines"][0]["words"][0]["boundingBox"]
    # result["regions"][0]["lines"][0]["words"][0]["text"]

    return analysis


def recognize_text_from_image_bytes(image_bytes: str):
    """ Recognize text using image url for data. Returns TextAnalysisResult object. """
    analyze_endpoint_url = service_constants.VISION_SERVICE_URL + "recognizeText"

    headers = {
        # subscription key must accompany every call
        'Ocp-Apim-Subscription-Key': service_constants.OCP_APIM_SUBSCRIPTION_KEY,
        # when sending image bytes, set this content type
        'Content-Type': 'application/octet-stream'
    }

    # if the text is handwritten, toggle this flag
    params = {'handwriting': 'false'}

    # make the POST request
    response = requests.post(analyze_endpoint_url, headers=headers, params=params, data=image_bytes)

    # if an error occurred
    response.raise_for_status()

    # json object from the body
    analysis = response.json()

    # This is the structure of the result dict
    # result["language"]
    # result["orientation"]
    # result["textAngle"]
    # result["regions"][0]["boundingBox"]
    # result["regions"][0]["lines"][0]["boundingBox"]
    # result["regions"][0]["lines"][0]["words"][0]["boundingBox"]
    # result["regions"][0]["lines"][0]["words"][0]["text"]

    return analysis
