import os
import glob
from PIL import Image

image_files = glob.glob(r"C:\Users\Yunxi\.gemini\antigravity\brain\cd836c43-12a6-4308-8e90-8d65a5b9be78\media__*.png")
for path in sorted(image_files):
    try:
        img = Image.open(path).convert("RGB")
        width, height = img.size
        # Find pixels that are highly red: R is high, G and B are low
        red_pixels = 0
        min_x, min_y, max_x, max_y = width, height, 0, 0
        for y in range(height):
            for x in range(width):
                r, g, b = img.getpixel((x, y))
                if r > 180 and g < 50 and b < 50:
                    red_pixels += 1
                    min_x = min(min_x, x)
                    min_y = min(min_y, y)
                    max_x = max(max_x, x)
                    max_y = max(max_y, y)
        if red_pixels > 50:
            print(f"File: {os.path.basename(path)}")
            print(f"  Red pixels: {red_pixels}, Bounding box: ({min_x}, {min_y}) to ({max_x}, {max_y})")
    except Exception as e:
        print(f"Error reading {path}: {e}")
