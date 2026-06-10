import pypdf

pdf_path = r"c:\Users\Yunxi\Desktop\Econ\三菱MC\MELSEC通讯协议.pdf"
reader = pypdf.PdfReader(pdf_path)

found = 0
for idx, page in enumerate(reader.pages):
    text = page.extract_text()
    if not text:
        continue
    # Standard Mitsubishi device codes: M is 0x90, D is 0xA8, X is 0x9C, Y is 0x9D, etc.
    # Let's search for pages containing these strings
    if "A8" in text and "90" in text and "9C" in text and "9D" in text and "软元件" in text:
        print(f"Page {idx + 1}:")
        print(text[:1000])
        print("="*40)
        found += 1
        if found >= 5:
            break
