import pypdf
import os
import sys

pdf_path = r"c:\Users\Yunxi\Desktop\Econ\H5U&Easy系列可编程逻辑控制器编程与应用手册-CN-A09.PDF"

if not os.path.exists(pdf_path):
    print("PDF not found at", pdf_path)
    sys.exit(1)

reader = pypdf.PdfReader(pdf_path)
print(f"Total pages: {len(reader.pages)}")

keywords = ["MC", "三菱", "Melsec", "Modbus", "以太网", "Ethernet", "TCP", "通讯", "通信"]
results = {kw: [] for kw in keywords}

for idx, page in enumerate(reader.pages):
    text = page.extract_text()
    if not text:
        continue
    for kw in keywords:
        if kw.lower() in text.lower():
            results[kw].append(idx + 1)

for kw, pages in results.items():
    print(f"Keyword '{kw}': found on {len(pages)} pages. First 10 pages: {pages[:10]}")
