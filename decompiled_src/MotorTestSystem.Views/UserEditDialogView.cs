using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using MotorTestSystem.ViewModels;

namespace MotorTestSystem.Views;

public class UserEditDialogView : UserControl, IComponentConnector
{
	internal PasswordBox PbPassword;

	internal PasswordBox PbConfirmPassword;

	private bool _contentLoaded;

	public UserEditDialogView()
	{
		InitializeComponent();
	}

	private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ChangedButton == MouseButton.Left)
		{
			Window.GetWindow((DependencyObject)(object)this)?.DragMove();
		}
	}

	private void Cancel_Click(object sender, RoutedEventArgs e)
	{
		Window window = Window.GetWindow((DependencyObject)(object)this);
		if (window != null)
		{
			window.DialogResult = false;
			window.Close();
		}
	}

	private void Save_Click(object sender, RoutedEventArgs e)
	{
		Window window = Window.GetWindow((DependencyObject)(object)this);
		if (window == null || !(base.DataContext is UserEditDialogViewModel userEditDialogViewModel))
		{
			return;
		}
		userEditDialogViewModel.Password = PbPassword.Password;
		userEditDialogViewModel.ConfirmPassword = PbConfirmPassword.Password;
		if (string.IsNullOrWhiteSpace(userEditDialogViewModel.Account))
		{
			MessageBox.Show("账号不能为空！", "输入验证", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		if (string.IsNullOrWhiteSpace(userEditDialogViewModel.Name))
		{
			MessageBox.Show("姓名不能为空！", "输入验证", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		if (userEditDialogViewModel.Title.Contains("新增"))
		{
			if (string.IsNullOrEmpty(userEditDialogViewModel.Password))
			{
				MessageBox.Show("请输入登录密码！", "输入验证", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return;
			}
			if (userEditDialogViewModel.Password != userEditDialogViewModel.ConfirmPassword)
			{
				MessageBox.Show("两次输入的密码不一致！", "输入验证", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return;
			}
		}
		else if (!string.IsNullOrEmpty(userEditDialogViewModel.Password) && userEditDialogViewModel.Password != userEditDialogViewModel.ConfirmPassword)
		{
			MessageBox.Show("两次输入的密码不一致！", "输入验证", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		window.DialogResult = true;
		window.Close();
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.5.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/MotorTestSystem;component/views/usereditdialogview.xaml", UriKind.Relative);
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
			((Button)target).Click += Cancel_Click;
			break;
		case 3:
			PbPassword = (PasswordBox)target;
			break;
		case 4:
			PbConfirmPassword = (PasswordBox)target;
			break;
		case 5:
			((Button)target).Click += Cancel_Click;
			break;
		case 6:
			((Button)target).Click += Save_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
