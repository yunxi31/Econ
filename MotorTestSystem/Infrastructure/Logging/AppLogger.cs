using System;
using System.IO;
using Serilog;
using Serilog.Events;

namespace MotorTestSystem.Infrastructure.Logging
{
    /// <summary>
    /// Serilog 实现的 IAppLogger。
    /// 日志写入 logs/app-.log（按日滚动），同时输出到 Debug 控制台。
    /// 用法：AppLogger.Initialize() → AppLogger.ForContext&lt;T&gt;() 或 AppLogger.Root
    /// </summary>
    public sealed class SerilogAppLogger : IAppLogger, IDisposable
    {
        private readonly ILogger _logger;

        public SerilogAppLogger(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Debug(string messageTemplate, params object?[] args)
            => _logger.Debug(messageTemplate, args);

        public void Info(string messageTemplate, params object?[] args)
            => _logger.Information(messageTemplate, args);

        public void Information(string messageTemplate, params object?[] args)
            => _logger.Information(messageTemplate, args);

        public void Warning(string messageTemplate, params object?[] args)
            => _logger.Warning(messageTemplate, args);

        public void Warning(Exception ex, string messageTemplate, params object?[] args)
            => _logger.Warning(ex, messageTemplate, args);

        public void Error(string messageTemplate, params object?[] args)
            => _logger.Error(messageTemplate, args);

        public void Error(Exception ex, string messageTemplate, params object?[] args)
            => _logger.Error(ex, messageTemplate, args);

        public void Fatal(Exception ex, string messageTemplate, params object?[] args)
            => _logger.Fatal(ex, messageTemplate, args);

        public void Dispose() => (_logger as IDisposable)?.Dispose();
    }

    /// <summary>
    /// 全局日志 Bootstrap。在 App.OnStartup 最早期调用 Initialize()，
    /// 之后任意位置通过 AppLogger.Root / AppLogger.ForContext() 取实例。
    /// </summary>
    public static class AppLogger
    {
        private static IAppLogger _root = new NullAppLogger();

        /// <summary>全局根日志实例（ForContext 的基础）</summary>
        public static IAppLogger Root => _root;

        /// <summary>
        /// 初始化 Serilog，必须在程序最开始（所有服务构造之前）调用一次。
        /// </summary>
        /// <param name="logDir">日志目录，默认 {BaseDir}/logs</param>
        /// <param name="minimumLevel">最低写入级别，默认 Debug（Release 可改 Information）</param>
        public static void Initialize(string? logDir = null, LogEventLevel minimumLevel = LogEventLevel.Debug)
        {
            logDir ??= Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(logDir);

            var serilog = new LoggerConfiguration()
                .MinimumLevel.Is(minimumLevel)
                // 结构化输出到滚动文件（每天一个文件，保留最近 30 天）
                .WriteTo.File(
                    path: Path.Combine(logDir, "app-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                    shared: false,
                    buffered: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(2))
                // Debug 输出（VS 调试窗口可见）
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                .Enrich.FromLogContext()
                .CreateLogger();

            Log.Logger = serilog; // 注册到 Serilog 全局
            _root = new SerilogAppLogger(serilog);

            _root.Info("=== MotorTestSystem 启动 === LogDir={LogDir}", logDir);
        }

        /// <summary>
        /// 为指定类型获取带 SourceContext 标签的日志实例。
        /// 用法：private static readonly IAppLogger _log = AppLogger.ForContext&lt;MyClass&gt;();
        /// </summary>
        public static IAppLogger ForContext<T>()
            => new SerilogAppLogger(Log.ForContext<T>());

        /// <summary>
        /// 程序退出时调用，确保缓冲区日志全部落盘。
        /// </summary>
        public static void CloseAndFlush()
        {
            _root.Info("=== MotorTestSystem 关闭 ===");
            Log.CloseAndFlush();
        }
    }

    /// <summary>
    /// 测试或初始化之前的空实现，避免空引用。
    /// </summary>
    internal sealed class NullAppLogger : IAppLogger
    {
        public void Debug(string t, params object?[] a) { }
        public void Info(string t, params object?[] a) { }
        public void Information(string t, params object?[] a) { }
        public void Warning(string t, params object?[] a) { }
        public void Warning(Exception ex, string t, params object?[] a) { }
        public void Error(string t, params object?[] a) { }
        public void Error(Exception ex, string t, params object?[] a) { }
        public void Fatal(Exception ex, string t, params object?[] a) { }
    }
}
