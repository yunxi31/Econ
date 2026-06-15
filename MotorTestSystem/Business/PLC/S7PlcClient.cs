using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MotorTestSystem.Models;
using S7.Net;
using S7.Net.Types;

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
        private IS7Plc? _plc;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private bool _isDisposed;
        private readonly Func<CpuType, string, int, short, short, IS7Plc> _plcFactory;

        public S7PlcClient(StationConfig config) : this(config, (cpu, ip, port, rack, slot) => new S7PlcWrapper(cpu, ip, port, rack, slot))
        {
        }

        public S7PlcClient(StationConfig config, Func<CpuType, string, int, short, short, IS7Plc> plcFactory)
        {
            Config = config;
            _plcFactory = plcFactory;
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
                _plc = _plcFactory(cpuType, Config.IpAddress, Config.Port, (short)0, (short)Config.StationId);

                // 2 秒超时 — S7.Net OpenAsync 不接受 CancellationToken，用 Task.WhenAny 模拟
                var openTask = _plc.OpenAsync(cancellationToken);
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                if (await Task.WhenAny(openTask, timeoutTask) != openTask)
                {
                    // 超时：关闭连接并返回
                    CloseConnection();
                    return false;
                }

                // OpenAsync 已完成，检查是否有异常
                try
                {
                    await openTask; // 传播异常
                }
                catch
                {
                    CloseConnection();
                    return false;
                }

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
                // Batch read all items to achieve 1 TCP roundtrip instead of 3
                var completionItem = new DataItem
                {
                    DataType = DataType.Memory,
                    VarType = VarType.Bit,
                    DB = 0,
                    StartByteAdr = 100,
                    BitAdr = 0,
                    Count = 1
                };
                var wordsItem = new DataItem
                {
                    DataType = DataType.DataBlock,
                    VarType = VarType.Word,
                    DB = 1,
                    StartByteAdr = 100,
                    Count = 4
                };
                var barcodeItem = new DataItem
                {
                    DataType = DataType.DataBlock,
                    VarType = VarType.String,
                    DB = 1,
                    StartByteAdr = 200,
                    Count = 20
                };

                var items = new List<DataItem> { completionItem, wordsItem, barcodeItem };
                await _plc!.ReadMultipleVarsAsync(items, cancellationToken);

                bool completionSignal = completionItem.Value is bool b && b;

                StageTestData? completedData = null;
                if (completionSignal)
                {
                    short rawCurrent = 0;
                    short speed = 0;
                    short rawLength = 0;
                    short rawDiameter = 0;

                    if (wordsItem.Value is ushort[] words && words.Length >= 4)
                    {
                        rawCurrent = (short)words[0];
                        speed = (short)words[1];
                        rawLength = (short)words[2];
                        rawDiameter = (short)words[3];
                    }

                    double current = Math.Round(rawCurrent / 1000.0, 3);
                    double shaftLength = Math.Round(rawLength / 1000.0, 3);
                    double knurlDiameter = Math.Round(rawDiameter / 1000.0, 3);

                    string barcode = barcodeItem.Value as string ?? "UNKNOWN_BARCODE";

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
                        CollectedAt = System.DateTime.Now,
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
                await _plc!.WriteAsync(DataType.Memory, 0, 100, false, 0, cancellationToken);
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
