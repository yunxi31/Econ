using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using MotorTestSystem.ViewModels;

namespace MotorTestSystem;

public class MainWindow : Window, IComponentConnector
{
	private bool _isFullscreen = false;

	private double _normalWidth = 1280.0;

	private double _normalHeight = 920.0;

	private double _normalLeft = 0.0;

	private double _normalTop = 0.0;

	private WindowState _normalState = WindowState.Normal;

	private WindowStyle _normalStyle = WindowStyle.SingleBorderWindow;

	private ResizeMode _normalResizeMode = ResizeMode.CanResize;

	internal Border UserAvatarBorder;

	internal Popup UserCardPopup;

	internal Button LogoutButton;

	private bool _contentLoaded;

	public bool IsLoggingOut { get; private set; } = false;

	public MainWindow()
	{
		InitializeComponent();
		base.DataContext = new MainViewModel();
		base.Loaded += delegate
		{
			_normalWidth = (double.IsNaN(base.Width) ? 1280.0 : base.Width);
			_normalHeight = (double.IsNaN(base.Height) ? 920.0 : base.Height);
			_normalStyle = base.WindowStyle;
			_normalResizeMode = base.ResizeMode;
			_normalState = base.WindowState;
			ToggleFullscreen();
		};
		base.PreviewMouseDoubleClick += MainWindow_PreviewMouseDoubleClick;
		base.PreviewMouseDown += MainWindow_PreviewMouseDown;
		base.Deactivated += delegate
		{
			UserCardPopup.IsOpen = false;
		};
		base.LocationChanged += delegate
		{
			UserCardPopup.IsOpen = false;
		};
		base.SizeChanged += delegate
		{
			UserCardPopup.IsOpen = false;
		};
	}

	private void MainWindow_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
	{
		if (e.ChangedButton != 0 || !_isFullscreen)
		{
			return;
		}
		object originalSource = e.OriginalSource;
		DependencyObject val = (DependencyObject)((originalSource is DependencyObject) ? originalSource : null);
		if (val != null)
		{
			bool flag = IsVisualDescendant(val, (DependencyObject)(object)UserAvatarBorder);
			bool flag2 = UserCardPopup.Child != null && IsVisualDescendant(val, (DependencyObject)(object)UserCardPopup.Child);
			if (flag || flag2)
			{
				return;
			}
		}
		ToggleFullscreen();
		e.Handled = true;
	}

	private void ToggleFullscreen()
	{
		if (!_isFullscreen)
		{
			if (base.WindowStyle != 0)
			{
				_normalWidth = ((base.ActualWidth > 0.0) ? base.ActualWidth : (double.IsNaN(base.Width) ? 1280.0 : base.Width));
				_normalHeight = ((base.ActualHeight > 0.0) ? base.ActualHeight : (double.IsNaN(base.Height) ? 920.0 : base.Height));
				_normalLeft = (double.IsNaN(base.Left) ? 0.0 : base.Left);
				_normalTop = (double.IsNaN(base.Top) ? 0.0 : base.Top);
				_normalState = base.WindowState;
				_normalStyle = base.WindowStyle;
				_normalResizeMode = base.ResizeMode;
			}
			base.WindowStyle = WindowStyle.None;
			base.ResizeMode = ResizeMode.NoResize;
			if (base.WindowState == WindowState.Maximized)
			{
				base.WindowState = WindowState.Normal;
			}
			base.WindowState = WindowState.Maximized;
			_isFullscreen = true;
			return;
		}
		base.WindowStyle = _normalStyle;
		base.ResizeMode = _normalResizeMode;
		base.WindowState = _normalState;
		if (base.WindowState == WindowState.Normal)
		{
			base.Width = ((_normalWidth > 0.0) ? _normalWidth : 1280.0);
			base.Height = ((_normalHeight > 0.0) ? _normalHeight : 920.0);
			if (_normalLeft <= 0.0 || double.IsNaN(_normalLeft))
			{
				base.Left = (SystemParameters.PrimaryScreenWidth - base.Width) / 2.0;
				base.Top = (SystemParameters.PrimaryScreenHeight - base.Height) / 2.0;
			}
			else
			{
				base.Left = _normalLeft;
				base.Top = _normalTop;
			}
		}
		_isFullscreen = false;
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Invalid comparison between Unknown and I4
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Invalid comparison between Unknown and I4
		base.OnKeyDown(e);
		if ((int)e.Key == 100)
		{
			ToggleFullscreen();
			e.Handled = true;
		}
		else if ((int)e.Key == 13 && _isFullscreen)
		{
			ToggleFullscreen();
			e.Handled = true;
		}
	}

	private void UserAvatarBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		UserCardPopup.IsOpen = !UserCardPopup.IsOpen;
		e.Handled = true;
	}

	private void LogoutButton_Click(object sender, RoutedEventArgs e)
	{
		UserCardPopup.IsOpen = false;
		IsLoggingOut = true;
		Close();
	}

	private void MainWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
	{
		if (!UserCardPopup.IsOpen)
		{
			return;
		}
		object originalSource = e.OriginalSource;
		DependencyObject val = (DependencyObject)((originalSource is DependencyObject) ? originalSource : null);
		if (val != null)
		{
			bool flag = IsVisualDescendant(val, (DependencyObject)(object)UserAvatarBorder);
			bool flag2 = UserCardPopup.Child != null && IsVisualDescendant(val, (DependencyObject)(object)UserCardPopup.Child);
			if (!flag && !flag2)
			{
				UserCardPopup.IsOpen = false;
			}
		}
	}

	private bool IsVisualDescendant(DependencyObject child, DependencyObject parent)
	{
		while (child != null)
		{
			if (child == parent)
			{
				return true;
			}
			child = VisualTreeHelper.GetParent(child);
		}
		return false;
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.5.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/MotorTestSystem;component/mainwindow.xaml", UriKind.Relative);
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
			UserAvatarBorder = (Border)target;
			UserAvatarBorder.MouseLeftButtonDown += UserAvatarBorder_MouseLeftButtonDown;
			break;
		case 2:
			UserCardPopup = (Popup)target;
			break;
		case 3:
			LogoutButton = (Button)target;
			LogoutButton.Click += LogoutButton_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
