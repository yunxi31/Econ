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

        var loginWindow = new LoginWindow();
        if (loginWindow.ShowDialog() == true)
        {
            var username = loginWindow.AuthenticatedUser;
            var mainWindow = new MainWindow();
            if (mainWindow.DataContext is ViewModels.MainViewModel mainVM)
            {
                mainVM.CurrentUser = username;
            }
            mainWindow.Show();
        }
        else
        {
            Shutdown();
        }
    }
}

