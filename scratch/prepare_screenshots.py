import shutil
import glob
import os

src_dir = r"C:\Users\Yunxi\.gemini\antigravity\brain\cd836c43-12a6-4308-8e90-8d65a5b9be78"
dest_dir = r"c:\Users\Yunxi\Desktop\Econ\scratch"

# Copy PNG files
png_files = glob.glob(os.path.join(src_dir, "media__*.png"))
for f in png_files:
    shutil.copy(f, dest_dir)

# Also copy from the temp media storage
temp_png_files = glob.glob(os.path.join(src_dir, ".tempmediaStorage", "media_*.png"))
for f in temp_png_files:
    shutil.copy(f, dest_dir)

print("Copied files:")
for f in glob.glob(os.path.join(dest_dir, "*.png")):
    print(" -", os.path.basename(f))

# Generate HTML
html_content = """<!DOCTYPE html>
<html>
<head>
    <title>Screenshots Viewer</title>
    <style>
        body { font-family: sans-serif; background: #1e1e1e; color: #fff; margin: 20px; }
        .image-container { margin-bottom: 40px; border-bottom: 2px solid #444; padding-bottom: 20px; }
        h3 { margin-bottom: 10px; color: #00DFFF; }
        img { max-width: 100%; border: 1px solid #555; }
    </style>
</head>
<body>
    <h1>App Screenshots</h1>
"""

for f in sorted(glob.glob(os.path.join(dest_dir, "*.png"))):
    name = os.path.basename(f)
    html_content += f"""
    <div class="image-container">
        <h3>{name}</h3>
        <img src="{name}" />
    </div>"""

html_content += """
</body>
</html>
"""

with open(os.path.join(dest_dir, "screenshots.html"), "w", encoding="utf-8") as html_file:
    html_file.write(html_content)

print("Generated screenshots.html")
