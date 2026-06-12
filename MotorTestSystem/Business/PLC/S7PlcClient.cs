using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MotorTestSystem.Models;
using S7.Net;

namespace MotorTestSystem.Services
{
    /// <summary>
    /// 西门子 S7 协议 (TCP) 通信客户端
    /// 支持 S7-1200 / S7-1500 等 PLC
    /// 使用 S7netPlus 库实现
    ///
    /// PLC 地址映射（与 ModbusTcpClient / MelsecMcClient 对齐）：
    ///   完成信号:  M100.0         (M 区位地址)
    ///   测试数据:  DB1.DBW100 起  (数据块 DB1，字地址从 100 开始)
    ///     DB1.DBW100  — 电流   (Int16, /1000 → A)
    ///     DB1.DBW102  — 转速   (Int16, r/min)
    ///     DB1.DBW104  — 轴长   (Int16, /1000 → mm)
    ///     DB1.DBW106  — 滚花直径 (Int16, /1000 → mm)
    ///     DB1.DBD200  — 条码   (String[20], 22 字节含头)
    /// </summary>
    public sealed class S7PlcClient : IPlcClient
    {
        private Plc? _plc;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private bool _isDisposed;

        public S7PlcClient(StationConfig config)
        {
            Config = config;
        }

        public StationConfig Config { get; }

        public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_plc is { IsConnected: true })
            {
                return true;
            }

            await _lock.WaitAsync(cancellationToken);
            try
            {
                if (_plc is { IsConnected: true })
                {
                    return true;
                }

                CloseConnection();

                var cpuType = ResolveCpuType(Config.PlcModel);
                _plc = new Plc(cpuType, Config.IpAddress, Config.Port, (short)0, (short)Config.StationId);

                // 2 秒超时
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(2));

                await _plc.OpenAsync();
                return _plc.IsConnected;
            }
            catch
            {
                CloseConnection();
                return false;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<StationSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default)
        {
            if (!await ConnectAsync(cancellationToken))
            {
                return new StationSnapshot
                {
                    StationId = Config.Id,
                    IsOnline = false,
                    Status = 2,
                    CompletionSignal = false
                };
            }

            await _lock.WaitAsync(cancellationToken);
            try
            {
                // 1. 读取完成信号 M100.0
                var completionObj = await _plc!.ReadAsync(DataType.Memory, 0, 100, VarType.Bit, 1, 0);
                bool completionSignal = completionObj is bool b && b;

                StageTestData? completedData = null;
                if (completionSignal)
                {
                    // 2. 读取测试数据 DB1.DBW100 起
                    //    一次性读取 220 字节 (DBW100 ~ DBW209 + 条码区域)
                    //    条码在 DB1.DBD200，S7 String 格式含 2 字节头 + 最多 254 字节数据
                    //    为了兼容 S7netPlus 的 ReadClass / ReadString，采用分段读取

                    // 2a. 读数值区域: DB1.DBW100, 共 8 字节 (4 个 Int16)
                    var rawWords = await _plc.ReadAsync(DataType.DataBlock, 1, 100, VarType.Word, 4);

                    short rawCurrent = 0;
                    short speed = 0;
                    short rawLength = 0;
                    short rawDiameter = 0;

                    if (rawWords is ushort[] words && words.Length >= 4)
                    {
                        rawCurrent = (short)words[0];
                        speed = (short)words[1];
                        rawLength = (short)words[2];
                        rawDiameter = (short)words[3];
                    }

                    double current = Math.Round(rawCurrent / 1000.0, 3);
                    double shaftLength = Math.Round(rawLength / 1000.0, 3);
                    double knurlDiameter = Math.Round(rawDiameter / 1000.0, 3);

                    // 2b. 读条码: DB1.DBD200, S7 String[20]
                    //     S7 String 头: byte[0]=最大长度, byte[1]=实际长度, 后跟字符
                    string barcode;
                    try
                    {
                        barcode = (string?)await _plc.ReadAsync(DataType.DataBlock, 1, 200, VarType.String, 20) ?? "UNKNOWN_BARCODE";
                    }
                    catch
                    {
                        // 降级: 手动读 22 字节并解析
                        barcode = await ReadBarcodeRawAsync();
                    }

                    if (string.IsNullOrWhiteSpace(barcode))
                        barcode = "UNKNOWN_BARCODE";

                    // 3. 判定阶段与结果
                    TestStage stage = ResolveStage(Config.Id);
                    string result = stage switch
                    {
                        TestStage.NoLoad => (current > 2.3 || knurlDiameter > 4.65) ? "NG" : "OK",
                        TestStage.Noise => (current > 70.0) ? "NG" : "OK",
                        TestStage.Load => (current > 3.2) ? "NG" : "OK",
                        _ => "OK"
                    };

                    completedData = new StageTestData
                    {
                        Barcode = barcode,
                        StationId = Config.Id,
                        Stage = stage,
                        CollectedAt = DateTime.Now,
                        Result = result
                    };

                    // 4. 按阶段填充字段
                    if (stage == TestStage.NoLoad)
                    {
                        completedData.NoLoadCurrent = current;
                        completedData.NoLoadSpeed = speed;
                        completedData.ShaftLength = shaftLength;
                        completedData.KnurlDiameter = knurlDiameter;
                    }
                    else if (stage == TestStage.Noise)
                    {
                        completedData.FwdNoise = current;
                        completedData.RevNoise = speed / 10.0;
                        completedData.NoiseDiff = Math.Round(Math.Abs(completedData.FwdNoise.Value - completedData.RevNoise.Value), 2);
                        completedData.Result = (completedData.NoiseDiff > 10.0 || completedData.FwdNoise > 70.0) ? "NG" : "OK";
                    }
                    else if (stage == TestStage.Load)
                    {
                        completedData.LoadCurrent = current;
                        completedData.LoadSpeed = speed;
                    }
                }

                return new StationSnapshot
                {
                    StationId = Config.Id,
                    IsOnline = true,
                    Status = completionSignal ? 1 : 0,
                    CompletionSignal = completionSignal,
                    CompletedData = completedData
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[S7PlcClient Error] ReadSnapshotAsync failed: {ex.Message}\n{ex.StackTrace}");
                CloseConnection();
                return new StationSnapshot
                {
                    StationId = Config.Id,
                    IsOnline = false,
                    Status = 2,
                    CompletionSignal = false
                };
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task ResetCompletionSignalAsync(CancellationToken cancellationToken = default)
        {
            if (!await ConnectAsync(cancellationToken))
            {
                return;
            }

            await _lock.WaitAsync(cancellationToken);
            try
            {
                // M100.0 = False
                await _plc!.WriteAsync(DataType.Memory, 0, 100, false, 0);
            }
            catch
            {
                CloseConnection();
            }
            finally
            {
                _lock.Release();
            }
        }

        // ===== 辅助方法 =====

        /// <summary>
        /// 根据配置中的 PLC 型号字符串解析为 S7netPlus 的 CpuType 枚举
        /// </summary>
        private static CpuType ResolveCpuType(string plcModel)
        {
            return plcModel.ToUpperInvariant() switch
            {
                "S7-1200" => CpuType.S71200,
                "S7-1500" => CpuType.S71500,
                "S7-300" => CpuType.S7300,
                "S7-400" => CpuType.S7400,
                "S7-200" or "S7-200SMART" => CpuType.S7200,
                _ => CpuType.S71200 // 默认按 S7-1200 处理
            };
        }

        /// <summary>
        /// 降级方式读取条码: 手动读取 DB1.DBW200 起始的 22 字节并解析 S7 String 格式
        /// </summary>
        private async Task<string> ReadBarcodeRawAsync()
        {
            try
            {
                // S7 String[20] 在 PLC 中占用 22 字节: byte[0]=maxLen, byte[1]=actualLen, byte[2..21]=chars
                var rawBytesObj = await _plc!.ReadAsync(DataType.DataBlock, 1, 200, VarType.Byte, 22);
                var rawBytes = rawBytesObj as byte[];

                if (rawBytes == null || rawBytes.Length < 2)
                    return "ERR_READ";

                int actualLen = rawBytes[1];
                if (actualLen <= 0 || actualLen > 20)
                    return "ERR_LEN";

                string result = Encoding.ASCII.GetString(rawBytes, 2, actualLen).Trim('\0', ' ', '\r', '\n');
                return string.IsNullOrEmpty(result) ? "UNKNOWN_BARCODE" : result;
            }
            catch
            {
                return "ERR_READ";
            }
        }

        private static TestStage ResolveStage(string stationId)
        {
            return stationId switch
            {
                "A1" or "A2" => TestStage.NoLoad,
                "A3" or "A4" => TestStage.Noise,
                "A5" or "A6" => TestStage.Load,
                _ => TestStage.NoLoad
            };
        }

        private void CloseConnection()
        {
            try
            {
                if (_plc is { IsConnected: true })
                {
                    _plc.Close();
                }
            }
            catch
            {
                // 忽略关闭异常
            }

            _plc = null;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            CloseConnection();
            _lock.Dispose();
        }
    }
}
