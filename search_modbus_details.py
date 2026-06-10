import pypdf
import os

pdf_path = r"c:\Users\Yunxi\Desktop\Econ\H5U&Easy系列可编程逻辑控制器编程与应用手册-CN-A09.PDF"
reader = pypdf.PdfReader(pdf_path)

results = []
for idx, page in enumerate(reader.pages):
    text = page.extract_text()
    if not text:
        continue
    # Search for Modbus and some Chinese characters like 软元件 (soft element) or 地址 (address) or 对应 (corresponding)
    if "modbus" in text.lower() and ("软元件" in text or "地址" in text or "映射" in text):
        results.append((idx + 1, text[:300].replace('\n', ' '))) # just print page number and snippet

print(f"Total matching pages: {len(results)}")
for p_num, snippet in results[:30]:
    print(f"Page {p_num}: {snippet}...")
