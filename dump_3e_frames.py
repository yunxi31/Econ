import pypdf
import os

folder = r"c:\Users\Yunxi\Desktop\Econ\三菱MC"
files = [
    "QnA兼容3E读取请求帧.pdf",
    "QnA兼容3E读取响应帧.pdf",
    "QnA兼容3E写入请求帧.pdf",
    "QnA兼容3E写入响应帧.pdf"
]

for filename in files:
    path = os.path.join(folder, filename)
    if os.path.exists(path):
        print(f"=== {filename} ===")
        reader = pypdf.PdfReader(path)
        for idx, page in enumerate(reader.pages):
            print(page.extract_text())
        print("="*50)
