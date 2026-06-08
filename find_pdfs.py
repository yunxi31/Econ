import os
desktop = r"c:\Users\Yunxi\Desktop"
for file in os.listdir(desktop):
    if file.endswith(".pdf"):
        print("Desktop PDF:", file)
