using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MotorTestSystem.Models;
using MotorTestSystem.Models.Entities;
using MotorTestSystem.Services;
using SqlSugar;
using Xunit;

namespace MotorTestSystem.Tests.PropertyTests
{
    /// <summary>
    /// Property 2: 事务失败后的状态回滚
    /// Validates: Requirements 1.3
    /// 注入故障，验证失败后数据库状态与初始状态一致。
    /// </summary>
    public class TransactionRollbackTests : IDisposable
    {
        private readonly string _dbDir;

        public TransactionRollbackTests()
        {
            _dbDir = Path.Combine(Path.GetTempPath(), $"TranRollback_Test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_dbDir);
        }

        /// <summary>
        /// 清理：整个测试目录在结束时会通过测试框架清理
        /// </summary>
        public void Dispose()
        {
            if (Directory.Exists(_dbDir))
            {
                try { Directory.Delete(_dbDir, true); }
                catch { /* cleanup best-effort */ }
            }
        }

        /// <summary>
        /// 验证事务失败时数据库状态正确回滚到初始状态。
        /// 测试 20 次迭代，每次使用独立数据库文件，
        /// 在事务中注入托管异常，验证回滚。
        /// 迭代数据库文件在测试方法结束时统一通过 Dispose 清理。
        /// </summary>
        [Fact]
        public async Task TransactionFailure_ShouldRollbackToInitialState()
        {
            var rng = new Random(42);

            for (int iteration = 0; iteration < 20; iteration++)
            {
                string dbPath = Path.Combine(_dbDir, $"Test_{iteration}.db");

                // 每个迭代使用独立的 SqlSugarClient
                using (var db = new SqlSugarClient(new ConnectionConfig
                {
                    ConnectionString = $"Data Source={dbPath};",
                    DbType = DbType.Sqlite,
                    IsAutoCloseConnection = true,
                    InitKeyType = InitKeyType.Attribute,
                }))
                {
                    db.CodeFirst.InitTables(typeof(MotorTestRecordEntity));

                    // 1. 创建初始记录
                    string barcode = $"SN-ROLLBACK-{iteration:D4}";
                    var initialRecord = new MotorTestRecordEntity
                    {
                        Barcode = barcode,
                        TestTime = DateTime.UtcNow.AddHours(-rng.Next(1, 48)),
                        FinalResult = "OK",
                        NoLoadCurrent = Math.Round(rng.NextDouble() * 2.5, 3),
                        NoLoadSpeed = rng.Next(2900, 3100),
                        NoLoadResult = "OK"
                    };

                    await db.Insertable(initialRecord).ExecuteCommandAsync();

                    // 记录初始状态
                    var beforeState = await db.Queryable<MotorTestRecordEntity>()
                        .FirstAsync(r => r.Barcode == barcode);

                    // 2. 执行一个注定失败的事务
                    var result = await db.Ado.UseTranAsync(async () =>
                    {
                        var record = await db.Queryable<MotorTestRecordEntity>()
                            .FirstAsync(r => r.Barcode == barcode);

                        record.FinalResult = "NG";
                        record.NoiseResult = "NG";
                        record.FwdNoise = 85.5;
                        record.RevNoise = 75.2;
                        await db.Updateable(record).ExecuteCommandAsync();

                        // 注入故障
                        throw new InvalidOperationException("Simulated rollback trigger.");
                    });

                    Assert.False(result.IsSuccess,
                        $"Iteration {iteration}: Expected transaction to fail.");

                    // 3. 验证状态回滚
                    var afterState = await db.Queryable<MotorTestRecordEntity>()
                        .FirstAsync(r => r.Barcode == barcode);

                    Assert.Equal(beforeState.FinalResult, afterState.FinalResult);
                    Assert.Equal(beforeState.NoLoadResult, afterState.NoLoadResult);
                    Assert.Equal(beforeState.NoLoadCurrent, afterState.NoLoadCurrent);
                    Assert.Equal(beforeState.NoLoadSpeed, afterState.NoLoadSpeed);
                    Assert.Null(afterState.NoiseResult);
                    Assert.Null(afterState.FwdNoise);
                    Assert.Null(afterState.RevNoise);
                }
                // 文件不在此删除，留给 Dispose 统一清理
            }
        }

        /// <summary>
        /// 验证外层事务在内部异常后回滚。
        /// 不嵌套 UseTranAsync，而是在外层事务中直接 throw。
        /// </summary>
        [Fact]
        public async Task NestedTransactionFailure_ShouldRollbackOuterTransaction()
        {
            string dbPath = Path.Combine(_dbDir, "NestedTest.db");

            using (var db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={dbPath};",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute,
            }))
            {
                db.CodeFirst.InitTables(typeof(MotorTestRecordEntity));

                // Arrange: 创建 3 条初始记录
                for (int i = 1; i <= 3; i++)
                {
                    await db.Insertable(new MotorTestRecordEntity
                    {
                        Barcode = $"SN-NESTED-{i}",
                        TestTime = DateTime.UtcNow,
                        FinalResult = "OK"
                    }).ExecuteCommandAsync();
                }

                var beforeRecords = await db.Queryable<MotorTestRecordEntity>().ToListAsync();
                Assert.Equal(3, beforeRecords.Count);

                // Act: 在外层事务中修改数据然后 throw 触发回滚
                var result = await db.Ado.UseTranAsync(async () =>
                {
                    var r1 = await db.Queryable<MotorTestRecordEntity>()
                        .FirstAsync(r => r.Barcode == "SN-NESTED-1");
                    r1.FinalResult = "NG";
                    await db.Updateable(r1).ExecuteCommandAsync();

                    await db.Insertable(new MotorTestRecordEntity
                    {
                        Barcode = "SN-NESTED-4",
                        TestTime = DateTime.UtcNow,
                        FinalResult = "NG"
                    }).ExecuteCommandAsync();

                    // 直接抛出异常触发外层事务回滚
                    throw new InvalidOperationException("Simulated outer transaction failure.");
                });

                Assert.False(result.IsSuccess, "Expected transaction to fail.");

                // Assert: 验证所有记录回到初始状态
                var afterRecords = await db.Queryable<MotorTestRecordEntity>()
                    .OrderBy(r => r.Barcode)
                    .ToListAsync();

                Assert.Equal(3, afterRecords.Count);
                Assert.All(afterRecords, r => Assert.Equal("OK", r.FinalResult));
            }
        }
    }
}
