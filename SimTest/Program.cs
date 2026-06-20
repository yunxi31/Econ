using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MotorTestSystem.Models;
using MotorTestSystem.Services;

namespace SimTest
{
    class Program
    {
        private static bool _modbusCoil100 = true;
        private static bool _mcBit100 = true;
        private static bool _isRunning = true;

        [STAThread]
        static async Task Main(string[] args)
        {
            Console.WriteLine("==================================================");
            Console.WriteLine(" PLC 通信协议集成仿真测试程序开始启动...");
            Console.WriteLine("==================================================");

            // 1. 启动仿真 PLC 服务器
            var modbusServer = StartModbusServer(5020);
            var mcServer = StartMcServer(6000);

            // 稍等以保证端口监听成功
            await Task.Delay(500);

            if (args.Length > 0 && (args[0] == "--server" || args[0] == "server"))
            {
                Console.WriteLine("\n[服务模式] 仿真 PLC 服务器正在运行中...");
                Console.WriteLine("-> Modbus TCP 监听端口: 5020");
                Console.WriteLine("-> Melsec MC 监听端口: 6000");
                Console.WriteLine("-> 输入 'exit' 并按回车退出...");

                while (true)
                {
                    var input = Console.ReadLine();
                    if (input != null && input.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
                    await Task.Delay(200);
                }

                _isRunning = false;
                Console.WriteLine("正在清理并退出服务器...");
                return;
            }

            try
            {
                // 2. 测试 Modbus TCP 通信
                Console.WriteLine("\n[1/2] 开始测试 Modbus TCP 客户端...");
                var modbusConfig = new StationConfig
                {
                    Id = "A1",
                    Name = "测试工位-ModbusTCP",
                    PlcModel = "FX5U",
                    IpAddress = "127.0.0.1",
                    Port = 5020,
                    Protocol = "ModbusTCP",
                    StationId = 1,
                    IsConnected = false,
                    Status = "离线"
                };

                using (var modbusClient = new ModbusTcpClient(modbusConfig))
                {
                    Console.WriteLine("-> 尝试连接 Modbus TCP PLC 服务器 (127.0.0.1:5020)...");
                    bool connected = await modbusClient.ConnectAsync();
                    Console.WriteLine($"-> 连接状态: {(connected ? "成功 (在线)" : "失败")}");

                    if (connected)
                    {
                        Console.WriteLine("-> 读取寄存器快照数据...");
                        var snapshot = await modbusClient.ReadSnapshotAsync();
                        Console.WriteLine($"-> 快照状态: Online={snapshot.IsOnline}, Status={snapshot.Status}, CompletionSignal={snapshot.CompletionSignal}");

                        if (snapshot.CompletionSignal && snapshot.CompletedData != null)
                        {
                            var data = snapshot.CompletedData;
                            Console.WriteLine($"   - 成功读取并解析工位: {data.StationId}");
                            Console.WriteLine($"   - 测试阶段: {data.Stage}");
                            Console.WriteLine($"   - 条码: {data.Barcode}");
                            Console.WriteLine($"   - 电流: {data.NoLoadCurrent} A");
                            Console.WriteLine($"   - 转速: {data.NoLoadSpeed} RPM");
                            Console.WriteLine($"   - 轴长: {data.ShaftLength} mm");
                            Console.WriteLine($"   - 压花外径: {data.KnurlDiameter} mm");
                            Console.WriteLine($"   - 判定结果: {data.Result}");

                            // 验证解析准确性
                            if (data.Barcode == "DES-SR-150GEN8888" &&
                                Math.Abs(data.NoLoadCurrent.Value - 1.820) < 0.001 &&
                                data.NoLoadSpeed == 2050 &&
                                Math.Abs(data.ShaftLength.Value - 32.400) < 0.001 &&
                                Math.Abs(data.KnurlDiameter.Value - 4.420) < 0.001)
                            {
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine("   >> [验证通过] Modbus TCP 数据解析 100% 正确！");
                                Console.ResetColor();
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("   >> [验证失败] Modbus TCP 数据解析有误差或不一致！");
                                Console.ResetColor();
                            }

                            Console.WriteLine("-> 向 PLC 写入复位完成信号...");
                            await modbusClient.ResetCompletionSignalAsync();
                            Console.WriteLine("-> 复位指令已发送。再次读取快照...");

                            var nextSnapshot = await modbusClient.ReadSnapshotAsync();
                            Console.WriteLine($"-> 再次读取快照: CompletionSignal={nextSnapshot.CompletionSignal}");
                            if (!nextSnapshot.CompletionSignal)
                            {
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine("   >> [验证通过] Modbus TCP 完成信号复位闭环正常！");
                                Console.ResetColor();
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("   >> [验证失败] Modbus TCP 完成信号未能成功清零！");
                                Console.ResetColor();
                            }
                        }
                        else
                        {
                            Console.WriteLine("   [错误] 未能读取到有效的完成信号或完成测试数据！");
                        }
                    }
                }

                // 3. 测试 三菱 MC 通信
                Console.WriteLine("\n[2/2] 开始测试 三菱 MC 客户端...");
                var mcConfig = new StationConfig
                {
                    Id = "A3",
                    Name = "测试工位-MelsecMC",
                    PlcModel = "FX5U",
                    IpAddress = "127.0.0.1",
                    Port = 6000,
                    Protocol = "MelsecMC",
                    StationId = 1,
                    IsConnected = false,
                    Status = "离线"
                };

                using (var mcClient = new MelsecMcClient(mcConfig))
                {
                    Console.WriteLine("-> 尝试连接 Mitsubishi MC PLC 服务器 (127.0.0.1:6000)...");
                    bool connected = await mcClient.ConnectAsync();
                    Console.WriteLine($"-> 连接状态: {(connected ? "成功 (在线)" : "失败")}");

                    if (connected)
                    {
                        Console.WriteLine("-> 读取寄存器快照数据...");
                        var snapshot = await mcClient.ReadSnapshotAsync();
                        Console.WriteLine($"-> 快照状态: Online={snapshot.IsOnline}, Status={snapshot.Status}, CompletionSignal={snapshot.CompletionSignal}");

                        if (snapshot.CompletionSignal && snapshot.CompletedData != null)
                        {
                            var data = snapshot.CompletedData;
                            Console.WriteLine($"   - 成功读取并解析工位: {data.StationId}");
                            Console.WriteLine($"   - 测试阶段 (已根据 A3 自动识别为 Noise): {data.Stage}");
                            Console.WriteLine($"   - 条码 (已验证高低字节交换逻辑): {data.Barcode}");
                            Console.WriteLine($"   - 正转噪声 (当前电流映射值): {data.FwdNoise} dB");
                            Console.WriteLine($"   - 反转噪声 (当前转速映射值): {data.RevNoise} dB");
                            Console.WriteLine($"   - 噪声差值: {data.NoiseDiff} dB");
                            Console.WriteLine($"   - 判定结果: {data.Result}");

                            // 验证解析准确性 (MC 包含高低字节交换)
                            if (data.Barcode == "DES-SR-150GEN8888" &&
                                Math.Abs(data.FwdNoise.Value - 1.820) < 0.001 &&
                                Math.Abs(data.RevNoise.Value - 205.0) < 0.001 &&
                                Math.Abs(data.NoiseDiff.Value - 203.18) < 0.001)
                            {
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine("   >> [验证通过] 三菱 MC 协议数据解析与高低字节交换 (Byte Swap) 100% 正确！");
                                Console.ResetColor();
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("   >> [验证失败] 三菱 MC 数据解析不一致！");
                                Console.ResetColor();
                            }

                            Console.WriteLine("-> 向 PLC 写入复位完成信号...");
                            await mcClient.ResetCompletionSignalAsync();
                            Console.WriteLine("-> 复位指令已发送。再次读取快照...");

                            var nextSnapshot = await mcClient.ReadSnapshotAsync();
                            Console.WriteLine($"-> 再次读取快照: CompletionSignal={nextSnapshot.CompletionSignal}");
                            if (!nextSnapshot.CompletionSignal)
                            {
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine("   >> [验证通过] 三菱 MC 完成信号复位闭环正常！");
                                Console.ResetColor();
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("   >> [验证失败] 三菱 MC 完成信号未能成功清零！");
                                Console.ResetColor();
                            }
                        }
                        else
                        {
                            Console.WriteLine("   [错误] 未能读取到有效的完成信号或完成测试数据！");
                        }
                    }
                }

                // 3. 测试 WPF ConfigViewModel 页面连接测试与诊断日志功能
                Console.WriteLine("\n[3/3] 开始测试 WPF ConfigViewModel 页面连接测试与诊断日志功能...");
                
                // 模拟注册工位
                var testConfigs = new System.Collections.ObjectModel.ObservableCollection<StationConfig>
                {
                    new()
                    {
                        Id = "A1",
                        Name = "测试工位-ModbusTCP",
                        PlcModel = "FX5U",
                        IpAddress = "127.0.0.1",
                        Port = 5020,
                        Protocol = "ModbusTCP",
                        StationId = 1,
                        IsConnected = false,
                        Status = "离线"
                    },
                    new()
                    {
                        Id = "A3",
                        Name = "测试工位-MelsecMC",
                        PlcModel = "FX5U",
                        IpAddress = "127.0.0.1",
                        Port = 6000,
                        Protocol = "MelsecMC",
                        StationId = 3,
                        IsConnected = false,
                        Status = "离线"
                    },
                    new()
                    {
                        Id = "A4",
                        Name = "无法连接工位-端口错误",
                        PlcModel = "FX5U",
                        IpAddress = "127.0.0.1",
                        Port = 5021, // 错误端口，应该连接失败
                        Protocol = "ModbusTCP",
                        StationId = 4,
                        IsConnected = false,
                        Status = "离线"
                    }
                };

                var testRepo = new InMemoryMotorTestRepository();
                var testFactory = new PlcClientFactory(useSimulation: false);
                var testRuntime = new BackendRuntime(testConfigs, testRepo, testFactory);
                
                var configViewModel = new MotorTestSystem.ViewModels.ConfigViewModel(testRuntime);
                
                string capturedMessage = "";
                string capturedCaption = "";
                System.Windows.MessageBoxImage capturedImage = default;
                
                // 设置 Mock 回调
                MotorTestSystem.ViewModels.ConfigViewModel.MessageBoxShowMock = (msg, cap, img) =>
                {
                    capturedMessage = msg;
                    capturedCaption = cap;
                    capturedImage = img;
                };

                // 测试 Modbus TCP 连接
                Console.WriteLine("-> 触发 Modbus TCP 工位连接测试 (端口 5020)...");
                var testCmd = configViewModel.TestConnectionCommand;
                await testCmd.ExecuteAsync(testConfigs[0]);
                Console.WriteLine($"   - 连接状态: {testConfigs[0].Status}, IsConnected: {testConfigs[0].IsConnected}");
                Console.WriteLine($"   - 弹窗标题: {capturedCaption}, 消息: {capturedMessage}");
                
                if (testConfigs[0].IsConnected && testConfigs[0].Status == "在线" && capturedCaption == "连接测试成功")
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("   >> [验证通过] ConfigViewModel 成功连接到 Modbus TCP 仿真服务器并正确更新状态！");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("   >> [验证失败] ConfigViewModel 未能正确连接到 Modbus TCP 仿真服务器或状态未更新！");
                    Console.ResetColor();
                }

                // 测试 Melsec MC 连接
                Console.WriteLine("\n-> 触发 Melsec MC 工位连接测试 (端口 6000)...");
                capturedMessage = "";
                capturedCaption = "";
                await testCmd.ExecuteAsync(testConfigs[1]);
                Console.WriteLine($"   - 连接状态: {testConfigs[1].Status}, IsConnected: {testConfigs[1].IsConnected}");
                Console.WriteLine($"   - 弹窗标题: {capturedCaption}, 消息: {capturedMessage}");

                if (testConfigs[1].IsConnected && testConfigs[1].Status == "在线" && capturedCaption == "连接测试成功")
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("   >> [验证通过] ConfigViewModel 成功连接到 Melsec MC 仿真服务器并正确更新状态！");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("   >> [验证失败] ConfigViewModel 未能正确连接到 Melsec MC 仿真服务器或状态未更新！");
                    Console.ResetColor();
                }

                // 测试失败连接
                Console.WriteLine("\n-> 触发异常工位连接测试 (端口 5021, 应该失败)...");
                capturedMessage = "";
                capturedCaption = "";
                await testCmd.ExecuteAsync(testConfigs[2]);
                Console.WriteLine($"   - 连接状态: {testConfigs[2].Status}, IsConnected: {testConfigs[2].IsConnected}");
                Console.WriteLine($"   - 弹窗标题: {capturedCaption}, 消息: {capturedMessage}");

                if (!testConfigs[2].IsConnected && testConfigs[2].Status == "离线" && capturedCaption == "连接测试失败")
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("   >> [验证通过] ConfigViewModel 对无法连接的工位正确更新为离线并弹出失败提示！");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("   >> [验证失败] ConfigViewModel 未能正确处理连接失败的情况！");
                    Console.ResetColor();
                }

                // 验证诊断日志生成
                Console.WriteLine("\n-> 检查 ConfigViewModel 诊断日志列表...");
                foreach (var log in configViewModel.DiagnosticLogs)
                {
                    Console.WriteLine($"   [{log.Timestamp}] {log.Level} {log.Message}");
                }

                var connectionLogs = configViewModel.DiagnosticLogs.Where(l => l.Level == "[连接]").ToList();
                var errorLogs = configViewModel.DiagnosticLogs.Where(l => l.Level == "[错误]").ToList();
                
                if (connectionLogs.Count >= 2 && errorLogs.Count >= 1)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("   >> [验证通过] ConfigViewModel 诊断日志生成正常，包含成功与错误日志！");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("   >> [验证失败] ConfigViewModel 诊断日志条目数不符合预期！");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"测试过程中发生异常: {ex.Message}\n{ex.StackTrace}");
                Console.ResetColor();
            }
            finally
            {
                _isRunning = false;
                Console.WriteLine("\n测试完成。正在清理服务器并退出...");
            }
        }

        #region Modbus TCP 仿真服务端
        private static Task StartModbusServer(int port)
        {
            return Task.Run(async () =>
            {
                TcpListener listener;
                try
                {
                    listener = new TcpListener(IPAddress.Loopback, port);
                    listener.Start();
                }
                catch (SocketException)
                {
                    Console.WriteLine($"[提示] Modbus TCP 端口 {port} 已被占用，假设仿真服务端已在后台运行。");
                    return;
                }
                try
                {
                    while (_isRunning)
                    {
                        if (!listener.Pending())
                        {
                            await Task.Delay(100);
                            continue;
                        }

                        using var client = await listener.AcceptTcpClientAsync();
                        using var stream = client.GetStream();
                        var buffer = new byte[1024];

                        while (_isRunning && client.Connected)
                        {
                            // 读取 7 字节 MBAP 头
                            int bytesRead = 0;
                            try
                            {
                                while (bytesRead < 7)
                                {
                                    int n = await stream.ReadAsync(buffer, bytesRead, 7 - bytesRead);
                                    if (n <= 0) break;
                                    bytesRead += n;
                                }
                            }
                            catch { break; }

                            if (bytesRead < 7) break;

                            ushort txId = (ushort)((buffer[0] << 8) | buffer[1]);
                            ushort protocolId = (ushort)((buffer[2] << 8) | buffer[3]);
                            ushort remainingLength = (ushort)((buffer[4] << 8) | buffer[5]);

                            // 读取 Payload (Unit ID 已在头中读取，因此还剩 remainingLength - 1 字节)
                            int payloadRead = 0;
                            int bytesToRead = remainingLength - 1;
                            try
                            {
                                while (payloadRead < bytesToRead)
                                {
                                    int n = await stream.ReadAsync(buffer, 7 + payloadRead, bytesToRead - payloadRead);
                                    if (n <= 0) break;
                                    payloadRead += n;
                                }
                            }
                            catch { break; }

                            if (payloadRead < bytesToRead) break;

                            byte unitId = buffer[6];
                            byte functionCode = buffer[7];

                            if (functionCode == 0x01) // Read Coils
                            {
                                ushort startAddr = (ushort)((buffer[8] << 8) | buffer[9]);
                                ushort qty = (ushort)((buffer[10] << 8) | buffer[11]);

                                // 构造响应
                                byte[] resp = new byte[10];
                                resp[0] = (byte)(txId >> 8); resp[1] = (byte)(txId & 0xFF); // Tx ID
                                resp[2] = 0; resp[3] = 0; // Proto ID
                                resp[4] = 0; resp[5] = 4; // Length (Unit ID + FC + ByteCount + Data)
                                resp[6] = unitId;
                                resp[7] = 0x01; // FC
                                resp[8] = 1; // Byte Count
                                resp[9] = (byte)(_modbusCoil100 ? 0x01 : 0x00); // M100

                                await stream.WriteAsync(resp, 0, resp.Length);
                            }
                            else if (functionCode == 0x03) // Read Holding Registers
                            {
                                ushort startAddr = (ushort)((buffer[8] << 8) | buffer[9]);
                                ushort qty = (ushort)((buffer[10] << 8) | buffer[11]);

                                byte[] regData = new byte[qty * 2];
                                if (startAddr == 100 && qty >= 110)
                                {
                                    // D100 = 1820 (0x071C)
                                    regData[0] = 0x07; regData[1] = 0x1C;
                                    // D102 = 2050 (0x0802)
                                    regData[4] = 0x08; regData[5] = 0x02;
                                    // D104 = 32400 (0x7E90)
                                    regData[8] = 0x7E; regData[9] = 0x90;
                                    // D106 = 4420 (0x1144)
                                    regData[12] = 0x11; regData[13] = 0x44;

                                    // D200 (offset 200 bytes) = "DES-SR-150GEN8888\0\0\0"
                                    byte[] barcodeBytes = Encoding.ASCII.GetBytes("DES-SR-150GEN8888\0\0\0");
                                    Buffer.BlockCopy(barcodeBytes, 0, regData, 200, Math.Min(barcodeBytes.Length, regData.Length - 200));
                                }

                                byte[] resp = new byte[9 + regData.Length];
                                resp[0] = (byte)(txId >> 8); resp[1] = (byte)(txId & 0xFF);
                                resp[2] = 0; resp[3] = 0;
                                ushort len = (ushort)(3 + regData.Length);
                                resp[4] = (byte)(len >> 8); resp[5] = (byte)(len & 0xFF);
                                resp[6] = unitId;
                                resp[7] = 0x03;
                                resp[8] = (byte)regData.Length;
                                Buffer.BlockCopy(regData, 0, resp, 9, regData.Length);

                                await stream.WriteAsync(resp, 0, resp.Length);
                            }
                            else if (functionCode == 0x05) // Write Coil
                            {
                                ushort startAddr = (ushort)((buffer[8] << 8) | buffer[9]);
                                ushort val = (ushort)((buffer[10] << 8) | buffer[11]);

                                if (startAddr == 100)
                                {
                                    _modbusCoil100 = (val == 0xFF00);
                                }

                                // Echo response (12 bytes)
                                byte[] resp = new byte[12];
                                Buffer.BlockCopy(buffer, 0, resp, 0, 12);
                                // length on index 4, 5 is 6
                                resp[4] = 0; resp[5] = 6;

                                await stream.WriteAsync(resp, 0, resp.Length);
                            }
                        }
                    }
                }
                catch { }
                finally
                {
                    listener.Stop();
                }
            });
        }
        #endregion

        #region Mitsubishi MC 仿真服务端
        private static Task StartMcServer(int port)
        {
            return Task.Run(async () =>
            {
                TcpListener listener;
                try
                {
                    listener = new TcpListener(IPAddress.Loopback, port);
                    listener.Start();
                }
                catch (SocketException)
                {
                    Console.WriteLine($"[提示] Mitsubishi MC 端口 {port} 已被占用，假设仿真服务端已在后台运行。");
                    return;
                }
                try
                {
                    while (_isRunning)
                    {
                        if (!listener.Pending())
                        {
                            await Task.Delay(100);
                            continue;
                        }

                        using var client = await listener.AcceptTcpClientAsync();
                        using var stream = client.GetStream();
                        var buffer = new byte[1024];

                        while (_isRunning && client.Connected)
                        {
                            // 读取 11 字节 MC 请求头 (前 11 字节包含 subheader, network, pc, io, station, length)
                            int bytesRead = 0;
                            try
                            {
                                while (bytesRead < 11)
                                {
                                    int n = await stream.ReadAsync(buffer, bytesRead, 11 - bytesRead);
                                    if (n <= 0) break;
                                    bytesRead += n;
                                }
                            }
                            catch { break; }

                            if (bytesRead < 11) break;

                            // verify subheader
                            if (buffer[0] != 0x50 || buffer[1] != 0x00) break;

                            ushort requestLength = BitConverter.ToUInt16(buffer, 7);

                            // 读取 Payload (CPU监控定时器2字节已经在11字节头中读取，因此还剩 requestLength - 2 字节)
                            int payloadRead = 0;
                            int bytesToRead = requestLength - 2;
                            try
                            {
                                while (payloadRead < bytesToRead)
                                {
                                    int n = await stream.ReadAsync(buffer, 11 + payloadRead, bytesToRead - payloadRead);
                                    if (n <= 0) break;
                                    payloadRead += n;
                                }
                            }
                            catch { break; }

                            if (payloadRead < bytesToRead) break;

                            ushort command = BitConverter.ToUInt16(buffer, 11);
                            ushort subcommand = BitConverter.ToUInt16(buffer, 13);
                            
                            // 24-bit address is buffer[15], buffer[16], buffer[17]
                            int startAddr = buffer[15] | (buffer[16] << 8) | (buffer[17] << 16);
                            byte deviceCode = buffer[18];
                            ushort count = BitConverter.ToUInt16(buffer, 19);

                            if (command == 0x0401) // Batch Read
                            {
                                if (subcommand == 0x0001) // Bit Read (represented by 01 00 subcommand on client side, wait, client subheader: frame[13] = isBitUnit ? 0x01 : 0x00, frame[14] = 0x00 -> so subcommand is 0x0001)
                                {
                                    // Bit Read M100
                                    // Send D0 00, Network 00, PC FF, I/O FF 03, Station 00, Length 03 00, EndCode 00 00, Data 0x10 (True) or 0x00 (False)
                                    byte[] resp = new byte[12];
                                    resp[0] = 0xD0; resp[1] = 0x00;
                                    resp[2] = 0x00; resp[3] = 0xFF;
                                    resp[4] = 0xFF; resp[5] = 0x03;
                                    resp[6] = 0x00;
                                    resp[7] = 0x03; resp[8] = 0x00; // Remaining length: 2 (EndCode) + 1 (Data) = 3
                                    resp[9] = 0x00; resp[10] = 0x00; // EndCode
                                    resp[11] = (byte)(_mcBit100 ? 0x10 : 0x00); // Bit value (nibble high represents first bit)

                                    await stream.WriteAsync(resp, 0, resp.Length);
                                }
                                else if (subcommand == 0x0000) // Word Read
                                {
                                    // Word Read D100, count 110
                                    int dataByteLen = count * 2;
                                    byte[] data = new byte[dataByteLen];

                                    if (deviceCode == 0xA8 && startAddr == 100 && count >= 110)
                                    {
                                        // D100 = 1820 (0x071C) -> little endian
                                        data[0] = 0x1C; data[1] = 0x07;
                                        // D102 = 2050 (0x0802) -> little endian
                                        data[4] = 0x02; data[5] = 0x08;
                                        // D104 = 32400 (0x7E90) -> little endian
                                        data[8] = 0x90; data[9] = 0x7E;
                                        // D106 = 4420 (0x1144) -> little endian
                                        data[12] = 0x44; data[13] = 0x11;

                                        // D200 (offset 200 bytes) = "DES-SR-150GEN8888\0\0\0"
                                        // Need byte swap for MC protocol client word string reading!
                                        // String characters in order: D, E, S, -, S, R, -, 1, 5, 0, G, E, N, 8, 8, 8, 8, \0, \0, \0
                                        // Since the client swaps pairs of bytes:
                                        // chars[i] = data[offset + i + 1], chars[i+1] = data[offset + i]
                                        // So we write: data[offset] = second_char, data[offset+1] = first_char
                                        byte[] barcodeRaw = Encoding.ASCII.GetBytes("DES-SR-150GEN8888\0\0\0");
                                        for (int i = 0; i < barcodeRaw.Length; i += 2)
                                        {
                                            if (i + 1 < barcodeRaw.Length)
                                            {
                                                data[200 + i] = barcodeRaw[i + 1]; // low byte is second char
                                                data[200 + i + 1] = barcodeRaw[i]; // high byte is first char
                                            }
                                        }
                                    }

                                    byte[] resp = new byte[11 + dataByteLen];
                                    resp[0] = 0xD0; resp[1] = 0x00;
                                    resp[2] = 0x00; resp[3] = 0xFF;
                                    resp[4] = 0xFF; resp[5] = 0x03;
                                    resp[6] = 0x00;
                                    ushort remLen = (ushort)(2 + dataByteLen);
                                    resp[7] = (byte)(remLen & 0xFF); resp[8] = (byte)((remLen >> 8) & 0xFF);
                                    resp[9] = 0x00; resp[10] = 0x00; // EndCode
                                    Buffer.BlockCopy(data, 0, resp, 11, dataByteLen);

                                    await stream.WriteAsync(resp, 0, resp.Length);
                                }
                            }
                            else if (command == 0x1401) // Batch Write
                            {
                                if (subcommand == 0x0001) // Bit Write
                                {
                                    byte writeVal = buffer[21]; // data byte (0x10 or 0x00)
                                    if (startAddr == 100)
                                    {
                                        _mcBit100 = (writeVal == 0x10);
                                    }

                                    // Send D0 00, Network 00, PC FF, I/O FF 03, Station 00, Length 02 00, EndCode 00 00
                                    byte[] resp = new byte[11];
                                    resp[0] = 0xD0; resp[1] = 0x00;
                                    resp[2] = 0x00; resp[3] = 0xFF;
                                    resp[4] = 0xFF; resp[5] = 0x03;
                                    resp[6] = 0x00;
                                    resp[7] = 0x02; resp[8] = 0x00; // Remaining length: 2 (EndCode only)
                                    resp[9] = 0x00; resp[10] = 0x00; // EndCode

                                    await stream.WriteAsync(resp, 0, resp.Length);
                                }
                            }
                        }
                    }
                }
                catch { }
                finally
                {
                    listener.Stop();
                }
            });
        }
        #endregion
    }
}
