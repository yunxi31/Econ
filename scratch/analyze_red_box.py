from PIL import Image
import os
import glob

image_files = glob.glob(r"c:\Users\Yunxi\Desktop\Econ\scratch\media__*.png")
for path in sorted(image_files):
    img = Image.open(path).convert("RGB")
    width, height = img.size
    
    # Simple blob detector for red pixels
    red_pts = []
    for y in range(height):
        for x in range(width):
            r, g, b = img.getpixel((x, y))
            if r > 180 and g < 60 and b < 60:
                red_pts.append((x, y))
                
    if len(red_pts) > 0 and len(red_pts) < 1500:
        xs = [p[0] for p in red_pts]
        ys = [p[1] for p in red_pts]
        min_x, max_x = min(xs), max(xs)
        min_y, max_y = min(ys), max(ys)
        print(f"File: {os.path.basename(path)}")
        print(f"  Count: {len(red_pts)}, BBox: ({min_x}, {min_y}) -> ({max_x}, {max_y}) (width={max_x-min_x}, height={max_y-min_y})")
        # Crop and save
        cropped = img.crop((max(0, min_x-10), max(0, min_y-10), min(width, max_x+10), min(height, max_y+10)))
        cropped.save(f"c:\\Users\\Yunxi\\Desktop\\Econ\\scratch\\crop_{os.path.basename(path)}")
