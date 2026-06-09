using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using MotorTestSystem.Models;
using MotorTestSystem.Services;

namespace MotorTestSystem.ViewModels;

public class MainViewModel : ViewModelBase
{
	private readonly BackendRuntime _runtime;

	private readonly Dictionary<string, bool> _onlineStations = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

	private readonly DispatcherTimer _clockTimer;

	[ObservableProperty]
	private ViewModelBase _currentView;

	[ObservableProperty]
	private string _currentTime = DateTime.UtcNow.ToString("HH:mm:ss");

	[ObservableProperty]
	private string _currentUser = "管理员";

	[ObservableProperty]
	private string _currentUserName = "admin";

	[ObservableProperty]
	private string _currentUserRole = "管理员";

	[ObservableProperty]
	private int _onlineStationCount;

	[ObservableProperty]
	private int _totalStationCount;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand<string>? navigateCommand;

	public DashboardViewModel DashboardVM { get; }

	public MonitorViewModel MonitorVM { get; }

	public HistoryViewModel HistoryVM { get; }

	public ConfigViewModel ConfigVM { get; }

	public UserViewModel UserVM { get; }

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public ViewModelBase CurrentView
	{
		get
		{
			return _currentView;
		}
		[MemberNotNull("_currentView")]
		set
		{
			if (!EqualityComparer<ViewModelBase>.Default.Equals(_currentView, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CurrentView);
				_currentView = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CurrentView);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string CurrentTime
	{
		get
		{
			return _currentTime;
		}
		[MemberNotNull("_currentTime")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_currentTime, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CurrentTime);
				_currentTime = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CurrentTime);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string CurrentUser
	{
		get
		{
			return _currentUser;
		}
		[MemberNotNull("_currentUser")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_currentUser, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CurrentUser);
				_currentUser = value;
				OnCurrentUserChanged(value);
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CurrentUser);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string CurrentUserName
	{
		get
		{
			return _currentUserName;
		}
		[MemberNotNull("_currentUserName")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_currentUserName, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CurrentUserName);
				_currentUserName = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CurrentUserName);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string CurrentUserRole
	{
		get
		{
			return _currentUserRole;
		}
		[MemberNotNull("_currentUserRole")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_currentUserRole, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CurrentUserRole);
				_currentUserRole = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CurrentUserRole);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int OnlineStationCount
	{
		get
		{
			return _onlineStationCount;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(_onlineStationCount, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.OnlineStationCount);
				_onlineStationCount = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.OnlineStationCount);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int TotalStationCount
	{
		get
		{
			return _totalStationCount;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(_totalStationCount, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.TotalStationCount);
				_totalStationCount = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.TotalStationCount);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<string> NavigateCommand => (IRelayCommand<string>)(object)(navigateCommand ?? (navigateCommand = new RelayCommand<string>((Action<string>)Navigate)));

	public MainViewModel()
		: this(BackendRuntime.Shared)
	{
	}

	public MainViewModel(BackendRuntime runtime)
	{
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Expected O, but got Unknown
		_runtime = runtime;
		TotalStationCount = _runtime.StationConfigs.Count;
		OnlineStationCount = 0;
		DashboardVM = new DashboardViewModel(_runtime.Repository);
		MonitorVM = new MonitorViewModel(_runtime);
		HistoryVM = new HistoryViewModel(_runtime.Repository);
		ConfigVM = new ConfigViewModel(_runtime);
		UserVM = new UserViewModel();
		_currentView = MonitorVM;
		_runtime.PollingService.SnapshotReceived += OnSnapshotReceived;
		_clockTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(1.0)
		};
		_clockTimer.Tick += delegate
		{
			CurrentTime = DateTime.UtcNow.ToString("HH:mm:ss");
		};
		_clockTimer.Start();
	}

	[RelayCommand]
	private void Navigate(string destination)
	{
		if (1 == 0)
		{
		}
		ViewModelBase currentView = destination switch
		{
			"Dashboard" => DashboardVM, 
			"Monitor" => MonitorVM, 
			"History" => HistoryVM, 
			"Config" => ConfigVM, 
			"User" => UserVM, 
			_ => DashboardVM, 
		};
		if (1 == 0)
		{
		}
		CurrentView = currentView;
	}

	private void OnSnapshotReceived(object? sender, StationSnapshot snapshot)
	{
		Application current = Application.Current;
		Dispatcher val = ((current != null) ? ((DispatcherObject)current).Dispatcher : null);
		if (val == null || val.CheckAccess())
		{
			ApplyOnlineState(snapshot);
			return;
		}
		val.InvokeAsync((Action)delegate
		{
			ApplyOnlineState(snapshot);
		});
	}

	private void ApplyOnlineState(StationSnapshot snapshot)
	{
		_onlineStations[snapshot.StationId] = snapshot.IsOnline;
		OnlineStationCount = _onlineStations.Count<KeyValuePair<string, bool>>((KeyValuePair<string, bool> kvp) => kvp.Value);
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	private void OnCurrentUserChanged(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			CurrentUserRole = "未知角色";
			CurrentUserName = "未知用户";
			return;
		}
		int num = value.IndexOf('(');
		if (num > 0)
		{
			CurrentUserRole = value.Substring(0, num).Trim();
			CurrentUserName = value.Substring(num + 1).Replace(")", "").Trim();
		}
		else
		{
			CurrentUserRole = value;
			CurrentUserName = value;
		}
	}
}
