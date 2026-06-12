using System;
using System.Windows;
using System.Windows.Threading;
using MotorTestSystem.Models;
using MotorTestSystem.Services;

namespace MotorTestSystem;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 全局未处理异常捕获（调试用）
        this.DispatcherUnhandledException += (s, ex) =>
        {
            MessageBox.Show(
                $"[DispatcherException]\n{ex.Exception.GetType().Name}: {ex.Exception.Message}\n\nStackTrace:\n{ex.Exception.StackTrace}",
                "未处理异常", MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true; // 阻止进程退出，方便查看
        };

        AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
        {
            if (ex.ExceptionObject is Exception exception)
            {
                MessageBox.Show(
                    $"[AppDomainException]\n{exception.GetType().Name}: {exception.Message}\n\nStackTrace:\n{exception.StackTrace}",
                    "致命异常", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };

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
}
