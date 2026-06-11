import os

def count_lines_in_directory(directory):
    total_lines = 0
    cs_files = 0
    
    for root, dirs, files in os.walk(directory):
        for file in files:
            if file.endswith('.cs'):
                file_path = os.path.join(root, file)
                try:
                    with open(file_path, 'r', encoding='utf-8') as f:
                        lines = f.readlines()
                        total_lines += len(lines)
                        cs_files += 1
                        print(f"Processing {file_path}: {len(lines)} lines")
                except Exception as e:
                    print(f"Error reading {file_path}: {e}")
    
    return total_lines, cs_files

if __name__ == "__main__":
    directory = "C:/Users/Yunxi/Desktop/Econ/MotorTestSystem"
    total_lines, cs_files = count_lines_in_directory(directory)
    print(f"\nTotal C# files: {cs_files}")
    print(f"Total lines of code: {total_lines}")