import easygui
import json
from io import BytesIO
import matplotlib.pyplot as plt
import requests
from PIL import Image

# API Key for your account, https://azure.microsoft.com/en-us/services/cognitive-services/directory/vision/
OCP_APIM_SUBSCRIPTION_KEY = "YOUR_VISION_API_KEY" 

# IMPORTANT Make sure the region is the same as your API key is assigned for
AZURE_REGION = "eastus2" 
COGNITIVE_SERVICES_URL = "https://" + AZURE_REGION + ".api.cognitive.microsoft.com/"

# Vision API v1 endpoint
VISION_SERVICE_URL = COGNITIVE_SERVICES_URL + "vision/v1.0/"

def render_bounding_box(comma_delimited_rect: str):
    box_coordinates = comma_delimited_rect.strip().split(',')
    x = int(box_coordinates[0].strip())
    y = int(box_coordinates[1].strip())
    width = int(box_coordinates[2].strip())
    height = int(box_coordinates[3].strip())
    bottom_left = [x, y]
    bottom_right = [x + width, y]
    top_left = [x, y + height]
    top_right = [x + width, y + height]
    points = [bottom_left, top_left, top_right, bottom_right, bottom_left]
    polygon = plt.Polygon(points, fill=None, edgecolor='xkcd:rusty red', closed=False)
    plt.gca().add_line(polygon)

def render_word(comma_delimited_rect: str, word: str):
    coordinates_array = comma_delimited_rect.strip().split(',')
    x = int(coordinates_array[0].strip())
    y = int(coordinates_array[1].strip())
    plt.gca().text(x, y-10, word, fontsize=8, color='xkcd:rusty red')

def find_name(name_to_search: str):
    print("loading file picker, please wait...")
    file_name = easygui.fileopenbox()

    result = None
    image_bytes = None

    print("opening file...")
    with open(file_name, "rb") as binary_file:
        print("reading file contents...")
        image_bytes = open(file_name, "rb").read()
        binary_file.close()

    print("uploading file to Azure...")

    headers = {
        'Ocp-Apim-Subscription-Key': OCP_APIM_SUBSCRIPTION_KEY,
        'Content-Type': 'application/octet-stream'
    }
    params = {'handwriting': 'false'}
    response = requests.post(VISION_SERVICE_URL + "recognizeText", headers=headers, params=params, data=image_bytes)
    response.raise_for_status()
    result = response.json()

    print("setting image to plot background...")
    image = Image.open(BytesIO(image_bytes))
    plt.imshow(image)

    print("drawing bounding boxes...")
    words_found = ""
    name_matches_found = ""

    for region in result["regions"]:
        for line in region["lines"]:
            for word in line["words"]:
                detected_word = word["text"]
                words_found += ", " + detected_word
                if detected_word.lower() == name_to_search.lower():
                    name_matches_found += ", " + detected_word
                    render_word(word["boundingBox"], detected_word)
                    render_bounding_box(word["boundingBox"])
                    render_bounding_box(region["boundingBox"])
                    render_bounding_box(line["boundingBox"])

    print("All words discovered:")
    print(words_found)

    print("Name matches discovered:")
    print(name_matches_found)

    print("Complete! Showing result...")
    plt.axis("off")
    plt.show()


# MAIN APPLICATION LOGIC

MVP_NAME = input("Enter the name you want to find:")
find_name(MVP_NAME)