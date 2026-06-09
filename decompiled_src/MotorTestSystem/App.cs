using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Windows;
using MotorTestSystem.ViewModels;

namespace MotorTestSystem;

public class App : Application
{
	private bool _contentLoaded;

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		RunLoginLoop();
	}

	private void RunLoginLoop()
	{
		base.ShutdownMode = ShutdownMode.OnExplicitShutdown;
		while (true)
		{
			LoginWindow loginWindow = new LoginWindow();
			if (loginWindow.ShowDialog() == true)
			{
				string authenticatedUser = loginWindow.AuthenticatedUser;
				MainWindow mainWindow = new MainWindow();
				if (mainWindow.DataContext is MainViewModel mainViewModel)
				{
					mainViewModel.CurrentUser = authenticatedUser;
				}
				base.MainWindow = mainWindow;
				mainWindow.ShowDialog();
				if (mainWindow.IsLoggingOut)
				{
					base.MainWindow = null;
					continue;
				}
				break;
			}
			break;
		}
		Shutdown();
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.5.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/MotorTestSystem;component/app.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[STAThread]
	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.5.0")]
	public static void Main()
	{
		App app = new App();
		app.InitializeComponent();
		app.Run();
	}
}
