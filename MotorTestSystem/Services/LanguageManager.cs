using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace MotorTestSystem
{
    public class LanguageManager : INotifyPropertyChanged
    {
        public static LanguageManager Instance { get; } = new LanguageManager();

        private string _currentLanguage = "ZH"; // "ZH" (Chinese) or "EN" (English)
        public string CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                if (_currentLanguage != value)
                {
                    _currentLanguage = value;
                    OnPropertyChanged(nameof(CurrentLanguage));
                    OnPropertyChanged("Item[]"); // Notifies all bindings using indexer
                }
            }
        }

        public string this[string key]
        {
            get
            {
                if (string.IsNullOrEmpty(key)) return string.Empty;
                
                string trimmedKey = key.Trim().Replace("\r\n", "\n");
                if (CurrentLanguage == "EN")
                {
                    if (_translations.TryGetValue(trimmedKey, out var translation))
                    {
                        return translation;
                    }

                    // Smart translation logic for dynamic messages:
                    
                    // 1. "账号 {account} 已存在"
                    if (trimmedKey.StartsWith("账号 ") && trimmedKey.EndsWith(" 已存在"))
                    {
                        string account = trimmedKey.Substring(3, trimmedKey.Length - 7);
                        return $"Account {account} already exists";
                    }

                    // 2. "确定要重置用户 {name} ({account}) 的密码为初始密码吗？"
                    if (trimmedKey.StartsWith("确定要重置用户 ") && trimmedKey.EndsWith(" 的密码为初始密码吗？"))
                    {
                        string middle = trimmedKey.Substring(8, trimmedKey.Length - 19);
                        return $"Are you sure you want to reset the password for user {middle} to default?";
                    }

                    // 3. "用户 {name} 的密码已重置为：123456"
                    if (trimmedKey.StartsWith("用户 ") && trimmedKey.EndsWith(" 的密码已重置为：123456"))
                    {
                        string name = trimmedKey.Substring(3, trimmedKey.Length - 18);
                        return $"The password for user {name} has been reset to: 123456";
                    }

                    // 4. "摄像头连接成功！\n设备序列号: ... \n通道数: ..."
                    if (trimmedKey.StartsWith("摄像头连接成功！"))
                    {
                        int snIdx = trimmedKey.IndexOf("设备序列号:");
                        int chIdx = trimmedKey.IndexOf("通道数:");
                        string sn = snIdx != -1 ? trimmedKey.Substring(snIdx + 6).Split('\n')[0].Trim() : "";
                        string ch = chIdx != -1 ? trimmedKey.Substring(chIdx + 4).Split('\n')[0].Trim() : "";
                        return $"Camera connected successfully!\nSerial Number: {sn}\nChannels: {ch}";
                    }

                    // 5. "连接失败: {msg}"
                    if (trimmedKey.StartsWith("连接失败: "))
                    {
                        string msg = trimmedKey.Substring(5);
                        return $"Connection failed: {msg}";
                    }

                    // 6. "连接异常: {msg}"
                    if (trimmedKey.StartsWith("连接异常: "))
                    {
                        string msg = trimmedKey.Substring(5);
                        return $"Connection exception: {msg}";
                    }

                    // 7. "抓图路径: {path}\n\n注意: 实际抓图需要先启动预览窗口"
                    if (trimmedKey.StartsWith("抓图路径: ") && trimmedKey.Contains("注意:"))
                    {
                        int noticeIndex = trimmedKey.IndexOf("注意:");
                        string path = trimmedKey.Substring(5, noticeIndex - 5).Trim();
                        return $"Capture saved to: {path}\n\nNote: Live preview must be started for actual capture.";
                    }

                    // 8. "抓图失败: {msg}"
                    if (trimmedKey.StartsWith("抓图失败: "))
                    {
                        string msg = trimmedKey.Substring(5);
                        return $"Capture failed: {msg}";
                    }

                    // 9. "{config.Name} ({config.IpAddress}:{config.Port}) 连接正常。"
                    if (trimmedKey.EndsWith(" 连接正常。"))
                    {
                        string left = trimmedKey.Substring(0, trimmedKey.Length - 6);
                        return $"{left} connection normal.";
                    }

                    // 10. "{config.Name} ({config.IpAddress}:{config.Port}) 无法建立连接，请检查 IP、端口、协议与网线连接。"
                    if (trimmedKey.EndsWith(" 无法建立连接，请检查 IP、端口、协议与网线连接。"))
                    {
                        string left = trimmedKey.Substring(0, trimmedKey.Length - 23);
                        return $"Could not establish connection to {left}. Please check IP, Port, Protocol, and cable connections.";
                    }

                    // 11. "电机 [{barcode}] 的追溯单已发送至打印机。"
                    if (trimmedKey.StartsWith("电机 [") && trimmedKey.EndsWith("] 的追溯单已发送至打印机。"))
                    {
                        string barcode = trimmedKey.Substring(4, trimmedKey.Length - 17);
                        return $"The trace slip for motor [{barcode}] has been sent to the printer.";
                    }

                    // 12. "打印失败: {msg}"
                    if (trimmedKey.StartsWith("打印失败: "))
                    {
                        string msg = trimmedKey.Substring(5);
                        return $"Print failed: {msg}";
                    }

                    // 13. "打开报告失败: {msg}"
                    if (trimmedKey.StartsWith("打开报告失败: "))
                    {
                        string msg = trimmedKey.Substring(7);
                        return $"Failed to open report: {msg}";
                    }

                    // 14. "复制条码失败: {msg}"
                    if (trimmedKey.StartsWith("复制条码失败: "))
                    {
                        string msg = trimmedKey.Substring(7);
                        return $"Failed to copy barcode: {msg}";
                    }

                    // 15. "成功导出 {count} 条记录至桌面:\n{path}"
                    if (trimmedKey.StartsWith("成功导出 ") && trimmedKey.Contains(" 条记录至桌面"))
                    {
                        int midIndex = trimmedKey.IndexOf(" 条记录至桌面");
                        string count = trimmedKey.Substring(5, midIndex - 5);
                        int pathStart = trimmedKey.IndexOf('\n');
                        string path = pathStart != -1 ? trimmedKey.Substring(pathStart).Trim() : "";
                        return $"Successfully exported {count} records to desktop:\n{path}";
                    }

                    // 16. "导出数据失败: {msg}"
                    if (trimmedKey.StartsWith("导出数据失败: "))
                    {
                        string msg = trimmedKey.Substring(7);
                        return $"Failed to export data: {msg}";
                    }
                }
                return key;
            }
        }

        public void ToggleLanguage()
        {
            CurrentLanguage = CurrentLanguage == "ZH" ? "EN" : "ZH";
        }

        private readonly Dictionary<string, string> _translations = new(StringComparer.OrdinalIgnoreCase)
        {
            // Navigation & Main Layout
            { "电机电性能测试系统", "Motor Test System" },
            { "生产监控", "Monitor" },
            { "数据追溯", "Data Trace" },
            { "生产看板", "Dashboard" },
            { "用户管理", "User" },
            { "日志中心", "Log Center" },
            { "通知中心", "Notification Center" },
            { "系统配置", "Config" },
            { "节点状态", "Node Status" },
            { "用户名", "Username" },
            { "用户角色", "Role" },
            { "退出登录", "Logout" },
            { "未登录", "Not Logged In" },
            { "未知", "Unknown" },
            { "用户主页", "User Home" },

            // Monitor View (GW-M01, etc.)
            { "待机", "Standby" },
            { "运行中", "Running" },
            { "故障", "Fault" },
            { "在线", "Online" },
            { "离线", "Offline" },
            { "设备运行状态", "Equipment Status" },
            { "正常", "Normal" },
            { "异常", "Abnormal" },
            { "站号", "Station" },
            { "电压", "Voltage" },
            { "电流", "Current" },
            { "转速", "Speed" },
            { "扭矩", "Torque" },
            { "温度", "Temp" },
            { "振动值", "Vibration" },
            { "噪音分贝", "Noise (dB)" },
            { "测试指标详细数据", "Detailed Test Metrics" },
            { "当前电流", "Current" },
            { "最大扭矩", "Max Torque" },
            { "最高温度", "Max Temp" },
            { "累计加工总数", "Total Output" },
            { "不合格总数", "Total NG" },
            { "完成进度", "Progress" },
            { "准备就绪", "Ready" },
            { "正在连接...", "Connecting..." },
            { "已连接", "Connected" },
            { "已断开", "Disconnected" },
            { "测试连接", "Test Conn" },
            { "连接成功", "Conn Success" },
            { "连接失败", "Conn Failed" },
            { "抓图", "Capture" },
            { "摄像头连接成功！", "Camera connected successfully!" },
            { "请先连接摄像头", "Please connect camera first" },
            { "抓图成功", "Capture successful" },
            { "抓图失败", "Capture failed" },

            // History View / Query
            { "数据追溯与查询", "Data Trace & Query" },
            { "产品条码", "Product Barcode" },
            { "电机条码", "Motor Barcode" },
            { "开始日期", "Start Date" },
            { "结束日期", "End Date" },
            { "最终判定", "Final Decision" },
            { "检测结果", "Test Result" },
            { "合格", "PASS" },
            { "不合格", "NG" },
            { "查询", "Query" },
            { "重置", "Reset" },
            { "导出 CSV", "Export CSV" },
            { "打印追溯单", "Print Slip" },
            { "查看完整报告", "Full Report" },
            { "显示第", "Show" },
            { "页 / 共", " of " },
            { "页", "" },
            { "条记录", "records" },
            { "条测试数据", "test records" },
            { "共", "Total" },
            { "暂无数据", "No Data" },
            { "时间范围", "Time Range" },
            { "最近一小时", "Last Hour" },
            { "今日", "Today" },
            { "本周", "This Week" },
            { "本月", "This Month" },
            { "次", " times" },

            // Dashboard View
            { "今日良率", "Pass Rate" },
            { "总测试数", "Total Tests" },
            { "合格总数", "Total Pass" },
            { "不良总数", "Total NG" },
            { "目标良率 98%", "Target Pass Rate 98%" },
            { "良率", "Pass Rate" },
            { "良率趋势及目标对比 (%)", "Pass Rate Trend & Target (%)" },
            { "各阶段不良占比与分布", "NG Ratio & Distribution" },
            { "故障类别 Top 5 排行", "Top 5 Fault Categories" },
            { "小时生产统计 (pcs)", "Hourly Production (pcs)" },
            { "周一", "Mon" },
            { "周二", "Tue" },
            { "周三", "Wed" },
            { "周四", "Thu" },
            { "周五", "Fri" },
            { "周六", "Sat" },
            { "周日", "Sun" },
            { "第一周", "Week 1" },
            { "第二周", "Week 2" },
            { "第三周", "Week 3" },
            { "第四周", "Week 4" },

            // User View
            { "用户管理与权限", "User Management & Role" },
            { "管理系统访问权限与操作员信息", "Manage Access Control and Operators" },
            { "搜索操作员账号或姓名...", "Search by account or name..." },
            { "新增用户", "Add User" },
            { "编辑", "Edit" },
            { "重置密码", "Reset Pwd" },
            { "启用", "Enable" },
            { "禁用", "Disable" },
            { "用户账号", "Account" },
            { "姓名", "Name" },
            { "所属角色", "Role" },
            { "状态", "Status" },
            { "最后登录时间", "Last Login" },
            { "系统管理员", "Administrator" },
            { "操作员 (Operator)", "Operator" },
            { "管理员 (Admin)", "Admin" },
            { "维护员 (Maintainer)", "Maintainer" },
            { "新增", "Add" },
            { "保存", "Save" },
            { "取消", "Cancel" },

            // Config View
            { "通信与系统配置", "Communication & System Config" },
            { "保存所有配置", "Save All Configurations" },
            { "设备控制", "Device Control" },
            { "设备校准", "Device Calibration" },
            { "协议", "Protocol" },
            { "IP 地址", "IP Address" },
            { "端口", "Port" },
            { "限值", "Limit" },
            { "标准上限", "Upper Limit" },
            { "标准下限", "Lower Limit" },
            { "运行日志", "Run Log" },
            { "操作日志", "Op Log" },
            { "系统警报", "Sys Alarm" },
            { "时间", "Time" },
            { "级别", "Level" },
            { "来源/机台", "Source/Machine" },
            { "消息内容", "Message" },
            { "搜索事件消息...", "Search events..." },
            { "全部", "All" },
            { "系统", "System" },
            { "维护", "Maintain" },
            { "已读", "Read" },
            { "未读", "Unread" },
            { "清除日志", "Clear Logs" },
            { "诊断事件日志", "Diagnostic Event Logs" },
            { "重试连接", "Retry Connection" },

            // Dialogs / Popups / Alerts
            { "编辑用户信息", "Edit User Info" },
            { "用户账号*", "Account*" },
            { "姓名*", "Name*" },
            { "选择身份角色*", "Select Role*" },
            { "系统警报配置", "System Alarm Config" },
            { "重置密码确认", "Reset Password Confirmation" },
            { "确定要重置用户", "Are you sure you want to reset password for user" },
            { "的密码为初始密码吗？", "'s password to default?" },
            { "提示", "Notification" },
            { "成功", "Success" },
            { "失败", "Failure" },
            { "重置密码成功！初始密码为：123456", "Password reset successfully! Default is: 123456" },
            { "保存成功", "Saved successfully" },
            { "保存失败", "Save failed" },
            { "请输入账号", "Please enter account" },
            { "请输入姓名", "Please enter name" },
            { "密码重置成功", "Password reset successful" },
            { "密码重置确认", "Confirm password reset" },
            { "输入验证", "Input Validation" },
            { "创建失败", "Create Failed" },
            { "更新失败", "Update Failed" },
            { "重置失败", "Reset Failed" },
            { "保存配置成功", "Configuration Saved Successfully" },
            { "所有配置已成功保存至系统数据库中。", "All configurations have been successfully saved to the system database." },
            { "当前没有符合条件的测试数据可供导出！", "There is no matching test data available for export!" },
            { "导出提示", "Export Notification" },
            { "数据导出成功", "Data Export Successful" },
            { "导出错误", "Export Error" },
            { "打印错误", "Print Error" },
            { "报告错误", "Report Error" },
            { "账号不能为空！", "Account cannot be empty!" },
            { "姓名不能为空！", "Name cannot be empty!" },
            { "请输入登录密码！", "Please enter login password!" },
            { "两次输入的密码不一致！", "Passwords do not match!" },
            { "打印超时已取消，请检查打印机连接状态后重试。", "Print timeout. Please check printer connection and try again." },
            { "账号不能为空", "Account cannot be empty" },
            { "姓名不能为空", "Name cannot be empty" },
            { "密码不能为空", "Password cannot be empty" },
            { "新密码不能为空", "New password cannot be empty" },
            { "旧密码不正确", "Old password is incorrect" },
            { "用户不存在", "User does not exist" },
            { "确定", "OK" },
            { "是", "Yes" },
            { "否", "No" },

            // Login Window
            { "系统安全登录", "Secure System Login" },
            { "选择身份角色", "Select Role" },
            { "安全密码", "Password" },
            { "登录密码", "Password" },
            { "登 录", "LOGIN" },
            { "记住密码", "Remember Password" },
            { "密码错误！", "Incorrect Password!" },
            { "用户不存在！", "User does not exist!" },
            { "请输入用户名！", "Please enter username!" },
            { "请输入密码！", "Please enter password!" },
            { "该账号已被禁用，请联系管理员！", "This account has been disabled. Contact admin!" },
            { "系统登录", "System Login" },

            // Motor Report Window & Printing
            { "电机电性能测试报告", "Motor Performance Test Report" },
            { "检测项目", "Test Item" },
            { "实测值", "Measured" },
            { "判定", "Result" },
            { "空载电流(A)", "No-load Current (A)" },
            { "空载转速(r/min)", "No-load Speed (rpm)" },
            { "正转噪音(dB)", "Fwd Noise (dB)" },
            { "反转噪音(dB)", "Rev Noise (dB)" },
            { "负载电流(A)", "Load Current (A)" },
            { "负载转速(r/min)", "Load Speed (rpm)" },
            { "振动值(mm/s)", "Vibration (mm/s)" },
            { "二、空载测试阶段 (A1/A2 工位)", "II. No-load Test Stage (A1/A2)" },
            { "四、噪音测试", "IV. Noise Test" },
            { "四、负载测试阶段 (A5/A6 工位)", "IV. Load Test Stage (A5/A6)" },
            { "五、检验结论", "V. Test Conclusion" },
            { "六、签章确认", "VI. Verification & Signature" },
            { "检验员", "Tester" },
            { "审核员", "Auditor" },
            { "批准人", "Approver" },
            { "报告编号", "Report No." },
            { "报告时间：", "Report Date:" },
            { "产品条码：", "Product Barcode:" },
            { "测试时间：", "Test Date:" },
            { "最终结果：", "Final Result:" },
            { "打印报告", "Print Report" },
            { "导出数据", "Export Data" },
            { "关闭", "Close" },
            { "二、检验结论", "II. Test Conclusion" },
            { "六、阈值判定详情", "VI. Threshold Details" },

            // Extra strings for status dot tooltips & warnings
            { "空载测试", "No-load Test" },
            { "噪音测试", "Noise Test" },
            { "负载测试", "Load Test" },

            // Missing Translations
            { "到", " to " },
            { "条，共", " of " },
            { " 缺陷件数超警戒", " Defects Exceed Warning" },
            { "< 上一页", "< Prev" },
            { "1 未处理", "1 Unresolved" },
            { "A4: 噪音超标", "A4: Noise Exceeded" },
            { "B线视图连接丢失", "B-Line View Connection Lost" },
            { "CAM-01 主测试工位", "CAM-01 Main Station" },
            { "Cam-2 离线", "Cam-2 Offline" },
            { "MotorTestSystem 电机电性能测试系统", "MotorTestSystem Motor Electrical Performance Test System" },
            { "SN-99480-XA1 测量值 78.9dB (限值 65.0)", "SN-99480-XA1 Measured 78.9dB (Limit 65.0)" },
            { "● 阶段 1: 空载测试", "● Phase 1: No-load Test" },
            { "● 阶段 2: 噪音测试", "● Phase 2: Noise Test" },
            { "● 阶段 3: 负载测试", "● Phase 3: Load Test" },
            { "⚠ 故障", "⚠ Fault" },
            { "一、产品基本信息", "I. Basic Product Info" },
            { "七、签章", "VII. Signatures" },
            { "三、空载测试", "III. No-load Test" },
            { "上一页", "Previous" },
            { "上限 (2.5A)", "Upper Limit (2.5A)" },
            { "上限 (70dB)", "Upper Limit (70dB)" },
            { "下一页", "Next" },
            { "下一页 >", "Next >" },
            { "五、负载测试", "V. Load Test" },
            { "反转噪音", "Reverse Noise" },
            { "反转综合(dB)", "Reverse Comb (dB)" },
            { "反转综合噪音", "Reverse Comb Noise" },
            { "台", " units" },
            { "噪音差值", "Noise Delta" },
            { "噪音波形", "Noise Waveform" },
            { "噪音频谱波形", "Noise Spectrum Waveform" },
            { "复制条码", "Copy Barcode" },
            { "实时", "Live" },
            { "已完成", "Completed" },
            { "当班", "Current Shift" },
            { "快捷筛选：", "Quick Filter:" },
            { "扭矩: OK - 合格", "Torque: OK - PASS" },
            { "报警", "Alarm" },
            { "提示内容", "Notification Content" },
            { "搜索", "Search" },
            { "操作", "Action" },
            { "数据实时更新 (每5秒)", "Data live updated (every 5s)" },
            { "最终结果", "Final Result" },
            { "最近检测条码", "Recent Barcodes" },
            { "检测时间", "Test Time" },
            { "检测结果: NG - 需人工检视", "Test Result: NG - Manual Inspection Required" },
            { "正在打印追溯单", "Printing trace slip..." },
            { "正转噪音", "Forward Noise" },
            { "正转综合(dB)", "Forward Comb (dB)" },
            { "正转综合噪音", "Forward Comb Noise" },
            { "测试结果", "Test Result" },
            { "海康威视 01号摄像头 - 主测试工位", "Hikvision Camera 01 - Main Station" },
            { "滚花直径", "Knurling Dia" },
            { "生产监控数据看板", "Production Monitoring Dashboard" },
            { "空载电流", "No-load Current" },
            { "空载电流波形", "No-load Current Waveform" },
            { "空载转速", "No-load Speed" },
            { "第", "Page " },
            { "等待中", "Waiting" },
            { "系统提示", "System Message" },
            { "至", "to" },
            { "角色", "Role" },
            { "角色权限配置", "Role Permissions" },
            { "负载电流", "Load Current" },
            { "负载转速", "Load Speed" },
            { "账号", "Account" },
            { "轴长", "Shaft Length" },
            { "需处理", "Pending" },
            { "（启动冲击 → 稳态波动）", " (Start Inrush -> Steady State)" },
            { "（正转 / 反转）", " (Forward / Reverse)" },
            { "[次]", " times" }
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class LocExtension : MarkupExtension
    {
        [ConstructorArgument("key")]
        public string Key { get; set; } = string.Empty;

        public LocExtension() { }

        public LocExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            // Bind to the LanguageManager indexer: Path=[Key]
            var binding = new Binding($"[{Key}]")
            {
                Source = LanguageManager.Instance,
                Mode = BindingMode.OneWay
            };
            return binding.ProvideValue(serviceProvider);
        }
    }
}
