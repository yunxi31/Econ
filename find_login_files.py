import os
root_dir = r"c:\Users\Yunxi\Desktop\Econ"
for root, dirs, files in os.walk(root_dir):
    for file in files:
        if "login" in file.lower() or "auth" in file.lower():
            print(os.path.join(root, file))
