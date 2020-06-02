# Pix2Pix Genrator for Public Parts Project
# Trained on dataset Version 3, 1420 training samples
# Upscaled 4x, placed on (0,0), 256x256, buffer size set to dataset size
# Model saved from checkpoint 41, after 200 training epochs

import tensorflow as tf
import os
import glob
import time 
from matplotlib import pyplot as plt
from PIL import Image as pimg
import numpy as np
import tensorboard as tb
import sys



##
## Inputs and Variables
##
IMG_WIDTH = 256
IMG_HEIGHT = 256
BATCH_SIZE = 1
models_dir = os.path.dirname(__file__) + '/saved_models/'
model_path = models_dir + "PP_p2p_ckpt-41"

##
## Functions and methods
##
def load(image_file):
    image = tf.io.read_file(image_file)
    image = tf.image.decode_png(image, channels=3) #added channel translation to be RGB

    image = tf.cast(image, tf.float32)

    return image

def resize(input_image, height, width):
    input_image = tf.image.resize(input_image, [height, width], method=tf.image.ResizeMethod.NEAREST_NEIGHBOR)

    return input_image

def normalize(input_image):
    # Normalize for 256
    input_image = (input_image / 127.5) - 1

    return input_image

def load_image_test(image_file):
    input_image = load(image_file)
    input_image = resize(input_image, IMG_HEIGHT, IMG_WIDTH)

    input_image = normalize(input_image)

    return input_image


##
## Get folder to analyse from Unity
##
# test_dir = models_dir + 'prem_test/input' # Uncomment this to run straight from python
test_dir = sys.argv[1]
# test_dir = 'D:\\GitRepo\\PublicParts\\PP_AI_Studies\\temp_sr'
test_dataset = tf.data.Dataset.list_files(test_dir +'/*.png', shuffle=False)
test_dataset = test_dataset.map(load_image_test)
test_dataset = test_dataset.batch(BATCH_SIZE)

# save_dir = models_dir + 'prem_test/output'
save_dir = test_dir

def EvaluateAllTestFolder(model):
    save_path = save_dir + '/'
    for evaluate_file_name, evaluate_tensor in zip(os.listdir(test_dir), test_dataset):
        prediction = model(evaluate_tensor, training=True)

        true_prediction = (prediction[0] + 1) * 127.5
        true_prediction = tf.cast(true_prediction, tf.uint8)
        print(evaluate_file_name)
        true_prediction = tf.image.encode_png(true_prediction)
        print(save_path + evaluate_file_name)
        
        tf.io.write_file(save_path + evaluate_file_name, true_prediction)


##
## Main Code
##


# loaded_generator = tf.keras.models.load_model(model_path, compile=False)
loaded_generator = tf.saved_model.load(model_path)
EvaluateAllTestFolder(loaded_generator)