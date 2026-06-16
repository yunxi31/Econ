using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MotorTestSystem.Models;
using MotorTestSystem.Models.Entities;
using MotorTestSystem.Services;
using SqlSugar;
using Xunit;

namespace MotorTestSystem.Tests.PropertyTests
{
    /// <summary>
    /// Property 1: 并发 Upsert 操作的最终一致性
    /// Validates: Requirements 1.1, 1.2
    /// 模拟实际生产环境（6 工位并发写入），验证最终只有一条记录存在。
    /// </summary>
    public class ConcurrentUpsertTests : IDisposable
    {
        private readonly string _dbDir;

        public ConcurrentUpsertTests()
        {
            _dbDir = Path.Combine(Path.GetTempPath(), $"ConcurrentUpsert_Test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_dbDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_dbDir))
            {
                try { Directory.Delete(_dbDir, true); }
                catch { /* cleanup best-effort */ }
            }
        }

        /// <summary>
        /// 安全删除 SQLite 文件（含辅助文件），带重试机制。
        /// </summary>
        private static void SafeDeleteDbFile(string dbPath)
        {
            if (!File.Exists(dbPath)) return;

            foreach (var suffix in new[] { "-wal", "-shm", "-journal" })
            {
                var extra = dbPath + suffix;
                if (File.Exists(extra))
                    try { File.Delete(extra); } catch { }
            }

            try { File.Delete(dbPath); }
            catch (IOException)
            {
                // 如无法删除则跳过，由 Dispose 统一清理
            }
        }

        /// <summary>
        /// 模拟 6 个工位对同一 Barcode 并发执行 Upsert，
        /// 验证最终只有一条记录存在。
        /// 每个工位使用独立连接，模拟实际生产模式。
        /// 测试 20 次迭代。
        /// </summary>
        [Fact]
        public async Task ConcurrentUpsert_SameBarcode_ShouldProduceExactlyOneRecord()
        {
            var rng = new Random(42);

            for (int iteration = 0; iteration < 20; iteration++)
            {
                string dbPath = Path.Combine(_dbDir, $"Test_{iteration}.db");

                // 设置 WAL 模式以支持并发读写
                using var setupDb = new SqlSugarClient(new ConnectionConfig
                {
                    ConnectionString = $"Data Source={dbPath};Pooling=true;",
                    DbType = DbType.Sqlite,
                    IsAutoCloseConnection = true,
                    InitKeyType = InitKeyType.Attribute,
                });
                setupDb.CodeFirst.InitTables(typeof(MotorTestRecordEntity));
                setupDb.Ado.ExecuteCommand("PRAGMA journal_mode=WAL;");
                setupDb.Ado.ExecuteCommand("PRAGMA busy_timeout=5000;");
                setupDb.Close();

                string barcode = $"SN-CONC-{iteration}-{rng.Next(10000, 99999)}";
                int stationCount = 6; // 模拟 6 工位

                var tasks = new List<Task>();

                for (int station = 0; station < stationCount; station++)
                {
                    var stage = (TestStage)(station % 3);
                    var data = new StageTestData
                    {
                        Barcode = barcode,
                        StationId = $"A{station + 1}",
                        Stage = stage,
                        CollectedAt = DateTime.UtcNow,
                        Result = rng.Next(0, 3) == 0 ? "NG" : "OK",
                        NoLoadCurrent = stage == TestStage.NoLoad ? Math.Round(rng.NextDouble() * 5, 3) : null,
                        NoLoadSpeed = stage == TestStage.NoLoad ? rng.Next(2500, 3200) : null,
                        FwdNoise = stage == TestStage.Noise ? Math.Round(rng.NextDouble() * 100, 2) : null,
                        RevNoise = stage == TestStage.Noise ? Math.Round(rng.NextDouble() * 100, 2) : null,
                        LoadCurrent = stage == TestStage.Load ? Math.Round(rng.NextDouble() * 6, 3) : null,
                        LoadSpeed = stage == TestStage.Load ? rng.Next(1000, 3200) : null,
                    };

                    tasks.Add(Task.Run(async () =>
                    {
                        // 每个工位使用独立连接
                        using var db = new SqlSugarClient(new ConnectionConfig
                        {
                            ConnectionString = $"Data Source={dbPath};Pooling=true;",
                            DbType = DbType.Sqlite,
                            IsAutoCloseConnection = true,
                            InitKeyType = InitKeyType.Attribute,
                        });

                        // 重试机制处理 SQLite 锁定
                        const int maxRetries = 5;
                        for (int retry = 0; retry < maxRetries; retry++)
                        {
                            try
                            {
                                await db.Ado.UseTranAsync(async () =>
                                {
                                    var existing = await db.Queryable<MotorTestRecordEntity>()
                                        .FirstAsync(r => r.Barcode == barcode);

                                    if (existing == null)
                                    {
                                        existing = new MotorTestRecordEntity
                                        {
                                            Barcode = barcode,
                                            TestTime = data.CollectedAt,
                                            FinalResult = "NG"
                                        };
                                        ApplyTestStage(existing, data);
                                        await db.Insertable(existing).ExecuteCommandAsync();
                                    }
                                    else
                                    {
                                        ApplyTestStage(existing, data);
                                        existing.TestTime = data.CollectedAt;
                                        existing.FinalResult = "OK";
                                        await db.Updateable(existing).ExecuteCommandAsync();
                                    }
                                });
                                return;
                            }
                            catch (Exception ex) when (
                                ex.Message.Contains("locked") ||
                                ex.Message.Contains("database is locked") ||
                                ex.InnerException?.Message?.Contains("locked") == true)
                            {
                                if (retry == maxRetries - 1) throw;
                                await Task.Delay(100 * (retry + 1));
                            }
                        }
                    }));
                }

                await Task.WhenAll(tasks);

                // Assert: 验证只有一条记录存在
                using var verifyDb = new SqlSugarClient(new ConnectionConfig
                {
                    ConnectionString = $"Data Source={dbPath};",
                    DbType = DbType.Sqlite,
                    IsAutoCloseConnection = true,
                    InitKeyType = InitKeyType.Attribute,
                });

                var records = await verifyDb.Queryable<MotorTestRecordEntity>()
                    .Where(r => r.Barcode == barcode)
                    .ToListAsync();

                Assert.True(
                    records.Count == 1,
                    $"Iteration {iteration}: Expected exactly 1 record for barcode '{barcode}', " +
                    $"but found {records.Count} (stations: {stationCount})"
                );

                verifyDb.Close();

                // 清理
                if (File.Exists(dbPath))
                {
                    try { File.Delete(dbPath); }
                    catch { /* cleanup best-effort */ }
                }
            }
        }

        /// <summary>
        /// 验证不同 Barcode 的并发写入不会相互干扰。
        /// 模拟 30 个独立 Barcode 同时写入。
        /// </summary>
        [Fact]
        public async Task ConcurrentUpsert_DifferentBarcodes_ShouldCreateAllRecords()
        {
            string dbPath = Path.Combine(_dbDir, "DifferentTest.db");

            // 设置 WAL 模式
            using (var setupDb = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={dbPath};Pooling=true;",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute,
            }))
            {
                setupDb.CodeFirst.InitTables(typeof(MotorTestRecordEntity));
                setupDb.Ado.ExecuteCommand("PRAGMA journal_mode=WAL;");
                setupDb.Ado.ExecuteCommand("PRAGMA busy_timeout=5000;");
                setupDb.Close();
            }

            var rng = new Random(42);
            int barcodeCount = 30;
            var tasks = new List<Task>();

            for (int i = 0; i < barcodeCount; i++)
            {
                string barcode = $"SN-DIFF-{i:D4}";
                tasks.Add(Task.Run(async () =>
                {
                    using var db = new SqlSugarClient(new ConnectionConfig
                    {
                        ConnectionString = $"Data Source={dbPath};Pooling=true;",
                        DbType = DbType.Sqlite,
                        IsAutoCloseConnection = true,
                        InitKeyType = InitKeyType.Attribute,
                    });

                    var data = new StageTestData
                    {
                        Barcode = barcode,
                        StationId = $"A{rng.Next(1, 7)}",
                        Stage = TestStage.NoLoad,
                        CollectedAt = DateTime.UtcNow,
                        Result = "OK",
                        NoLoadCurrent = Math.Round(rng.NextDouble() * 5, 3),
                        NoLoadSpeed = rng.Next(2500, 3200),
                    };

                    const int maxRetries = 5;
                    for (int retry = 0; retry < maxRetries; retry++)
                    {
                        try
                        {
                            await db.Ado.UseTranAsync(async () =>
                            {
                                var existing = await db.Queryable<MotorTestRecordEntity>()
                                    .FirstAsync(r => r.Barcode == barcode);

                                if (existing == null)
                                {
                                    existing = new MotorTestRecordEntity
                                    {
                                        Barcode = barcode,
                                        TestTime = data.CollectedAt,
                                        FinalResult = "OK",
                                        NoLoadCurrent = data.NoLoadCurrent,
                                        NoLoadSpeed = data.NoLoadSpeed,
                                        NoLoadResult = "OK"
                                    };
                                    await db.Insertable(existing).ExecuteCommandAsync();
                                }
                            });
                            return;
                        }
                        catch (Exception ex) when (
                            ex.Message.Contains("locked") ||
                            ex.Message.Contains("database is locked") ||
                            ex.InnerException?.Message?.Contains("locked") == true)
                        {
                            if (retry == maxRetries - 1) throw;
                            await Task.Delay(100 * (retry + 1));
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);

            using var verifyDb = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={dbPath};",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute,
            });

            var totalRecords = await verifyDb.Queryable<MotorTestRecordEntity>().CountAsync();
            Assert.Equal(barcodeCount, totalRecords);

            verifyDb.Close();
            if (File.Exists(dbPath))
            {
                try { File.Delete(dbPath); }
                catch { /* cleanup best-effort */ }
            }
        }

        private static void ApplyTestStage(MotorTestRecordEntity record, StageTestData data)
        {
            string stageResult = string.Equals(data.Result, "OK", StringComparison.OrdinalIgnoreCase) ? "OK" : "NG";

            switch (data.Stage)
            {
                case TestStage.NoLoad:
                    record.NoLoadCurrent = data.NoLoadCurrent;
                    record.NoLoadSpeed = data.NoLoadSpeed;
                    record.ShaftLength = data.ShaftLength;
                    record.KnurlDiameter = data.KnurlDiameter;
                    record.NoLoadResult = stageResult;
                    break;
                case TestStage.Noise:
                    record.FwdNoise = data.FwdNoise;
                    record.RevNoise = data.RevNoise;
                    record.NoiseDiff = data.NoiseDiff;
                    record.NoiseResult = stageResult;
                    break;
                case TestStage.Load:
                    record.LoadCurrent = data.LoadCurrent;
                    record.LoadSpeed = data.LoadSpeed;
                    record.LoadResult = stageResult;
                    break;
            }
        }
    }
}
