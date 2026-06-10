with open("extracted_modbus_pages.txt", "r", encoding="utf-8") as f:
    text = f.read()

pages = text.split("================== PAGE ")
for page in pages:
    if page.startswith("157 ") or page.startswith("158 "):
        print("=" * 40)
        print("PAGE " + page[:1500])
