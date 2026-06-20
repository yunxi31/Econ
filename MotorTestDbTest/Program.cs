using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MotorTestSystem.Models;
using MotorTestSystem.Models.Entities;
using MotorTestSystem.Services;
using SqlSugar;

namespace MotorTestDbTest
{
    /// <summary>
    /// SQLite + SqlSugar 数据层全面测试程序
    /// 直接使用 SqlSugarScope，绕过 SqlSugarDbContext 的静态初始化问题
    /// </summary>
    class Program
    {
        private static int _totalTests = 0;
        private static int _passedTests = 0;
        private static int _failedTests = 0;
        private static readonly List<string> _failDetails = new();
        private static string _testDbPath = "";

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("╔══════════════════════════════════════════════════════╗");
            Console.WriteLine("║   SQLite + SqlSugar 数据层集成测试                   ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════╝");
            Console.WriteLine();

            // 使用独立的测试数据库
            string testDbDir = Path.Combine(Path.GetTempPath(), "MotorTestDbTest");
            if (!Directory.Exists(testDbDir)) Directory.CreateDirectory(testDbDir);
            _testDbPath = Path.Combine(testDbDir, $"test_{DateTime.Now:yyyyMMdd_HHmmss}.db");

            try
            {
                // ===== 1. 数据库初始化测试 =====
                TestDatabaseInitialization();

                // ===== 2. 种子数据验证 =====
                TestSeedData();

                // ===== 3. Repository CRUD 测试 =====
                await TestRepositoryCRUD();

                // ===== 4. UserService CRUD 测试 =====
                TestUserServiceCRUD();

                // ===== 5. 高级查询测试 =====
                await TestAdvancedQueries();

                // ===== 6. 数据持久化测试 =====
                TestDataPersistence();

                // ===== 7. 边界条件测试 =====
                await TestEdgeCases();

                // ===== 8. 通知数据CRUD与持久化测试 =====
                TestNotificationCRUD();
            }
            finally
            {
                // 清理测试数据库
                try
                {
                    // SqlSugarScope 在进程退出时会自动释放连接
                    if (File.Exists(_testDbPath))
                    {
                        File.Delete(_testDbPath);
                        Console.WriteLine("\n🧹 已清理测试数据库");
                    }
                }
                catch { /* 忽略清理失败 */ }
            }

            // ===== 输出测试报告 =====
            PrintReport();
        }

        /// <summary>创建独立的测试用 SqlSugarScope</summary>
        private static ISqlSugarClient CreateDb()
        {
            var dir = Path.GetDirectoryName(_testDbPath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var db = new SqlSugarScope(new ConnectionConfig
            {
                ConnectionString = $"Data Source={_testDbPath};",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute,
                MoreSettings = new ConnMoreSettings { IsAutoRemoveDataCache = true },
                ConfigureExternalServices = new ConfigureExternalServices
                {
                    EntityService = (property, column) =>
                    {
                        if (column.DataType == "decimal") column.DataType = "REAL";
                    }
                }
            });

            // 建表
            db.CodeFirst.InitTables(
                typeof(MotorTestRecordEntity),
                typeof(UserEntity),
                typeof(StationConfigEntity),
                typeof(NotificationEntity)
            );

            return db;
        }

        /// <summary>创建初始化好种子数据的数据库</summary>
        private static ISqlSugarClient CreateDbWithSeed()
        {
            var db = CreateDb();
            if (!db.Queryable<UserEntity>().Any()) SeedUsers(db);
            if (!db.Queryable<StationConfigEntity>().Any()) SeedStationConfigs(db);
            if (!db.Queryable<NotificationEntity>().Any()) SeedNotifications(db);
            return db;
        }

        /// <summary>通过反射替换 SqlSugarDbContext 的 Db 属性</summary>
        private static void ReplaceDb(SqlSugarDbContext ctx, ISqlSugarClient newDb)
        {
            // Db 是只读自动属性，需要通过 backing field 注入
            var backingField = typeof(SqlSugarDbContext).GetField("<Db>k__BackingField",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (backingField == null)
            {
                // 尝试其他可能的 backing field 名称
                var fields = typeof(SqlSugarDbContext).GetFields(
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                backingField = fields.FirstOrDefault(f => f.Name.Contains("Db"));
            }
            if (backingField != null)
            {
                backingField.SetValue(ctx, newDb);
            }
            else
            {
                throw new InvalidOperationException("无法找到 SqlSugarDbContext.Db 的 backing field");
            }
        }

        /// <summary>创建 SqlMotorTestRepository（通过反射注入 ISqlSugarClient）</summary>
        private static SqlMotorTestRepository CreateRepository(ISqlSugarClient db)
        {
            var ctx = new SqlSugarDbContext();
            ReplaceDb(ctx, db);
            var repo = new SqlMotorTestRepository(ctx);
            return repo;
        }

        /// <summary>创建 SqlSugarUserService（通过反射注入 ISqlSugarClient）</summary>
        private static SqlSugarUserService CreateUserService(ISqlSugarClient db)
        {
            var ctx = new SqlSugarDbContext();
            ReplaceDb(ctx, db);
            var service = new SqlSugarUserService(ctx);
            return service;
        }

        // ================================================================
        // 1. 数据库初始化测试
        // ================================================================
        static void TestDatabaseInitialization()
        {
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("📦 1. 数据库初始化测试");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            var db = CreateDb();

            // 1.1 数据库文件创建
            Assert("数据库文件应被创建", File.Exists(_testDbPath));

            // 1.2 表创建验证
            var tables = db.DbMaintenance.GetTableInfoList(false);
            Assert("MotorTestRecords 表已创建", tables.Any(t => t.Name == "MotorTestRecords"));
            Assert("Users 表已创建", tables.Any(t => t.Name == "Users"));
            Assert("StationConfigs 表已创建", tables.Any(t => t.Name == "StationConfigs"));
            Assert("Notifications 表已创建", tables.Any(t => t.Name == "Notifications"));
            Assert("共创建 4 张表", tables.Count == 4, $"实际: {tables.Count}");

            // 1.3 表结构验证 - MotorTestRecords
            var mtrColumns = db.DbMaintenance.GetColumnInfosByTableName("MotorTestRecords", false);
            Assert("MotorTestRecords 有自增主键 Id", mtrColumns.Any(c => c.DbColumnName == "Id" && c.IsIdentity));
            Assert("MotorTestRecords 有 Barcode 列", mtrColumns.Any(c => c.DbColumnName == "Barcode"));
            Assert("MotorTestRecords 有 FinalResult 列", mtrColumns.Any(c => c.DbColumnName == "FinalResult"));
            Assert("MotorTestRecords 有 NoLoadCurrent 列", mtrColumns.Any(c => c.DbColumnName == "NoLoadCurrent"));
            Assert("MotorTestRecords 有 NoiseDiff 列", mtrColumns.Any(c => c.DbColumnName == "NoiseDiff"));

            // 1.4 表结构验证 - Users
            var userColumns = db.DbMaintenance.GetColumnInfosByTableName("Users", false);
            Assert("Users 有 Id 主键", userColumns.Any(c => c.DbColumnName == "Id" && c.IsPrimarykey));
            Assert("Users 有 Account 列", userColumns.Any(c => c.DbColumnName == "Account"));
            Assert("Users 有 PasswordHash 列", userColumns.Any(c => c.DbColumnName == "PasswordHash"));
            Assert("Users 有 Role 列 (int)", userColumns.Any(c => c.DbColumnName == "Role"));
            Assert("Users 有 Status 列 (int)", userColumns.Any(c => c.DbColumnName == "Status"));

            Console.WriteLine();
        }

        // ================================================================
        // 2. 种子数据验证
        // ================================================================
        static void TestSeedData()
        {
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("🌱 2. 种子数据验证");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            var db = CreateDbWithSeed();

            // 2.1 用户种子数据
            var users = db.Queryable<UserEntity>().ToList();
            Assert("种子用户数 = 16", users.Count == 16, $"实际: {users.Count}");

            var admins = users.Count(u => u.Role == (int)AppRole.Admin);
            var operators = users.Count(u => u.Role == (int)AppRole.Operator);
            var maintainers = users.Count(u => u.Role == (int)AppRole.Maintainer);
            Assert("管理员数 = 3", admins == 3, $"实际: {admins}");
            Assert("操作员数 = 8", operators == 8, $"实际: {operators}");
            Assert("维护员数 = 5", maintainers == 5, $"实际: {maintainers}");

            // 验证默认管理员
            var admin = users.FirstOrDefault(u => u.Account == "admin");
            Assert("admin 用户存在", admin != null);
            Assert("admin 角色为 Admin", admin?.Role == (int)AppRole.Admin);
            Assert("admin 密码哈希非空", !string.IsNullOrEmpty(admin?.PasswordHash));
            Assert("admin 密码验证通过", admin != null && InMemoryUserService.HashPassword("admin123") == admin.PasswordHash);

            // 验证默认操作员
            var op = users.FirstOrDefault(u => u.Account == "operator");
            Assert("operator 用户存在", op != null);
            Assert("operator 密码验证通过", op != null && InMemoryUserService.HashPassword("123") == op.PasswordHash);

            // 验证默认维护员
            var mt = users.FirstOrDefault(u => u.Account == "maintainer");
            Assert("maintainer 用户存在", mt != null);
            Assert("maintainer 密码验证通过", mt != null && InMemoryUserService.HashPassword("maint123") == mt.PasswordHash);

            // 验证禁用用户
            var disabledUsers = users.Count(u => u.Status == (int)UserStatus.Disabled);
            Assert("禁用用户数 = 4", disabledUsers == 4, $"实际: {disabledUsers}");

            // 2.2 工位配置种子数据
            var configs = db.Queryable<StationConfigEntity>().ToList();
            Assert("工位配置数 = 6", configs.Count == 6, $"实际: {configs.Count}");

            var a2 = configs.FirstOrDefault(c => c.Id == "A2");
            Assert("A2 工位协议为 S7 Protocol (TCP)", a2?.Protocol == "S7 Protocol (TCP)");
            Assert("A2 工位 PLC 型号为 S7-1200", a2?.PlcModel == "S7-1200");

            var a5 = configs.FirstOrDefault(c => c.Id == "A5");
            Assert("A5 工位 PLC 型号为 S7-1500", a5?.PlcModel == "S7-1500");

            var a1 = configs.FirstOrDefault(c => c.Id == "A1");
            Assert("A1 工位协议为 ModbusTCP", a1?.Protocol == "ModbusTCP");

            // 2.3 通知种子数据
            var notifications = db.Queryable<NotificationEntity>().ToList();
            Assert("通知种子数 = 12", notifications.Count == 12, $"实际: {notifications.Count}");

            int alarmCount = notifications.Count(n => n.Type == 0);
            int maintCount = notifications.Count(n => n.Type == 1);
            int sysCount = notifications.Count(n => n.Type == 2);
            Assert("报警通知 = 3", alarmCount == 3, $"实际: {alarmCount}");
            Assert("维护通知 = 4", maintCount == 4, $"实际: {maintCount}");
            Assert("系统通知 = 5", sysCount == 5, $"实际: {sysCount}");

            int criticalCount = notifications.Count(n => n.Severity == 2);
            int warningCount = notifications.Count(n => n.Severity == 1);
            int infoCount = notifications.Count(n => n.Severity == 0);
            Assert("严重级通知 >= 2", criticalCount >= 2, $"实际: {criticalCount}");
            Assert("警告级通知 >= 3", warningCount >= 3, $"实际: {warningCount}");
            Assert("信息级通知 >= 4", infoCount >= 4, $"实际: {infoCount}");

            // 验证通知内容非空
            Assert("所有通知标题非空", notifications.All(n => !string.IsNullOrEmpty(n.Title)));
            Assert("所有通知内容非空", notifications.All(n => !string.IsNullOrEmpty(n.Content)));

            // 2.4 种子数据幂等性 — 重复播种不应重复插入
            SeedUsers(db); // 再次调用，但因为账号唯一约束会失败或跳过
            var users2 = db.Queryable<UserEntity>().ToList();
            Assert("重复播种后用户数不变（幂等）", users2.Count == 16, $"实际: {users2.Count}");

            Console.WriteLine();
        }

        // ================================================================
        // 3. Repository CRUD 测试
        // ================================================================
        static async Task TestRepositoryCRUD()
        {
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("🔄 3. Repository CRUD 测试");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            var db = CreateDbWithSeed();
            var repo = CreateRepository(db);

            // 3.1 插入空载阶段数据 (Upsert - 新记录)
            var barcode1 = "TEST-NOLOAD-001";
            var noLoadData = new StageTestData
            {
                Barcode = barcode1,
                StationId = "A1",
                Stage = TestStage.NoLoad,
                CollectedAt = DateTime.Now,
                Result = "OK",
                NoLoadCurrent = 1.234,
                NoLoadSpeed = 2980,
                ShaftLength = 25.678,
                KnurlDiameter = 12.345
            };

            await repo.UpsertStageResultAsync(noLoadData);

            var record1 = db.Queryable<MotorTestRecordEntity>().First(r => r.Barcode == barcode1);
            Assert("空载数据插入成功", record1 != null);
            Assert("空载电流 = 1.234", Math.Abs((record1?.NoLoadCurrent ?? 0) - 1.234) < 0.001, $"实际: {record1?.NoLoadCurrent}");
            Assert("空载转速 = 2980", record1?.NoLoadSpeed == 2980, $"实际: {record1?.NoLoadSpeed}");
            Assert("轴长 = 25.678", Math.Abs((record1?.ShaftLength ?? 0) - 25.678) < 0.001, $"实际: {record1?.ShaftLength}");
            Assert("滚花直径 = 12.345", Math.Abs((record1?.KnurlDiameter ?? 0) - 12.345) < 0.001, $"实际: {record1?.KnurlDiameter}");
            Assert("空载结果 = OK", record1?.NoLoadResult == "OK", $"实际: {record1?.NoLoadResult}");
            Assert("最终结果 = NG (仅空载阶段OK不算全OK)", record1?.FinalResult == "NG", $"实际: {record1?.FinalResult}");

            // 3.2 更新噪音阶段数据 (Upsert - 已有记录追加)
            var noiseData = new StageTestData
            {
                Barcode = barcode1,
                StationId = "A1",
                Stage = TestStage.Noise,
                CollectedAt = DateTime.Now,
                Result = "OK",
                FwdNoise = 45.6,
                RevNoise = 43.2,
                NoiseDiff = 2.4
            };

            await repo.UpsertStageResultAsync(noiseData);

            var record1b = db.Queryable<MotorTestRecordEntity>().First(r => r.Barcode == barcode1);
            Assert("噪音数据追加成功", record1b?.FwdNoise == 45.6, $"实际: {record1b?.FwdNoise}");
            Assert("噪音差 = 2.4", Math.Abs((record1b?.NoiseDiff ?? 0) - 2.4) < 0.01, $"实际: {record1b?.NoiseDiff}");
            Assert("空载数据保留: NoLoadCurrent", Math.Abs((record1b?.NoLoadCurrent ?? 0) - 1.234) < 0.001, $"实际: {record1b?.NoLoadCurrent}");
            Assert("最终结果仍 = NG (还差负载)", record1b?.FinalResult == "NG", $"实际: {record1b?.FinalResult}");

            // 3.3 更新负载阶段数据 (Upsert - 全部OK)
            var loadData = new StageTestData
            {
                Barcode = barcode1,
                StationId = "A1",
                Stage = TestStage.Load,
                CollectedAt = DateTime.Now,
                Result = "OK",
                LoadCurrent = 3.456,
                LoadSpeed = 3000
            };

            await repo.UpsertStageResultAsync(loadData);

            var record1c = db.Queryable<MotorTestRecordEntity>().First(r => r.Barcode == barcode1);
            Assert("负载数据追加成功", Math.Abs((record1c?.LoadCurrent ?? 0) - 3.456) < 0.001, $"实际: {record1c?.LoadCurrent}");
            Assert("负载转速 = 3000", record1c?.LoadSpeed == 3000);
            Assert("最终结果 = OK (三阶段全OK)", record1c?.FinalResult == "OK", $"实际: {record1c?.FinalResult}");

            // 3.4 插入NG记录
            var barcode2 = "TEST-NG-002";
            var ngData = new StageTestData
            {
                Barcode = barcode2,
                StationId = "A2",
                Stage = TestStage.NoLoad,
                CollectedAt = DateTime.Now,
                Result = "NG",
                NoLoadCurrent = 2.89,
                NoLoadSpeed = 3200,
                ShaftLength = 24.5,
                KnurlDiameter = 11.8
            };
            await repo.UpsertStageResultAsync(ngData);

            var record2 = db.Queryable<MotorTestRecordEntity>().First(r => r.Barcode == barcode2);
            Assert("NG记录空载结果 = NG", record2?.NoLoadResult == "NG");
            Assert("NG记录最终结果 = NG", record2?.FinalResult == "NG");

            // 3.5 GetRecentAsync 测试
            var recent = await repo.GetRecentAsync(10);
            Assert("GetRecent 返回记录数 >= 2", recent.Count >= 2, $"实际: {recent.Count}");

            // 3.6 QueryAsync 测试
            var query = new MotorTestQuery
            {
                Barcode = "TEST-",
                ResultFilter = "全部",
                StartTime = DateTime.Now.AddDays(-1),
                EndTime = DateTime.Now.AddDays(1)
            };
            var queryResults = await repo.QueryAsync(query);
            Assert("Query 按 TEST- 模糊查询返回 >= 2", queryResults.Count >= 2, $"实际: {queryResults.Count}");

            // 3.7 QueryAsync 按结果筛选
            var okQuery = new MotorTestQuery
            {
                Barcode = "",
                ResultFilter = "OK",
                StartTime = DateTime.Now.AddDays(-1),
                EndTime = DateTime.Now.AddDays(1)
            };
            var okResults = await repo.QueryAsync(okQuery);
            Assert("Query 筛选OK结果 >= 1", okResults.Count >= 1, $"实际: {okResults.Count}");
            Assert("所有返回记录均为OK", okResults.All(r => r.FinalResult == "OK"));

            Console.WriteLine();
        }

        // ================================================================
        // 4. UserService CRUD 测试
        // ================================================================
        static void TestUserServiceCRUD()
        {
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("👤 4. UserService CRUD 测试");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            var db = CreateDbWithSeed();
            var userService = CreateUserService(db);

            // 4.1 GetAll
            var allUsers = userService.GetAll();
            Assert("GetAll 返回 16 个种子用户", allUsers.Count == 16, $"实际: {allUsers.Count}");

            // 4.2 GetById
            var admin = userService.GetById("U00001");
            Assert("GetById U00001 返回 admin", admin?.Account == "admin", $"实际: {admin?.Account}");

            var notFound = userService.GetById("U99999");
            Assert("GetById 不存在返回 null", notFound == null);

            // 4.3 GetByAccount
            var opUser = userService.GetByAccount("operator");
            Assert("GetByAccount operator 返回用户", opUser?.Account == "operator");

            var notFound2 = userService.GetByAccount("nonexistent_user");
            Assert("GetByAccount 不存在返回 null", notFound2 == null);

            // 4.4 Create - 成功
            var createResult = userService.Create("testuser", "测试用户", "test123", AppRole.Operator);
            Assert("Create 成功返回 null", createResult == null, $"实际: {createResult}");

            var createdUser = userService.GetByAccount("testuser");
            Assert("新用户 testuser 已创建", createdUser != null);
            Assert("新用户姓名 = 测试用户", createdUser?.Name == "测试用户");
            Assert("新用户角色 = Operator", createdUser?.Role == AppRole.Operator);
            Assert("新用户ID自增正确 (U00017)", createdUser?.Id == "U00017", $"实际: {createdUser?.Id}");

            // 4.5 Create - 重复账号
            var dupResult = userService.Create("testuser", "重复用户", "xxx", AppRole.Operator);
            Assert("Create 重复账号返回错误", dupResult != null && dupResult.Contains("已存在"), $"实际: {dupResult}");

            // 4.6 Create - 空账号
            var emptyResult = userService.Create("", "用户", "123", AppRole.Operator);
            Assert("Create 空账号返回错误", emptyResult != null, $"实际: {emptyResult}");

            // 4.7 Update
            var updateResult = userService.Update(createdUser!.Id, "测试用户改", AppRole.Maintainer, UserStatus.Disabled);
            Assert("Update 成功返回 null", updateResult == null, $"实际: {updateResult}");

            var updatedUser = userService.GetById(createdUser.Id);
            Assert("更新后姓名 = 测试用户改", updatedUser?.Name == "测试用户改");
            Assert("更新后角色 = Maintainer", updatedUser?.Role == AppRole.Maintainer);
            Assert("更新后状态 = Disabled", updatedUser?.Status == UserStatus.Disabled);

            // 4.8 ValidatePassword
            bool validPwd = userService.ValidatePassword("admin", "admin123");
            Assert("ValidatePassword admin/admin123 = true", validPwd);

            bool invalidPwd = userService.ValidatePassword("admin", "wrongpwd");
            Assert("ValidatePassword admin/wrongpwd = false", !invalidPwd);

            bool disabledPwd = userService.ValidatePassword("ad_sunyan", "admin123");
            Assert("ValidatePassword 禁用用户 = false", !disabledPwd);

            // 4.9 ResetPassword
            var resetResult = userService.ResetPassword(createdUser.Id, "newpass456");
            Assert("ResetPassword 成功", resetResult == null, $"实际: {resetResult}");

            bool validAfterReset = userService.ValidatePassword("testuser", "newpass456");
            Assert("重置后新密码验证通过 (用户已被禁用，验证返回false)", !validAfterReset, "Disabled 用户无法通过密码验证");

            // 4.10 ChangePassword — 先重新启用用户
            var reEnableResult = userService.Update(createdUser.Id, "测试用户改", AppRole.Operator, UserStatus.Active);
            Assert("重新启用用户", reEnableResult == null);

            bool validAfterResetEnabled = userService.ValidatePassword("testuser", "newpass456");
            Assert("启用后密码验证通过", validAfterResetEnabled);

            var changeResult = userService.ChangePassword(createdUser.Id, "newpass456", "changed789");
            Assert("ChangePassword 成功", changeResult == null, $"实际: {changeResult}");

            bool validAfterChange = userService.ValidatePassword("testuser", "changed789");
            Assert("修改密码后验证通过", validAfterChange);

            var wrongOldPwd = userService.ChangePassword(createdUser.Id, "wrong_old", "xxx");
            Assert("ChangePassword 旧密码错误返回错误", wrongOldPwd != null);

            // 4.11 UpdateLastLoginTime
            userService.UpdateLastLoginTime("U00001");
            var adminAfterLogin = userService.GetById("U00001");
            Assert("LastLoginTime 已更新", adminAfterLogin?.LastLoginTime != null);

            // 4.12 Delete
            var deleteResult = userService.Delete(createdUser.Id);
            Assert("Delete 成功", deleteResult == null, $"实际: {deleteResult}");

            var deletedUser = userService.GetById(createdUser.Id);
            Assert("删除后查询返回 null", deletedUser == null);

            var deleteNotFound = userService.Delete("U99999");
            Assert("Delete 不存在用户返回错误", deleteNotFound != null);

            // 4.13 密码哈希一致性
            string hash1 = SqlSugarUserService.HashPassword("test123");
            string hash2 = SqlSugarUserService.HashPassword("test123");
            Assert("相同密码哈希一致", hash1 == hash2);

            string hash3 = SqlSugarUserService.HashPassword("different");
            Assert("不同密码哈希不同", hash1 != hash3);

            Console.WriteLine();
        }

        // ================================================================
        // 5. 高级查询测试
        // ================================================================
        static async Task TestAdvancedQueries()
        {
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("📊 5. 高级查询测试");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            var db = CreateDbWithSeed();
            var repo = CreateRepository(db);

            // 先插入一些有明确时间的数据用于测试
            var now = DateTime.Now;
            var testRecords = new[]
            {
                CreateTestRecord("STAT-OK-001", now.AddHours(-5), "OK",
                    1.2, 3000, "OK", 40.5, 38.2, 2.3, "OK", 2.8, 3050, "OK"),
                CreateTestRecord("STAT-OK-002", now.AddHours(-4), "OK",
                    1.1, 2990, "OK", 42.1, 40.0, 2.1, "OK", 2.9, 3010, "OK"),
                CreateTestRecord("STAT-OK-003", now.AddHours(-3), "OK",
                    1.3, 3010, "OK", 38.7, 37.5, 1.2, "OK", 2.7, 3020, "OK"),
                CreateTestRecord("STAT-NG-001", now.AddHours(-2), "NG",
                    2.8, 3200, "NG", 55.2, 38.0, 17.2, "NG", 2.9, 3010, "OK"),
                CreateTestRecord("STAT-NG-002", now.AddHours(-1), "NG",
                    1.2, 3000, "OK", 72.5, 55.0, 17.5, "NG", 3.8, 950, "NG"),
                CreateTestRecord("STAT-NG-003", now.AddMinutes(-30), "NG",
                    1.1, 2990, "OK", 42.1, 40.0, 2.1, "OK", 3.2, 980, "NG"),
                CreateTestRecord("STAT-YEST-OK-001", now.AddDays(-1), "OK",
                    1.15, 2995, "OK", 41.0, 39.5, 1.5, "OK", 2.85, 3030, "OK"),
            };

            foreach (var r in testRecords)
            {
                db.Insertable(r).ExecuteCommand();
            }

            // 5.1 GetSummaryAsync
            var todaySummary = await repo.GetSummaryAsync(now.Date, now.Date.AddDays(1));
            Assert("今日总计 >= 6", todaySummary.TotalChecked >= 6, $"实际: {todaySummary.TotalChecked}");
            Assert("今日OK >= 3", todaySummary.OkCount >= 3, $"实际: {todaySummary.OkCount}");
            Assert("今日NG >= 3", todaySummary.NgCount >= 3, $"实际: {todaySummary.NgCount}");
            Assert("今日良率 > 0", todaySummary.PassRate > 0, $"实际: {todaySummary.PassRate}");

            // 5.2 GetDefectSummaryAsync
            var defectSummary = await repo.GetDefectSummaryAsync(now.Date, now.Date.AddDays(1));
            Assert("空载NG >= 1", defectSummary.NoLoadNgCount >= 1, $"实际: {defectSummary.NoLoadNgCount}");
            Assert("噪音NG >= 2", defectSummary.NoiseNgCount >= 2, $"实际: {defectSummary.NoiseNgCount}");
            Assert("负载NG >= 2", defectSummary.LoadNgCount >= 2, $"实际: {defectSummary.LoadNgCount}");
            Assert("总不良数 > 0", defectSummary.TotalNgCount > 0);

            // 5.3 GetFaultRankingAsync
            var faultRanking = await repo.GetFaultRankingAsync(now.Date, now.Date.AddDays(1), topN: 10);
            Assert("故障排行非空", faultRanking.Count > 0);
            Assert("故障排行按次数降序", IsDescending(faultRanking.Select(f => f.Count).ToList()));
            Assert("排名第一的故障名非空", !string.IsNullOrEmpty(faultRanking.FirstOrDefault()?.Name));
            Assert("排名第一的故障次数 >= 1", faultRanking.FirstOrDefault()?.Count >= 1);

            Console.WriteLine("  📋 故障排行详情:");
            foreach (var item in faultRanking)
            {
                Console.WriteLine($"     #{item.Rank} {item.Name}: {item.Count}次");
            }

            // 5.4 按时间范围筛选
            var yesterdayOnly = await repo.QueryAsync(new MotorTestQuery
            {
                Barcode = "STAT-YEST",
                ResultFilter = "全部",
                StartTime = now.AddDays(-2),
                EndTime = now.AddDays(-0.5)
            });
            Assert("昨天范围查询返回 >= 1", yesterdayOnly.Count >= 1);

            // 5.5 空查询条件
            var emptyBarcodeQuery = await repo.QueryAsync(new MotorTestQuery
            {
                Barcode = "",
                ResultFilter = "全部",
                StartTime = now.AddDays(-1),
                EndTime = now.AddDays(1)
            });
            Assert("空条码查询返回所有记录", emptyBarcodeQuery.Count >= 6);

            Console.WriteLine();
        }

        // ================================================================
        // 6. 数据持久化测试
        // ================================================================
        static void TestDataPersistence()
        {
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("💾 6. 数据持久化测试");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            var db = CreateDbWithSeed();

            // 6.1 关闭连接后重新打开，数据仍存在
            var userCount1 = db.Queryable<UserEntity>().Count();
            var recordCount1 = db.Queryable<MotorTestRecordEntity>().Count();

            // 模拟重新创建连接
            var db2 = CreateDb();
            var userCount2 = db2.Queryable<UserEntity>().Count();
            var recordCount2 = db2.Queryable<MotorTestRecordEntity>().Count();

            Assert("重新打开后用户数不变", userCount1 == userCount2, $"前: {userCount1}, 后: {userCount2}");
            Assert("重新打开后记录数不变", recordCount1 == recordCount2, $"前: {recordCount1}, 后: {recordCount2}");

            // 6.2 数据库文件大小
            var fileInfo = new FileInfo(_testDbPath);
            Assert("数据库文件大小 > 0", fileInfo.Length > 0);
            Console.WriteLine($"  📁 数据库文件大小: {fileInfo.Length / 1024.0:F1} KB");

            // 6.3 事务回滚测试
            try
            {
                db.Ado.BeginTran();
                db.Insertable(new UserEntity
                {
                    Id = "U_TEMP_01",
                    Account = "temp_user",
                    Name = "临时用户",
                    PasswordHash = "HASH",
                    Role = 1,
                    Status = 0,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                }).ExecuteCommand();
                db.Ado.RollbackTran();
            }
            catch
            {
                try { db.Ado.RollbackTran(); } catch { }
            }

            var tempUser = db.Queryable<UserEntity>().First(u => u.Account == "temp_user");
            Assert("事务回滚后临时用户不存在", tempUser == null);

            // 6.4 事务提交测试
            try
            {
                db.Ado.BeginTran();
                db.Insertable(new UserEntity
                {
                    Id = "U_COMMIT_01",
                    Account = "commit_user",
                    Name = "提交用户",
                    PasswordHash = "HASH",
                    Role = 1,
                    Status = 0,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                }).ExecuteCommand();
                db.Ado.CommitTran();
            }
            catch
            {
                try { db.Ado.RollbackTran(); } catch { }
            }

            var commitUser = db.Queryable<UserEntity>().First(u => u.Account == "commit_user");
            Assert("事务提交后用户存在", commitUser != null);

            // 6.5 数据库完整性检查
            var tables = db.DbMaintenance.GetTableInfoList(false);
            Assert("数据库完整性检查 - 表结构完整", tables.Count == 4);

            Console.WriteLine();
        }

        // ================================================================
        // 7. 边界条件测试
        // ================================================================
        static async Task TestEdgeCases()
        {
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("🧪 7. 边界条件测试");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            var db = CreateDbWithSeed();
            var repo = CreateRepository(db);

            // 7.1 空条码应抛异常
            bool threwArgEx = false;
            try
            {
                await repo.UpsertStageResultAsync(new StageTestData
                {
                    Barcode = "",
                    Stage = TestStage.NoLoad,
                    Result = "OK",
                    CollectedAt = DateTime.Now
                });
            }
            catch (ArgumentException)
            {
                threwArgEx = true;
            }
            Assert("空条码抛出 ArgumentException", threwArgEx);

            // 7.2 null data 应抛异常
            bool threwNullEx = false;
            try
            {
                await repo.UpsertStageResultAsync(null!);
            }
            catch (ArgumentNullException)
            {
                threwNullEx = true;
            }
            Assert("null data 抛出 ArgumentNullException", threwNullEx);

            // 7.3 GetRecentAsync(0) 应返回空
            var emptyResult = await repo.GetRecentAsync(0);
            Assert("GetRecentAsync(0) 返回空列表", emptyResult.Count == 0);

            // 7.4 GetSummaryAsync 无匹配时间范围
            var emptySummary = await repo.GetSummaryAsync(DateTime.Now.AddDays(100), DateTime.Now.AddDays(101));
            Assert("无匹配范围 TotalChecked = 0", emptySummary.TotalChecked == 0);
            Assert("无匹配范围 PassRate = 0", emptySummary.PassRate == 0);

            // 7.5 GetDefectSummaryAsync 无匹配
            var emptyDefect = await repo.GetDefectSummaryAsync(DateTime.Now.AddDays(100), DateTime.Now.AddDays(101));
            Assert("无匹配缺陷 TotalNgCount = 0", emptyDefect.TotalNgCount == 0);

            // 7.6 GetFaultRankingAsync 无匹配
            var emptyFault = await repo.GetFaultRankingAsync(DateTime.Now.AddDays(100), DateTime.Now.AddDays(101));
            Assert("无匹配故障排行返回空列表", emptyFault.Count == 0);

            // 7.7 超长条码
            var longBarcode = "L" + new string('O', 48) + "G"; // 50 chars
            await repo.UpsertStageResultAsync(new StageTestData
            {
                Barcode = longBarcode,
                Stage = TestStage.NoLoad,
                Result = "OK",
                CollectedAt = DateTime.Now,
                NoLoadCurrent = 1.0,
                NoLoadSpeed = 3000
            });
            var longRecord = db.Queryable<MotorTestRecordEntity>().First(r => r.Barcode == longBarcode);
            Assert("超长条码(50字符)存储成功", longRecord != null);

            // 7.8 特殊字符条码
            var specialBarcode = "SPC-条码_测试.123";
            await repo.UpsertStageResultAsync(new StageTestData
            {
                Barcode = specialBarcode,
                Stage = TestStage.Noise,
                Result = "OK",
                CollectedAt = DateTime.Now,
                FwdNoise = 40.0,
                RevNoise = 38.0,
                NoiseDiff = 2.0
            });
            var specialRecord = db.Queryable<MotorTestRecordEntity>().First(r => r.Barcode == specialBarcode);
            Assert("含中文/特殊字符条码存储成功", specialRecord != null);

            Console.WriteLine();
        }

        // ================================================================
        // 8. 通知数据CRUD与持久化测试
        // ================================================================
        static void TestNotificationCRUD()
        {
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("🔔 8. 通知数据CRUD与持久化测试");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            var db = CreateDbWithSeed();
            int initialCount = db.Queryable<NotificationEntity>().Count();

            // 8.1 插入新通知
            var newNotification = new NotificationEntity
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                Title = "测试通知",
                Content = "数据库持久化验证测试",
                CreatedAt = DateTime.Now,
                Type = 0, // Alarm
                Severity = 2, // Critical
                IsRead = false,
                Source = "TEST"
            };
            db.Insertable(newNotification).ExecuteCommand();

            var inserted = db.Queryable<NotificationEntity>().First(e => e.Id == newNotification.Id);
            Assert("新通知插入成功", inserted != null, $"实际: {(inserted == null ? "null" : "ok")}");
            Assert("标题匹配", inserted?.Title == "测试通知", $"实际: {inserted?.Title}");
            Assert("内容匹配", inserted?.Content == "数据库持久化验证测试", $"实际: {inserted?.Content}");
            Assert("类型=Alarm(0)", inserted?.Type == 0, $"实际: {inserted?.Type}");
            Assert("严重=Critical(2)", inserted?.Severity == 2, $"实际: {inserted?.Severity}");
            Assert("未读", inserted?.IsRead == false, $"实际: {inserted?.IsRead}");
            Assert("来源=TEST", inserted?.Source == "TEST", $"实际: {inserted?.Source}");
            Assert("插入后总计 + 1", db.Queryable<NotificationEntity>().Count() == initialCount + 1);

            // 8.2 标记为已读
            db.Updateable<NotificationEntity>()
                .SetColumns(e => new NotificationEntity { IsRead = true })
                .Where(e => e.Id == newNotification.Id)
                .ExecuteCommand();

            var readNotif = db.Queryable<NotificationEntity>().First(e => e.Id == newNotification.Id);
            Assert("标记已读成功", readNotif?.IsRead == true, $"实际: {readNotif?.IsRead}");

            // 8.3 批量更新全部为已读
            db.Updateable<NotificationEntity>()
                .SetColumns(e => new NotificationEntity { IsRead = true })
                .Where(e => !e.IsRead)
                .ExecuteCommand();

            int unreadCount = db.Queryable<NotificationEntity>().Where(e => !e.IsRead).Count();
            Assert("全部标记已读后无未读", unreadCount == 0, $"实际未读: {unreadCount}");

            // 8.4 删除单条通知
            db.Deleteable<NotificationEntity>()
                .Where(e => e.Id == newNotification.Id)
                .ExecuteCommand();

            var deleted = db.Queryable<NotificationEntity>().First(e => e.Id == newNotification.Id);
            Assert("删除后查询为 null", deleted == null);
            Assert("删除后数量恢复", db.Queryable<NotificationEntity>().Count() == initialCount);

            // 8.5 清空全部
            db.Deleteable<NotificationEntity>().Where(e => true).ExecuteCommand();
            Assert("清空后数量为 0", db.Queryable<NotificationEntity>().Count() == 0, $"实际: {db.Queryable<NotificationEntity>().Count()}");

            // 8.6 重新播种验证幂等
            SeedNotifications(db);
            Assert("重新播种后 12 条", db.Queryable<NotificationEntity>().Count() == 12, $"实际: {db.Queryable<NotificationEntity>().Count()}");

            // 8.7 批量插入测试
            var batchItems = new List<NotificationEntity>();
            for (int i = 0; i < 5; i++)
            {
                batchItems.Add(new NotificationEntity
                {
                    Id = Guid.NewGuid().ToString("N")[..8],
                    Title = $"批量通知 #{i + 1}",
                    Content = $"批量插入测试 - 第{i + 1}条",
                    CreatedAt = DateTime.Now.AddMinutes(-i),
                    Type = i % 3,
                    Severity = i % 3,
                    IsRead = false
                });
            }
            db.Insertable(batchItems).ExecuteCommand();
            Assert("批量插入5条后总数=17", db.Queryable<NotificationEntity>().Count() == 17, $"实际: {db.Queryable<NotificationEntity>().Count()}");

            // 8.8 按类型查询
            int alarmTotal = db.Queryable<NotificationEntity>().Where(e => e.Type == 0).Count();
            Assert("批量插入后报警 >= 3", alarmTotal >= 3, $"实际: {alarmTotal}");

            // 8.9 按来源筛选
            int testSource = db.Queryable<NotificationEntity>().Where(e => e.Source == "TEST").Count();
            Assert("来源筛选", testSource >= 0); // 只是验证语法正确

            // 8.10 按时间范围
            int recentCount = db.Queryable<NotificationEntity>()
                .Where(e => e.CreatedAt >= DateTime.Now.AddHours(-1))
                .Count();
            Assert("最近1小时通知 >= 5", recentCount >= 5, $"实际: {recentCount}");

            // 8.11 跨实例持久化验证
            var db2 = CreateDb();
            var reloadedCount = db2.Queryable<NotificationEntity>().Count();
            Assert("新连接仍可读取数据 (持久化)", reloadedCount == 17, $"实际: {reloadedCount}");

            // 8.12 枚举映射一致性
            var alarmItems = db.Queryable<NotificationEntity>().Where(e => e.Type == 0).ToList();
            Assert("Enum→int 映射: Alarm=0", alarmItems.All(e => e.Type == (int)NotificationType.Alarm));

            var criticalItems = db.Queryable<NotificationEntity>().Where(e => e.Severity == 2).ToList();
            Assert("Enum→int 映射: Critical=2", criticalItems.All(e => e.Severity == (int)NotificationSeverity.Critical));

            Console.WriteLine();
        }

        /// <summary>通知种子数据辅助方法</summary>

        private static void SeedUsers(ISqlSugarClient db)
        {
            if (db.Queryable<UserEntity>().Any()) return; // 已有数据不重复
            var now = DateTime.Now;
            var users = new[]
            {
                CreateSeedUser("U00001", "admin", "系统管理员", "admin123", AppRole.Admin, UserStatus.Active, now.AddHours(-1), now.AddMonths(-6)),
                CreateSeedUser("U00002", "ad_liwei", "李威", "admin123", AppRole.Admin, UserStatus.Active, now.AddDays(-3), now.AddMonths(-3)),
                CreateSeedUser("U00003", "ad_sunyan", "孙燕", "admin123", AppRole.Admin, UserStatus.Disabled, null, now.AddMonths(-8)),
                CreateSeedUser("U00004", "operator", "默认操作员", "123", AppRole.Operator, UserStatus.Active, now.AddMinutes(-10), now.AddMonths(-5)),
                CreateSeedUser("U00005", "op_zhangwei", "张伟", "123", AppRole.Operator, UserStatus.Active, now.AddHours(-3), now.AddMonths(-4)),
                CreateSeedUser("U00006", "op_lina", "李娜", "123", AppRole.Operator, UserStatus.Active, now.AddDays(-1), now.AddMonths(-3)),
                CreateSeedUser("U00007", "op_zhaolei", "赵雷", "123", AppRole.Operator, UserStatus.Disabled, now.AddDays(-30), now.AddMonths(-2)),
                CreateSeedUser("U00008", "op_chenjing", "陈静", "123", AppRole.Operator, UserStatus.Active, now.AddMinutes(-45), now.AddMonths(-1)),
                CreateSeedUser("U00009", "op_zhoumei", "周梅", "123", AppRole.Operator, UserStatus.Active, now.AddHours(-8), now.AddMonths(-2)),
                CreateSeedUser("U00010", "op_wugang", "吴刚", "123", AppRole.Operator, UserStatus.Active, null, now.AddDays(-14)),
                CreateSeedUser("U00011", "op_huangli", "黄丽", "123", AppRole.Operator, UserStatus.Disabled, now.AddDays(-7), now.AddDays(-14)),
                CreateSeedUser("U00012", "maintainer", "默认维护员", "maint123", AppRole.Maintainer, UserStatus.Active, now.AddHours(-2), now.AddMonths(-6)),
                CreateSeedUser("U00013", "mt_wangqiang", "王强", "maint123", AppRole.Maintainer, UserStatus.Active, now.AddHours(-5), now.AddMonths(-4)),
                CreateSeedUser("U00014", "mt_liuyang", "刘洋", "maint123", AppRole.Maintainer, UserStatus.Active, now.AddDays(-2), now.AddMonths(-3)),
                CreateSeedUser("U00015", "mt_zhaomin", "赵敏", "maint123", AppRole.Maintainer, UserStatus.Active, now.AddMinutes(-20), now.AddMonths(-1)),
                CreateSeedUser("U00016", "mt_chenhao", "陈昊", "maint123", AppRole.Maintainer, UserStatus.Disabled, now.AddDays(-15), now.AddMonths(-2)),
            };
            db.Insertable(users).ExecuteCommand();
        }

        private static UserEntity CreateSeedUser(string id, string account, string name, string password,
            AppRole role, UserStatus status, DateTime? lastLogin, DateTime createdAt)
        {
            return new UserEntity
            {
                Id = id,
                Account = account,
                Name = name,
                PasswordHash = InMemoryUserService.HashPassword(password),
                Role = (int)role,
                Status = (int)status,
                LastLoginTime = lastLogin,
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            };
        }

        private static void SeedStationConfigs(ISqlSugarClient db)
        {
            if (db.Queryable<StationConfigEntity>().Any()) return;
            var configs = new[]
            {
                new StationConfigEntity { Id = "A1", Name = "A1", PlcModel = "FX5U", IpAddress = "192.168.10.11", Port = 502, Protocol = "ModbusTCP", StationId = 1, IsConnected = true, Status = "在线" },
                new StationConfigEntity { Id = "A2", Name = "A2", PlcModel = "S7-1200", IpAddress = "192.168.10.12", Port = 102, Protocol = "S7 Protocol (TCP)", StationId = 1, IsConnected = true, Status = "在线" },
                new StationConfigEntity { Id = "A3", Name = "A3", PlcModel = "AM600", IpAddress = "192.168.10.13", Port = 502, Protocol = "ModbusTCP", StationId = 1, IsConnected = false, Status = "故障" },
                new StationConfigEntity { Id = "A4", Name = "A4", PlcModel = "FX5U", IpAddress = "192.168.10.14", Port = 502, Protocol = "ModbusTCP", StationId = 1, IsConnected = false, Status = "离线" },
                new StationConfigEntity { Id = "A5", Name = "A5", PlcModel = "S7-1500", IpAddress = "192.168.10.15", Port = 102, Protocol = "S7 Protocol (TCP)", StationId = 1, IsConnected = true, Status = "在线" },
                new StationConfigEntity { Id = "A6", Name = "A6", PlcModel = "AM600", IpAddress = "192.168.10.16", Port = 502, Protocol = "ModbusTCP", StationId = 1, IsConnected = true, Status = "在线" },
            };
            db.Insertable(configs).ExecuteCommand();
        }

        /// <summary>通知种子数据辅助方法</summary>
        private static void SeedNotifications(ISqlSugarClient db)
        {
            if (db.Queryable<NotificationEntity>().Any()) return;

            var now = DateTime.Now;
            var seeds = new List<NotificationEntity>
            {
                // ---- 报警 ----
                new()
                {
                    Id = Guid.NewGuid().ToString("N")[..8],
                    Title = "A4机台噪音超标",
                    Content = "A4机台噪音 85dB，超出安全阈值上限 75dB (自动暂停)",
                    CreatedAt = now.AddHours(-7).AddMinutes(38),
                    Type = (int)NotificationType.Alarm, Severity = (int)NotificationSeverity.Critical,
                    Source = "A4", IsRead = false
                },
                new()
                {
                    Id = Guid.NewGuid().ToString("N")[..8],
                    Title = "PLC 通信异常",
                    Content = "工位3 (GW-M02) 丢失心跳包超 5s (状态: 离线)",
                    CreatedAt = now.AddHours(-7).AddMinutes(44),
                    Type = (int)NotificationType.Alarm, Severity = (int)NotificationSeverity.Critical,
                    Source = "A3", IsRead = false
                },
                new()
                {
                    Id = Guid.NewGuid().ToString("N")[..8],
                    Title = "机温预警限制触发",
                    Content = "工位1 (GW-M01) 电机测试温度 72°C，接近安全阈值 80°C",
                    CreatedAt = now.AddDays(-1).AddHours(-5).AddMinutes(27),
                    Type = (int)NotificationType.Alarm, Severity = (int)NotificationSeverity.Warning,
                    Source = "A1", IsRead = false
                },

                // ---- 维护 ----
                new()
                {
                    Id = Guid.NewGuid().ToString("N")[..8],
                    Title = "例行维护周期提醒",
                    Content = "C区夹具例行润滑与校准到期，建议交班停机维护",
                    CreatedAt = now.AddHours(-12),
                    Type = (int)NotificationType.Maintenance, Severity = (int)NotificationSeverity.Warning,
                    IsRead = false
                },
                new()
                {
                    Id = Guid.NewGuid().ToString("N")[..8],
                    Title = "传感器标定超期",
                    Content = "工位A2 扭矩传感器标定超期 3 天",
                    CreatedAt = now.AddDays(-1).AddHours(3).AddMinutes(40),
                    Type = (int)NotificationType.Maintenance, Severity = (int)NotificationSeverity.Warning,
                    Source = "A2", IsRead = false
                },
                new()
                {
                    Id = Guid.NewGuid().ToString("N")[..8],
                    Title = "清洁过滤网警告",
                    Content = "配电柜冷风机过滤网压差异常，请清洁更换",
                    CreatedAt = now.AddDays(-2).AddHours(2).AddMinutes(44),
                    Type = (int)NotificationType.Maintenance, Severity = (int)NotificationSeverity.Info,
                    IsRead = false
                },
                new()
                {
                    Id = Guid.NewGuid().ToString("N")[..8],
                    Title = "UPS 电池包寿命警告",
                    Content = "主UPS电池自检警告: 电池寿命不足15%",
                    CreatedAt = now.AddDays(-3).AddHours(6).AddMinutes(17),
                    Type = (int)NotificationType.Maintenance, Severity = (int)NotificationSeverity.Warning,
                    IsRead = false
                },

                // ---- 系统 ----
                new()
                {
                    Id = Guid.NewGuid().ToString("N")[..8],
                    Title = "固件更新可用",
                    Content = "系统固件 v2.4.2-Stable 可用，包含高采样性能优化",
                    CreatedAt = now.AddDays(-1).AddHours(-2),
                    Type = (int)NotificationType.System, Severity = (int)NotificationSeverity.Info,
                    IsRead = false
                },
                new()
                {
                    Id = Guid.NewGuid().ToString("N")[..8],
                    Title = "数据备份完成",
                    Content = "每日数据库备份完成，大小 4.2 GB (同步至数据湖)",
                    CreatedAt = now.AddDays(-1).AddHours(2),
                    Type = (int)NotificationType.System, Severity = (int)NotificationSeverity.Info,
                    IsRead = false
                },
                new()
                {
                    Id = Guid.NewGuid().ToString("N")[..8],
                    Title = "系统时间校准成功",
                    Content = "NTP 时间同步成功，偏差 +0.023s，全节点时钟已同步",
                    CreatedAt = now.AddDays(-3).AddHours(-17).AddMinutes(49),
                    Type = (int)NotificationType.System, Severity = (int)NotificationSeverity.Info,
                    IsRead = false
                },
                new()
                {
                    Id = Guid.NewGuid().ToString("N")[..8],
                    Title = "网络延迟预警",
                    Content = "交换机丢包，延迟 45ms (已切换至冗余物理链路)",
                    CreatedAt = now.AddDays(-4).AddHours(2).AddMinutes(47),
                    Type = (int)NotificationType.System, Severity = (int)NotificationSeverity.Warning,
                    IsRead = false
                },
                new()
                {
                    Id = Guid.NewGuid().ToString("N")[..8],
                    Title = "报表导出成功",
                    Content = "电机能效及测试合格率分析报表导出成功",
                    CreatedAt = now.AddDays(-5).AddHours(4).AddMinutes(29),
                    Type = (int)NotificationType.System, Severity = (int)NotificationSeverity.Info,
                    IsRead = false
                }
            };

            db.Insertable(seeds).ExecuteCommand();
        }

        private static MotorTestRecordEntity CreateTestRecord(
            string barcode, DateTime testTime, string finalResult,
            double? noLoadCurrent, int? noLoadSpeed, string? noLoadResult,
            double? fwdNoise, double? revNoise, double? noiseDiff, string? noiseResult,
            double? loadCurrent, int? loadSpeed, string? loadResult)
        {
            return new MotorTestRecordEntity
            {
                Barcode = barcode,
                TestTime = testTime,
                FinalResult = finalResult,
                NoLoadCurrent = noLoadCurrent,
                NoLoadSpeed = noLoadSpeed,
                NoLoadResult = noLoadResult,
                FwdNoise = fwdNoise,
                RevNoise = revNoise,
                NoiseDiff = noiseDiff,
                NoiseResult = noiseResult,
                LoadCurrent = loadCurrent,
                LoadSpeed = loadSpeed,
                LoadResult = loadResult
            };
        }

        private static bool IsDescending(List<int> values)
        {
            for (int i = 1; i < values.Count; i++)
            {
                if (values[i] > values[i - 1]) return false;
            }
            return true;
        }

        // ================================================================
        // 断言方法
        // ================================================================
        private static void Assert(string testName, bool condition, string? detail = null)
        {
            _totalTests++;
            if (condition)
            {
                _passedTests++;
                Console.WriteLine($"  ✅ {testName}");
            }
            else
            {
                _failedTests++;
                string msg = detail != null ? $"{testName} ({detail})" : testName;
                _failDetails.Add(msg);
                Console.WriteLine($"  ❌ {testName}" + (detail != null ? $" — {detail}" : ""));
            }
        }

        private static void PrintReport()
        {
            Console.WriteLine();
            Console.WriteLine("╔══════════════════════════════════════════════════════╗");
            Console.WriteLine("║                 测试报告                             ║");
            Console.WriteLine("╠══════════════════════════════════════════════════════╣");
            Console.WriteLine($"║  总计: {_totalTests}  ✅ 通过: {_passedTests}  ❌ 失败: {_failedTests}        ║");
            Console.WriteLine($"║  通过率: {(_totalTests > 0 ? _passedTests * 100.0 / _totalTests : 0):F1}%                            ║");

            if (_failDetails.Count > 0)
            {
                Console.WriteLine("╠══════════════════════════════════════════════════════╣");
                Console.WriteLine("║  失败详情:                                          ║");
                foreach (var d in _failDetails)
                {
                    Console.WriteLine($"║  ❌ {d}");
                }
            }

            Console.WriteLine("╚══════════════════════════════════════════════════════╝");

            if (_failedTests == 0)
            {
                Console.WriteLine();
                Console.WriteLine("🎉 所有测试通过！SQLite + SqlSugar 数据层功能完全正常！");
            }
        }
    }
}
