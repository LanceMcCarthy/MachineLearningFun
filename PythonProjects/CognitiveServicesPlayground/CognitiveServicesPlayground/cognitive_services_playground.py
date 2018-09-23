""" Main Application File """
import json
from io import BytesIO
import matplotlib.pyplot as plt
import requests
from PIL import Image
import vision_service

def test_image_analysis():
    """ Test Image analysis service """
    image_url = "https://dvlup.com/wp-content/uploads/2018/03/cropped-blurredfeatureimage.jpg"

    # Use helper py file to make request to Azure
    analysis_result = vision_service.analyze_image(image_url)

    # get the captions from the json dict object
    image_caption = analysis_result["description"]["captions"][0]["text"].capitalize()

    # shows the stringified json in the console
    print(json.dumps(analysis_result, indent=1))

    # Part 2 (not required, uses matplotlib to show a UI for the result)
    image_data = requests.get(source_image_url).content
    image = Image.open(BytesIO(image_data))

    plt.imshow(image)
    plt.subplots_adjust(0, 0, 1, 1, 0, 0)
    plt.axis("off")
    plt.title(image_caption, size="x-large", y=-0.1)
    plt.show()

def test_text_recognition_with_bytes():
    """ Test Text Recognition service using an image as a byte array """
    text_image_url = "https://content.screencast.com/users/LanceMcCarthy/folders/Jing/media/7262d166-a290-4e51-af36-7d45ffd4e7e0/2018-07-16_2020.png"

    image_bytes_data = requests.get(text_image_url).content
    image_bytes = BytesIO(image_bytes_data)

    # PART 1 - Cognitive Services
    # Make the request to Azure using the image byte array
    result = vision_service.recognize_text_from_image_bytes(image_bytes)

    # This is the structure of the result dict
    # result["language"]
    # result["orientation"]
    # result["textAngle"]
    # result["regions"][0]["boundingBox"]
    # result["regions"][0]["lines"][0]["boundingBox"]
    # result["regions"][0]["lines"][0]["words"][0]["boundingBox"]
    # result["regions"][0]["lines"][0]["words"][0]["text"]

    print("Language Detected: " + result["language"])

    # PART 2 - Draw the discovered boxes and the text results

    # PART 2.1 - Set the image as the background of the plot area
    image = Image.open(image_bytes)
    plt.imshow(image)

    # PART 2.2 - Iterate over all the regions, lines and words to draw their bounding boxes

    # Draw a box for every region
    for region in result["regions"]:
        region_box_coordinates = region["boundingBox"].strip().split(',')
        x = int(region_box_coordinates[0].strip())
        y = int(region_box_coordinates[1].strip())
        width = int(region_box_coordinates[2].strip())
        height = int(region_box_coordinates[3].strip())
        region_points = [[x, y], [x, y + height], [x + width, y + height], [x + width, y], [x, y]]
        region_line = plt.Polygon(region_points, closed=None, fill=None, edgecolor='r')
        plt.gca().add_line(region_line)

        # Draw a box around every line in a region
        for line in region["lines"]:
            line_box_coordinates = line["boundingBox"].strip().split(',')
            x2 = int(line_box_coordinates[0].strip())
            y2 = int(line_box_coordinates[1].strip())
            width2 = int(line_box_coordinates[2].strip())
            height2 = int(line_box_coordinates[3].strip())
            line_points = [[x2, y2], [x2, y2 + height2], [x2 + width2, y2 + height2], [x2 + width2, y2], [x2, y2]]
            text_line_line = plt.Polygon(line_points, closed=None, fill=None, edgecolor='r')
            plt.gca().add_line(text_line_line)

            # Draw a box around every word in the line
            for word in line["words"]:
                word_box_coordinates = word["boundingBox"].strip().split(',')
                x3 = int(word_box_coordinates[0].strip())
                y3 = int(word_box_coordinates[1].strip())
                width3 = int(word_box_coordinates[2].strip())
                height3 = int(word_box_coordinates[3].strip())
                word_points = [[x3, y3], [x3, y3 + height3], [x3 + width3, y3 + height3], [x3 + width3, y3], [x3, y3]]
                line3 = plt.Polygon(word_points, closed=None, fill=None, edgecolor='r')
                plt.gca().add_line(line3)
    
    # STEP 2.3 - Turn off the graph axes and show the graph!
    plt.axis("off")
    plt.show()


def test_text_recognition_with_url():
    """ Test Text Recognition service using an image as a URL string """
    text_image_url = "https://content.screencast.com/users/LanceMcCarthy/folders/Jing/media/7262d166-a290-4e51-af36-7d45ffd4e7e0/2018-07-16_2020.png"

    # Use helper py file to make request to Azure
    text_recog_result = vision_service.recognize_text_with_image_url(text_image_url)



    print(json.dumps(text_recog_result, indent=1))

print("Test Options:")
print("1 - Image Analysis")
print("2 - Text Recognition, with bounding boxes, using image bytes")
print("3 - Text Recognition using image URL")

CHOSEN_TEST = input("Enter 1, 2 or 3...")

if CHOSEN_TEST == "1":
    test_image_analysis()
elif CHOSEN_TEST == "2":
    test_text_recognition_with_bytes()
elif CHOSEN_TEST == "3":
    test_text_recognition_with_url()
