import os
import pypdf

desktop = r"c:\Users\Yunxi\Desktop"
for file in os.listdir(desktop):
    if file.endswith(".pdf") and len(file) < 15: # "开发需求.pdf" is short
        path = os.path.join(desktop, file)
        print("Reading:", file)
        try:
            reader = pypdf.PdfReader(path)
            for i, page in enumerate(reader.pages):
                print(f"--- Page {i+1} ---")
                print(page.extract_text())
        except Exception as e:
            print("Error:", e)
