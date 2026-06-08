using System.Configuration;
using System.Data;
using System.Windows;

namespace MotorTestSystem;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 临时修改关机模式，防止关闭登录窗口导致整个进程直接退出
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var loginWindow = new LoginWindow();
        if (loginWindow.ShowDialog() == true)
        {
            var username = loginWindow.AuthenticatedUser;
            var mainWindow = new MainWindow();
            if (mainWindow.DataContext is ViewModels.MainViewModel mainVM)
            {
                mainVM.CurrentUser = username;
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

