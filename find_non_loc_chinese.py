import os
import re

# Regex for Chinese characters
chinese_regex = re.compile(r'[\u4e00-\u9fff]+')

# Directory to scan
src_dir = r"c:\Users\Yunxi\Desktop\Econ\MotorTestSystem"

exclude_dirs = ["bin", "obj", ".idea"]

def scan_files():
    results = []
    for root, dirs, files in os.walk(src_dir):
        # Exclude directories
        dirs[:] = [d for d in dirs if d not in exclude_dirs]
        for file in files:
            if file.endswith(('.xaml', '.cs')):
                file_path = os.path.join(root, file)
                with open(file_path, 'r', encoding='utf-8', errors='ignore') as f:
                    lines = f.readlines()
                for idx, line in enumerate(lines, start=1):
                    # Check if line contains Chinese
                    if chinese_regex.search(line):
                        # For xaml files, look for lines that contain Chinese but do NOT use Loc
                        if file.endswith('.xaml'):
                            # Find attributes/tags with Chinese that aren't using Loc or StaticResource
                            # Typically something like Title="电机电性能", Content="查询", Text="正常"
                            # Let's check if there is Loc in the same line.
                            # We can also check if it's inside quotes or text nodes.
                            if 'Loc ' not in line and 'LanguageManager' not in line:
                                results.append((file_path, idx, line.strip()))
                        elif file.endswith('.cs'):
                            # For cs files, look for literal strings with Chinese
                            # Typically "..." containing Chinese
                            # Exclude comments
                            comment_match = re.match(r'^\s*//', line)
                            if not comment_match:
                                results.append((file_path, idx, line.strip()))
    return results

if __name__ == '__main__':
    found = scan_files()
    with open("non_loc.txt", "w", encoding="utf-8") as out_f:
        out_f.write(f"Found {len(found)} entries:\n")
        for path, line_num, content in found:
            rel = os.path.relpath(path, src_dir)
            out_f.write(f"{rel}:{line_num}: {content}\n")
    print(f"Done, wrote to non_loc.txt")
