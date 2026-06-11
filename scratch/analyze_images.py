import os
import numpy as np
from PIL import Image

image_dir = r"c:\Users\Yunxi\Desktop\Econ"
files = [f for f in os.listdir(image_dir) if f.endswith(('.png', '.webp', '.jpg'))]

for file in files:
    path = os.path.join(image_dir, file)
    try:
        with Image.open(path) as img:
            arr = np.array(img)
            # Check for pure black regions or transparent regions
            # Let's see if there is any large rectangle with the same color
            h, w = arr.shape[:2]
            print(f"{file}: shape={arr.shape}")
            if arr.shape[2] == 4:
                # check alpha channel
                alpha = arr[:, :, 3]
                zero_count = np.sum(alpha == 0)
                if zero_count > 0:
                    print(f"  Transparent pixels: {zero_count} ({zero_count/(w*h)*100:.2f}%)")
            # check for black/dark region
            gray = img.convert('L')
            gray_arr = np.array(gray)
            black_pixels = np.sum(gray_arr < 15)
            print(f"  Dark pixels (<15): {black_pixels} ({black_pixels/(w*h)*100:.2f}%)")
    except Exception as e:
        print(f"Error reading {file}: {e}")
