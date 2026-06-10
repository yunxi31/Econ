with open("extracted_modbus_pages.txt", "r", encoding="utf-8") as f:
    lines = f.readlines()

current_page = ""
for line in lines:
    if "================== PAGE" in line:
        current_page = line.strip()
    # If the line contains typical Modbus addresses or PLC elements (like M0, D0, X0, Y0, SD0, H, 40001, etc.)
    if any(k in line for k in ["软元件", "Modbus", "映射", "0x", "H5U", "Easy"]):
        if len(line.strip()) > 10:
            print(f"{current_page}: {line.strip()}")
