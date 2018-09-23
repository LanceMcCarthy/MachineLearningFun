""" Main Application File """
import json
from io import BytesIO
import matplotlib.pyplot as plt
import requests
from PIL import Image
import vision_service


def generate_bounding_box_polygon(comma_delimited_rect: str):
    """ Custom helper method I wrote to create a bounding box for matplot using Azure data """
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
    return polygon


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
        region_box = generate_bounding_box_polygon(region["boundingBox"])
        plt.gca().add_line(region_box)

        # Draw a box around every line in a region
        for line in region["lines"]:
            line_box = generate_bounding_box_polygon(line["boundingBox"])
            plt.gca().add_line(line_box)

            detected_text = ""

            # Draw a box around every word in the line
            for word in line["words"]:
                detected_text += word
                word_box = generate_bounding_box_polygon(word["boundingBox"])
                plt.gca().add_line(word_box)

            # output each line's detected text
            print(detected_text)

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
        region_box = generate_bounding_box_polygon(region["boundingBox"])
        plt.gca().add_line(region_box)

        # Draw a box around every line in a region
        for line in region["lines"]:
            line_box = generate_bounding_box_polygon(line["boundingBox"])
            plt.gca().add_line(line_box)

            detected_text = ""

            # Draw a box around every word in the line
            for word in line["words"]:
                word_box = generate_bounding_box_polygon(word["boundingBox"])
                plt.gca().add_line(word_box)

            # output each line's detected text
            print(detected_text)

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


# Main application Logic

COMPUTER_ON_TABLE_IMAGE_URL = "https://dvlup.com/wp-content/uploads/2018/03/cropped-blurredfeatureimage.jpg"
CODE_SCREENSHOT_URL = "https://content.screencast.com/users/LanceMcCarthy/folders/Jing/media/7262d166-a290-4e51-af36-7d45ffd4e7e0/2018-07-16_2020.png"

print("Test Options:")
print("1 - Image Analysis")
print("2 - Text Recognition using test image bytes (Bounding Boxes)")
print("3 - Text Recognition using test image URL (Bounding Boxes)")
print("4 - Text Recognition using a pasted URL (Bounding Boxes)")

CHOSEN_TEST = input("Enter 1, 2, 3 or 4...")

if CHOSEN_TEST == "1":
    test_image_analysis(image_url=COMPUTER_ON_TABLE_IMAGE_URL)
elif CHOSEN_TEST == "2":
    test_text_recognition_with_bytes(image_url=CODE_SCREENSHOT_URL)
elif CHOSEN_TEST == "3":
    test_text_recognition_with_url(image_url=CODE_SCREENSHOT_URL)
elif CHOSEN_TEST == "4":
    TEST_URL = input("Paste in URL to the image, then hit enter...")
    test_text_recognition_with_url(image_url=TEST_URL)
