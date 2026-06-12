using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using MotorTestSystem.Models;
using MotorTestSystem.Services;

namespace MotorTestSystem;

public partial class App : Application
{
    private int _exceptionDepth; // 防止异常处理递归导致 Stack overflow

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 全局未处理异常捕获
        this.DispatcherUnhandledException += OnDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainException;

        // 临时修改关机模式，防止关闭登录窗口导致整个进程直接退出
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var loginWindow = new LoginWindow();
        if (loginWindow.ShowDialog() == true)
        {
            var authenticatedUser = loginWindow.AuthenticatedUser;
            var mainWindow = new MainWindow();
            if (mainWindow.DataContext is ViewModels.MainViewModel mainVM)
            {
                // 将认证用户信息传递给 MainViewModel
                mainVM.SetAuthenticatedUser(authenticatedUser);
            }
            
            // 绑定主窗口，并恢复默认的关机模式（主窗口关闭时退出程序）
            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            
            mainWindow.Show();
        }
        else
        {
            Shutdown();
        }
    }

    private void OnDispatcherException(object? sender, DispatcherUnhandledExceptionEventArgs ex)
    {
        _exceptionDepth++;
        if (_exceptionDepth > 3)
        {
            // 已递归多次，直接退出防止 Stack overflow
            Environment.FailFast($"致命错误（递归异常处理），请检查日志文件。\n原始异常: {ex.Exception.GetType().Name}: {ex.Exception.Message}");
            return;
        }

        try
        {
            // 先写入日志文件（不依赖 UI）
            var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(logDir);
            var logFile = Path.Combine(logDir, $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            File.WriteAllText(logFile,
                $"=== {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n" +
                $"异常类型: {ex.Exception.GetType().FullName}\n" +
                $"异常消息: {ex.Exception.Message}\n" +
                $"堆栈跟踪:\n{ex.Exception.StackTrace}\n" +
                $"内部异常: {ex.Exception.InnerException}\n");
        }
        catch
        {
            // 写日志也失败，放弃
        }

        try
        {
            // 尝试显示简单消息框（不含完整堆栈，避免 StringBuilder.ToString 溢出）
            MessageBox.Show(
                $"[DispatcherException]\n{ex.Exception.GetType().Name}: {ex.Exception.Message}\n\n详细信息已保存至 logs/ 目录。",
                "未处理异常", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch
        {
            // 消息框也失败了（递归到了），放弃
        }

        ex.Handled = true;
    }

    private void OnAppDomainException(object? sender, UnhandledExceptionEventArgs ex)
    {
        if (ex.ExceptionObject is Exception exception)
        {
            try
            {
                var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                Directory.CreateDirectory(logDir);
                var logFile = Path.Combine(logDir, $"fatal_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                File.WriteAllText(logFile,
                    $"=== {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n" +
                    $"异常类型: {exception.GetType().FullName}\n" +
                    $"异常消息: {exception.Message}\n" +
                    $"堆栈跟踪:\n{exception.StackTrace}\n");
            }
            catch { }

            try
            {
                MessageBox.Show(
                    $"[AppDomainException]\n{exception.GetType().Name}: {exception.Message}\n\n详细信息已保存至 logs/ 目录。",
                    "致命异常", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { }
        }
    }
}
