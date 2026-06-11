using SqlSugar;
using System;
using System.IO;
using MotorTestSystem.Models;
using MotorTestSystem.Models.Entities;

namespace MotorTestSystem.Services
{
    /// <summary>
    /// SqlSugar 数据库上下文 — 管理 SQLite 连接和初始化
    /// </summary>
    public class SqlSugarDbContext
    {
        private static readonly object _initLock = new();
        private static bool _initialized;

        public ISqlSugarClient Db { get; }

        /// <summary>
        /// 数据库文件路径（默认在应用程序目录下 Data/MotorTest.db）
        /// </summary>
        public static string DbPath { get; } = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Data", "MotorTest.db");

        public SqlSugarDbContext()
        {
            // 确保数据目录存在
            var dir = Path.GetDirectoryName(DbPath)!;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            Db = new SqlSugarScope(new ConnectionConfig
            {
                ConnectionString = $"Data Source={DbPath};",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,   // 自动释放连接
                InitKeyType = InitKeyType.Attribute, // 从特性读取主键
                MoreSettings = new ConnMoreSettings
                {
                    IsAutoRemoveDataCache = true  // 自动清理缓存
                },
                ConfigureExternalServices = new ConfigureExternalServices
                {
                    EntityNameService = (type, entity) =>
                    {
                        // 统一处理：枚举存储为 int
                    },
                    EntityService = (property, column) =>
                    {
                        // SQLite 不支持 decimal，统一用 double
                        if (column.DataType == "decimal")
                        {
                            column.DataType = "REAL";
                        }
                    }
                }
            },
            config =>
            {
                // SQL 执行日志（调试用）
                config.Aop.OnLogExecuting = (sql, pars) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[SQL] {sql}");
                };
            });

            InitializeDatabase();
        }

        /// <summary>
        /// 初始化数据库：建表 + 种子数据
        /// </summary>
        private void InitializeDatabase()
        {
            lock (_initLock)
            {
                if (_initialized) return;

                // 建表（如果不存在）
                Db.CodeFirst.InitTables(
                    typeof(MotorTestRecordEntity),
                    typeof(UserEntity),
                    typeof(StationConfigEntity),
                    typeof(NotificationEntity)
                );

                // 种子数据（仅在表为空时插入）
                SeedIfEmpty();

                _initialized = true;
            }
        }

        private void SeedIfEmpty()
        {
            // ---- 用户种子 ----
            if (!Db.Queryable<UserEntity>().Any())
            {
                SeedUsers();
            }

            // ---- 工位配置种子 ----
            if (!Db.Queryable<StationConfigEntity>().Any())
            {
                SeedStationConfigs();
            }
        }

        private void SeedUsers()
        {
            var now = DateTime.Now;
            var users = new[]
            {
                // 管理员
                CreateSeedUser("U00001", "admin", "系统管理员", "admin123", AppRole.Admin, UserStatus.Active, now.AddHours(-1), now.AddMonths(-6)),
                CreateSeedUser("U00002", "ad_liwei", "李威", "admin123", AppRole.Admin, UserStatus.Active, now.AddDays(-3), now.AddMonths(-3)),
                CreateSeedUser("U00003", "ad_sunyan", "孙燕", "admin123", AppRole.Admin, UserStatus.Disabled, null, now.AddMonths(-8)),

                // 操作员
                CreateSeedUser("U00004", "operator", "默认操作员", "123", AppRole.Operator, UserStatus.Active, now.AddMinutes(-10), now.AddMonths(-5)),
                CreateSeedUser("U00005", "op_zhangwei", "张伟", "123", AppRole.Operator, UserStatus.Active, now.AddHours(-3), now.AddMonths(-4)),
                CreateSeedUser("U00006", "op_lina", "李娜", "123", AppRole.Operator, UserStatus.Active, now.AddDays(-1), now.AddMonths(-3)),
                CreateSeedUser("U00007", "op_zhaolei", "赵雷", "123", AppRole.Operator, UserStatus.Disabled, now.AddDays(-30), now.AddMonths(-2)),
                CreateSeedUser("U00008", "op_chenjing", "陈静", "123", AppRole.Operator, UserStatus.Active, now.AddMinutes(-45), now.AddMonths(-1)),
                CreateSeedUser("U00009", "op_zhoumei", "周梅", "123", AppRole.Operator, UserStatus.Active, now.AddHours(-8), now.AddMonths(-2)),
                CreateSeedUser("U00010", "op_wugang", "吴刚", "123", AppRole.Operator, UserStatus.Active, null, now.AddDays(-14)),
                CreateSeedUser("U00011", "op_huangli", "黄丽", "123", AppRole.Operator, UserStatus.Disabled, now.AddDays(-7), now.AddDays(-14)),

                // 维护员
                CreateSeedUser("U00012", "maintainer", "默认维护员", "maint123", AppRole.Maintainer, UserStatus.Active, now.AddHours(-2), now.AddMonths(-6)),
                CreateSeedUser("U00013", "mt_wangqiang", "王强", "maint123", AppRole.Maintainer, UserStatus.Active, now.AddHours(-5), now.AddMonths(-4)),
                CreateSeedUser("U00014", "mt_liuyang", "刘洋", "maint123", AppRole.Maintainer, UserStatus.Active, now.AddDays(-2), now.AddMonths(-3)),
                CreateSeedUser("U00015", "mt_zhaomin", "赵敏", "maint123", AppRole.Maintainer, UserStatus.Active, now.AddMinutes(-20), now.AddMonths(-1)),
                CreateSeedUser("U00016", "mt_chenhao", "陈昊", "maint123", AppRole.Maintainer, UserStatus.Disabled, now.AddDays(-15), now.AddMonths(-2)),
            };

            Db.Insertable(users).ExecuteCommand();
        }

        private static UserEntity CreateSeedUser(
            string id, string account, string name, string password,
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

        private void SeedStationConfigs()
        {
            var configs = new[]
            {
                new StationConfigEntity { Id = "A1", Name = "A1", PlcModel = "FX5U", IpAddress = "192.168.10.11", Port = 502, Protocol = "ModbusTCP", StationId = 1, IsConnected = true, Status = "在线" },
                new StationConfigEntity { Id = "A2", Name = "A2", PlcModel = "S7-1200", IpAddress = "192.168.10.12", Port = 102, Protocol = "S7 Protocol (TCP)", StationId = 1, IsConnected = true, Status = "在线" },
                new StationConfigEntity { Id = "A3", Name = "A3", PlcModel = "AM600", IpAddress = "192.168.10.13", Port = 502, Protocol = "ModbusTCP", StationId = 1, IsConnected = false, Status = "故障" },
                new StationConfigEntity { Id = "A4", Name = "A4", PlcModel = "FX5U", IpAddress = "192.168.10.14", Port = 502, Protocol = "ModbusTCP", StationId = 1, IsConnected = false, Status = "离线" },
                new StationConfigEntity { Id = "A5", Name = "A5", PlcModel = "S7-1500", IpAddress = "192.168.10.15", Port = 102, Protocol = "S7 Protocol (TCP)", StationId = 1, IsConnected = true, Status = "在线" },
                new StationConfigEntity { Id = "A6", Name = "A6", PlcModel = "AM600", IpAddress = "192.168.10.16", Port = 502, Protocol = "ModbusTCP", StationId = 1, IsConnected = true, Status = "在线" },
            };

            Db.Insertable(configs).ExecuteCommand();
        }
    }
}
