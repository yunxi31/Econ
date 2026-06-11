from PIL import Image
import os
import glob

image_files = glob.glob(r"c:\Users\Yunxi\Desktop\Econ\scratch\media__*.png")
for path in sorted(image_files):
    try:
        img = Image.open(path)
        print(f"File: {os.path.basename(path)} - Size: {img.size}")
    except Exception as e:
        print(f"Error reading {path}: {e}")
