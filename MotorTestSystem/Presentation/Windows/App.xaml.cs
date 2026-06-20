using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using MotorTestSystem.Infrastructure.Logging;
using MotorTestSystem.Models;
using MotorTestSystem.Services;
using Serilog.Events;

namespace MotorTestSystem;

public partial class App : Application
{
    // 防止异常处理递归导致 Stack overflow
    private int _exceptionDepth;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ── 1. 首先初始化结构化日志（其他一切之前）──────────────────────────────
#if DEBUG
        AppLogger.Initialize(minimumLevel: LogEventLevel.Debug);
#else
        AppLogger.Initialize(minimumLevel: LogEventLevel.Information);
#endif

        // ── 2. 注册全局未处理异常 Hook（三处覆盖所有异常逃逸路径）─────────────
        // UI 线程未捕获异常（同步代码 / await 的延续在 UI 线程上抛出）
        DispatcherUnhandledException += OnDispatcherException;

        // 非 UI 线程未捕获异常（后台线程 throw 且没有 catch）
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainException;

        // async/await Task 被 GC 回收前仍未 observed 的异常（最常被遗漏的一处）
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        AppLogger.Root.Info("全局异常处理器注册完毕");

        // ── 3. 启动流程 ───────────────────────────────────────────────────────────
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var loginWindow = new LoginWindow();
        if (loginWindow.ShowDialog() == true)
        {
            var authenticatedUser = loginWindow.AuthenticatedUser;
            var mainWindow = new MainWindow();
            if (mainWindow.DataContext is ViewModels.MainViewModel mainVM)
            {
                mainVM.SetAuthenticatedUser(authenticatedUser);
            }

            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();

            AppLogger.Root.Info("主窗口已打开，认证用户={User}", authenticatedUser?.Account ?? "unknown");
        }
        else
        {
            AppLogger.Root.Info("用户取消登录，程序退出");
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // 释放后端（不阻塞 UI，仅对已完成的 Task 执行 Dispose）
        var task = BackendRuntime.GetSharedAsync();
        if (task.IsCompleted)
        {
            task.Result?.Dispose();
        }

        AppLogger.CloseAndFlush(); // 确保日志缓冲区落盘
        base.OnExit(e);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UI 线程异常：可以 Handled = true 让程序继续运行
    // ─────────────────────────────────────────────────────────────────────────
    private void OnDispatcherException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _exceptionDepth++;
        if (_exceptionDepth > 3)
        {
            // 已递归多次，直接 FailFast 防止栈溢出
            Environment.FailFast(
                $"致命错误（递归异常处理）。原始异常: {e.Exception.GetType().Name}: {e.Exception.Message}");
            return;
        }

        try
        {
            AppLogger.Root.Error(
                e.Exception,
                "[DispatcherException] {ExType}: {Message}",
                e.Exception.GetType().FullName,
                e.Exception.Message);

            MessageBox.Show(
                $"界面线程发生未处理异常：\n{e.Exception.GetType().Name}: {e.Exception.Message}\n\n详细信息已写入 logs/ 目录。",
                "未处理异常",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
            // 连 MessageBox 都失败了，放弃
        }
        finally
        {
            _exceptionDepth--;
        }

        e.Handled = true; // 阻止 WPF 默认崩溃，程序继续运行
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 非 UI 线程异常：IsTerminating = true，进程即将崩溃，只能记录日志
    // ─────────────────────────────────────────────────────────────────────────
    private void OnAppDomainException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is not Exception ex) return;

        try
        {
            AppLogger.Root.Fatal(
                ex,
                "[AppDomainException] IsTerminating={IsTerminating} {ExType}: {Message}",
                e.IsTerminating,
                ex.GetType().FullName,
                ex.Message);

            AppLogger.CloseAndFlush();
        }
        catch { /* 此时环境已不可信，什么都不做 */ }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Unobserved Task 异常：GC 回收 Task 时触发，程序默认不崩溃（.NET 4.5+）
    // 记录日志后标记 SetObserved() 避免升级为进程崩溃
    // ─────────────────────────────────────────────────────────────────────────
    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        try
        {
            AppLogger.Root.Error(
                e.Exception,
                "[UnobservedTask] {ExType}: {Message}",
                e.Exception.GetType().FullName,
                e.Exception.Message);
        }
        catch { }

        e.SetObserved(); // 阻止 .NET 将其升级为 AppDomain 崩溃
    }
}
