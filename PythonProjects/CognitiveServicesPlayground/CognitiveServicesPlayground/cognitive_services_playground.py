""" Main Application File """
import easygui
import json
from io import BytesIO
import matplotlib.pyplot as plt
import requests
from PIL import Image
import vision_service


def render_bounding_box(comma_delimited_rect: str, plot: plt):
    """ Helper method to render bounding box around the detected word """
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
    plot.gca().add_line(polygon)

def render_word(comma_delimited_rect: str, word: str, plot: plt):
    """ Helper method to render the word above the bounding box """
    coordinates_array = comma_delimited_rect.strip().split(',')
    x = int(coordinates_array[0].strip())
    y = int(coordinates_array[1].strip())
    plot.gca().text(x, y-10, word, fontsize=8, color='xkcd:rusty red')

def test_text_recognition_with_bytes(image_url: str):
    """ Test Text Recognition service using an image as a byte array """

    # This loads up the bytes from the online image as an example of a file-loaded image
    image_bytes_data = requests.get(image_url).content
    image_bytes = BytesIO(image_bytes_data)

    # ----- PART 1 ------
    # Make the request to Azure Cognitive Services using the image byte array
    result = vision_service.recognize_text_from_image_bytes(image_bytes)

    # If you want to see the result in the console, use this approach
    # print(json.dumps(result, indent=1))

    # ----- PART 2 ------
    # Draw the discovered boxes and the text results
    # Step 1 - Set the image as the background of the plot area
    image = Image.open(image_bytes)
    plt.imshow(image)

    # Step 2 - Iterate over all the regions, lines and words to draw their bounding boxes

    # Draw a box for every region
    for region in result["regions"]:
        region_box = render_bounding_box(region["boundingBox"], plot=plt)
        # Draw a box around every line in a region
        for line in region["lines"]:
            line_box = render_bounding_box(line["boundingBox"], plot=plt)
            # Draw a box around every word in the line
            for word in line["words"]:
                render_bounding_box(word["boundingBox"], plot=plt)
                render_word(comma_delimited_rect=word["boundingBox"], word=word["text"], plot=plt)

    # Step 3 - Turn off the graph axes and show the graph!
    plt.axis("off")
    plt.show()


def test_text_recognition_with_url(image_url: str):
    """ Test Text Recognition service using an image as a URL string """

    # ----- PART 1 ------
    # Make the request to Azure Cognitive Services using the image byte array
    result = vision_service.recognize_text_with_image_url(image_url)

    # If you want to see the result in the console, use this approach
    # print(json.dumps(result, indent=1))

    # ----- PART 2 ------
    # Draw the discovered boxes and the text results

    # Step 1 - Set the image as the background of the plot area
    image_bytes_data = requests.get(image_url).content
    image_bytes = BytesIO(image_bytes_data)
    image = Image.open(image_bytes)
    plt.imshow(image)

    # Step 2 - Iterate over all the regions, lines and words to draw their bounding boxes
    # Draw a box for every region
    for region in result["regions"]:
        render_bounding_box(region["boundingBox"], plot=plt)
        # Draw a box around every line in a region
        for line in region["lines"]:
            render_bounding_box(line["boundingBox"], plot=plt)
            # Draw a box around every word in the line
            for word in line["words"]:
                render_bounding_box(word["boundingBox"], plot=plt)
                render_word(comma_delimited_rect=word["boundingBox"], word=word["text"], plot=plt)

    # Step 3 - Turn off the graph axes and show the graph!
    plt.axis("off")
    plt.show()


def test_image_analysis(image_url: str):
    """ Test Image analysis service """

    # ----- PART 1 ------
    # Call the image analysis API
    result = vision_service.analyze_image(image_url)

    # ----- PART 2 ------
    # Get the captions from the json dict object
    image_caption = result["description"]["captions"][0]["text"].capitalize()

    # If you want to see the result in the console, use this approach
    # print(json.dumps(result, indent=1))

    # ----- PART 3 ------
    # Not required, uses matplotlib to show the image and result in title
    image_data = requests.get(image_url).content
    image = Image.open(BytesIO(image_data))

    plt.imshow(image)
    plt.subplots_adjust(0, 0, 1, 1, 0, 0)
    plt.axis("off")
    plt.title(image_caption, size="x-large", y=-0.1)
    plt.show()


def test_text_recognition_with_local_file():
    """ Test Image analysis service using locally picked image file"""

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
    result = vision_service.recognize_text_from_image_bytes(image_bytes)

    print("setting image to plot background...")
    image = Image.open(BytesIO(image_bytes))
    plt.imshow(image)

    print("drawing bounding boxes...")
    for region in result["regions"]:
        render_bounding_box(region["boundingBox"], plot=plt)
        # Draw a box around every line in a region
        for line in region["lines"]:
            render_bounding_box(line["boundingBox"], plot=plt)
            # Draw a box around every word in the line
            for word in line["words"]:
                render_bounding_box(word["boundingBox"], plot=plt)
                render_word(comma_delimited_rect=word["boundingBox"], word=word["text"], plot=plt)

    # Step 3 - Turn off the graph axes and show the graph!
    plt.axis("off")

    print("Complete! Showing result...")
    plt.show()

# Main application Logic

COMPUTER_ON_TABLE_IMAGE_URL = "https://dvlup.com/wp-content/uploads/2018/03/cropped-blurredfeatureimage.jpg"
CODE_SCREENSHOT_URL = "https://content.screencast.com/users/LanceMcCarthy/folders/Jing/media/7262d166-a290-4e51-af36-7d45ffd4e7e0/2018-07-16_2020.png"

print("Test Options:")
print("1 - Image Analysis")
print("2 - Text Recognition using test image bytes (Bounding Boxes)")
print("3 - Text Recognition using test image URL (Bounding Boxes)")
print("4 - Text Recognition using an image URL (Bounding Boxes)")
print("5 - Text Recognition using a loaded file  (Bounding Boxes)")

CHOSEN_TEST = input("Enter 1, 2, 3, 4 or 5...")

if CHOSEN_TEST == "1":
    test_image_analysis(image_url=COMPUTER_ON_TABLE_IMAGE_URL)
elif CHOSEN_TEST == "2":
    test_text_recognition_with_bytes(image_url=CODE_SCREENSHOT_URL)
elif CHOSEN_TEST == "3":
    test_text_recognition_with_url(image_url=CODE_SCREENSHOT_URL)
elif CHOSEN_TEST == "4":
    TEST_URL = input("Paste in URL to the image, then hit enter...")
    test_text_recognition_with_url(image_url=TEST_URL)
elif CHOSEN_TEST == "5":
    test_text_recognition_with_local_file()
