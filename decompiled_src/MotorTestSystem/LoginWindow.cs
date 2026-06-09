using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace MotorTestSystem;

public class LoginWindow : Window, IComponentConnector
{
	internal ComboBox RoleComboBox;

	internal TextBox UsernameTextBox;

	internal PasswordBox PasswordBox;

	internal TextBlock ErrorTextBlock;

	private bool _contentLoaded;

	public string AuthenticatedUser { get; private set; } = "操作员";

	public LoginWindow()
	{
		InitializeComponent();
		UpdateUsernameDefault();
	}

	private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ChangedButton == MouseButton.Left)
		{
			DragMove();
		}
	}

	private void CloseButton_Click(object sender, RoutedEventArgs e)
	{
		base.DialogResult = false;
		Close();
	}

	private void RoleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (base.IsLoaded)
		{
			UpdateUsernameDefault();
		}
	}

	private void UpdateUsernameDefault()
	{
		if (UsernameTextBox != null)
		{
			switch (RoleComboBox.SelectedIndex)
			{
			case 0:
				UsernameTextBox.Text = "operator";
				PasswordBox.Password = "";
				break;
			case 1:
				UsernameTextBox.Text = "technician";
				PasswordBox.Password = "";
				break;
			case 2:
				UsernameTextBox.Text = "admin";
				PasswordBox.Password = "";
				break;
			}
			ErrorTextBlock.Visibility = Visibility.Collapsed;
		}
	}

	private void LoginButton_Click(object sender, RoutedEventArgs e)
	{
		PerformLogin();
	}

	private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Invalid comparison between Unknown and I4
		if ((int)e.Key == 6)
		{
			PerformLogin();
		}
	}

	private void PerformLogin()
	{
		string text = UsernameTextBox.Text.Trim();
		string password = PasswordBox.Password;
		if (string.IsNullOrEmpty(text))
		{
			ShowError("请输入用户名！");
			return;
		}
		bool flag = false;
		string authenticatedUser = "";
		switch (RoleComboBox.SelectedIndex)
		{
		case 0:
			if (string.IsNullOrEmpty(password) || password == "op123" || password == "123")
			{
				flag = true;
				authenticatedUser = "操作员 (" + text + ")";
			}
			else
			{
				ShowError("操作员密码错误！(提示：可为空或输入 123)");
			}
			break;
		case 1:
			if (password == "tech123" || password == "456")
			{
				flag = true;
				authenticatedUser = "技术员 (" + text + ")";
			}
			else
			{
				ShowError("技术员密码错误！(提示：tech123 或 456)");
			}
			break;
		case 2:
			if (password == "admin123" || password == "789")
			{
				flag = true;
				authenticatedUser = "管理员 (" + text + ")";
			}
			else
			{
				ShowError("管理员密码错误！(提示：admin123 或 789)");
			}
			break;
		}
		if (flag)
		{
			AuthenticatedUser = authenticatedUser;
			base.DialogResult = true;
			Close();
		}
	}

	private void ShowError(string message)
	{
		ErrorTextBlock.Text = message;
		ErrorTextBlock.Visibility = Visibility.Visible;
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.5.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/MotorTestSystem;component/loginwindow.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.5.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 1:
			((Grid)target).MouseLeftButtonDown += TitleBar_MouseLeftButtonDown;
			break;
		case 2:
			((Button)target).Click += CloseButton_Click;
			break;
		case 3:
			RoleComboBox = (ComboBox)target;
			RoleComboBox.SelectionChanged += RoleComboBox_SelectionChanged;
			break;
		case 4:
			UsernameTextBox = (TextBox)target;
			break;
		case 5:
			PasswordBox = (PasswordBox)target;
			PasswordBox.KeyDown += PasswordBox_KeyDown;
			break;
		case 6:
			ErrorTextBlock = (TextBlock)target;
			break;
		case 7:
			((Button)target).Click += LoginButton_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
