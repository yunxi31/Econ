import os

for root, dirs, files in os.walk(r"c:\Users\Yunxi\Desktop\Econ"):
    for file in files:
        if file.endswith(".cs"):
            print(os.path.join(root, file))
