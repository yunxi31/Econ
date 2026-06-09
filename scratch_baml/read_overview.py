import os

log_path = r"C:\Users\Yunxi\.gemini\antigravity\brain\1c4bd4d0-ed8f-4c67-947f-fe446471b6e6\.system_generated\logs\overview.txt"
if os.path.exists(log_path):
    print("Log file size:", os.path.getsize(log_path))
    with open(log_path, "r", encoding="utf-8", errors="ignore") as f:
        content = f.read()
    
    # 查找“通信与系统配置”或者“PlcConfigCardControl”或者“任务一”
    search_keywords = ["任务三", "PlcConfigCardControl", "端口", "垂直截断"]
    for kw in search_keywords:
        idx = content.find(kw)
        if idx != -1:
            print(f"\nFound keyword '{kw}' at index {idx}:")
            # 打印前后 500 个字符
            start = max(0, idx - 300)
            end = min(len(content), idx + 800)
            print(content[start:end])
else:
    print("Log file not found.")
