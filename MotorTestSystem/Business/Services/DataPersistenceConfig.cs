using System;
using System.IO;
using System.Text.Json;

namespace MotorTestSystem.Services
{
    /// <summary>
    /// 数据持久化配置 — 从 appsettings.json 加载，或使用默认值。
    /// </summary>
    public sealed class DataPersistenceConfig
    {
        /// <summary>写入通道容量（默认 500）</summary>
        public int WriteChannelCapacity { get; set; } = 500;

        /// <summary>写入重试次数（默认 3）</summary>
        public int MaxWriteRetryCount { get; set; } = 3;

        /// <summary>单次写入超时秒数（默认 10s）</summary>
        public int FlushTimeoutSeconds { get; set; } = 10;

        /// <summary>SQLite 同步模式（默认 NORMAL）</summary>
        public string SQLiteSyncMode { get; set; } = "NORMAL";

        /// <summary>启用长连接（默认 false）</summary>
        public bool SQLiteLongConnection { get; set; } = false;

        /// <summary>CloudSync 启用（默认 false）</summary>
        public bool CloudSyncEnabled { get; set; } = false;

        /// <summary>CloudSync 端点（默认空）</summary>
        public string? CloudSyncEndpoint { get; set; }

        /// <summary>死信队列目录（默认 null = 使用默认路径）</summary>
        public string? DeadLetterQueuePath { get; set; }

        /// <summary>从 appsettings.json 加载配置（如果文件存在）</summary>
        public static DataPersistenceConfig Load()
        {
            string configPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

            if (!File.Exists(configPath))
            {
                return new DataPersistenceConfig();
            }

            try
            {
                string json = File.ReadAllText(configPath);
                var parsed = JsonSerializer.Deserialize<DataPersistenceConfigFile>(json);
                if (parsed?.DataPersistence == null) return new DataPersistenceConfig();

                var c = parsed.DataPersistence;
                return new DataPersistenceConfig
                {
                    WriteChannelCapacity = c.WriteChannelCapacity > 0 ? c.WriteChannelCapacity : 500,
                    MaxWriteRetryCount = c.MaxWriteRetryCount > 0 ? c.MaxWriteRetryCount : 3,
                    FlushTimeoutSeconds = c.FlushTimeoutSeconds > 0 ? c.FlushTimeoutSeconds : 10,
                    SQLiteSyncMode = !string.IsNullOrWhiteSpace(c.SQLiteSyncMode) ? c.SQLiteSyncMode : "NORMAL",
                    SQLiteLongConnection = c.SQLiteLongConnection,
                    CloudSyncEnabled = c.CloudSyncEnabled,
                    CloudSyncEndpoint = string.IsNullOrWhiteSpace(c.CloudSyncEndpoint) ? null : c.CloudSyncEndpoint,
                    DeadLetterQueuePath = string.IsNullOrWhiteSpace(c.DeadLetterQueuePath) ? null : c.DeadLetterQueuePath
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Failed to load DataPersistence config: {ex.Message}");
                return new DataPersistenceConfig();
            }
        }

        private sealed class DataPersistenceConfigFile
        {
            public DataPersistenceSection? DataPersistence { get; set; }
        }

        private sealed class DataPersistenceSection
        {
            public int WriteChannelCapacity { get; set; }
            public int MaxWriteRetryCount { get; set; }
            public int FlushTimeoutSeconds { get; set; }
            public string? SQLiteSyncMode { get; set; }
            public bool SQLiteLongConnection { get; set; }
            public bool CloudSyncEnabled { get; set; }
            public string? CloudSyncEndpoint { get; set; }
            public string? DeadLetterQueuePath { get; set; }
        }
    }
}
