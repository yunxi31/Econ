import os
import glob

brain_dir = r'C:\Users\Yunxi\.gemini\antigravity\brain\d4f006b1-eff8-4059-a672-1176a4acc1d6'
image_patterns = [
    os.path.join(brain_dir, "*.png"),
    os.path.join(brain_dir, "*.jpg"),
    os.path.join(brain_dir, "*.webp"),
    os.path.join(brain_dir, ".tempmediaStorage", "*.png"),
    os.path.join(brain_dir, ".tempmediaStorage", "*.webp"),
]

for pat in image_patterns:
    for path in glob.glob(pat):
        print(f"Path: {path}")
        print(f"  Size: {os.path.getsize(path)} bytes")
        print(f"  Modified: {os.path.getmtime(path)}")
