import pypdf

pdf_path = r"c:\Users\Yunxi\Desktop\Econ\H5U&Easy系列可编程逻辑控制器编程与应用手册-CN-A09.PDF"
reader = pypdf.PdfReader(pdf_path)

pages_to_extract = list(range(153, 171)) + list(range(533, 546))

with open("extracted_modbus_pages.txt", "w", encoding="utf-8") as f:
    for p_num in pages_to_extract:
        if p_num <= len(reader.pages):
            page = reader.pages[p_num - 1]
            f.write(f"================== PAGE {p_num} ==================\n")
            text = page.extract_text()
            f.write(text if text else "")
            f.write("\n\n")

print("Modbus extraction complete")
