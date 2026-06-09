using System;
using System.CodeDom.Compiler;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using MotorTestSystem.Models;
using MotorTestSystem.Services;

namespace MotorTestSystem.ViewModels;

public class ConfigViewModel : ViewModelBase
{
	private readonly BackendRuntime _runtime;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private AsyncRelayCommand<StationConfig>? testConnectionCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? clearLogsCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? saveAllCommand;

	public ObservableCollection<StationConfig> Stations => _runtime.StationConfigs;

	public ObservableCollection<LogEntry> DiagnosticLogs { get; } = new ObservableCollection<LogEntry>
	{
		new LogEntry
		{
			Timestamp = "14:02:33.105",
			Level = "[信息]",
			Message = "系统启动，网络接口已初始化。",
			LevelBrush = "#10B981"
		},
		new LogEntry
		{
			Timestamp = "14:02:35.021",
			Level = "[连接]",
			Message = "‖ A1 (192.168.10.11:502) ‖‖‖‖",
			LevelBrush = "#12DDF7"
		},
		new LogEntry
		{
			Timestamp = "14:02:38.442",
			Level = "[错误]",
			Message = "‖ A3 (192.168.10.13:502) ‖‖‖‖‖‖‖‖‖‖‖‖‖‖‖‖‖‖‖‖",
			LevelBrush = "#FF5A5F"
		},
		new LogEntry
		{
			Timestamp = "14:03:01.000",
			Level = "[警告]",
			Message = "‖ A4 (192.168.10.14) Ping ‖ > 1000ms‖",
			LevelBrush = "#FFC53D"
		}
	};

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IAsyncRelayCommand<StationConfig> TestConnectionCommand => (IAsyncRelayCommand<StationConfig>)(object)(testConnectionCommand ?? (testConnectionCommand = new AsyncRelayCommand<StationConfig>((Func<StationConfig, Task>)TestConnectionAsync)));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand ClearLogsCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected O, but got Unknown
			//IL_0023: Expected O, but got Unknown
			RelayCommand obj = clearLogsCommand;
			if (obj == null)
			{
				RelayCommand val = new RelayCommand((Action)ClearLogs);
				RelayCommand val2 = val;
				clearLogsCommand = val;
				obj = val2;
			}
			return (IRelayCommand)(object)obj;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand SaveAllCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected O, but got Unknown
			//IL_0023: Expected O, but got Unknown
			RelayCommand obj = saveAllCommand;
			if (obj == null)
			{
				RelayCommand val = new RelayCommand((Action)SaveAll);
				RelayCommand val2 = val;
				saveAllCommand = val;
				obj = val2;
			}
			return (IRelayCommand)(object)obj;
		}
	}

	public ConfigViewModel()
		: this(BackendRuntime.Shared)
	{
	}

	public ConfigViewModel(BackendRuntime runtime)
	{
		_runtime = runtime;
	}

	[RelayCommand]
	private async Task TestConnectionAsync(StationConfig config)
	{
		if (config != null)
		{
			bool isSuccess = await _runtime.PollingService.TestConnectionAsync(config);
			config.IsConnected = isSuccess;
			config.Status = (isSuccess ? "在线" : "离线");
			string time = DateTime.Now.ToString("HH:mm:ss.fff");
			if (isSuccess)
			{
				DiagnosticLogs.Add(new LogEntry
				{
					Timestamp = time,
					Level = "[连接]",
					Message = $"‖ {config.Name} ({config.IpAddress}:{config.Port}) 连接测试成功 ‖‖‖‖",
					LevelBrush = "#12DDF7"
				});
				MessageBox.Show($"{config.Name} ({config.IpAddress}:{config.Port}) 连接正常。", "连接测试成功", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			}
			else
			{
				DiagnosticLogs.Add(new LogEntry
				{
					Timestamp = time,
					Level = "[错误]",
					Message = $"‖ {config.Name} ({config.IpAddress}:{config.Port}) 连接测试失败 ‖‖‖‖",
					LevelBrush = "#FF5A5F"
				});
				MessageBox.Show($"{config.Name} ({config.IpAddress}:{config.Port}) 无法建立连接，请检查 IP、端口、协议与网线连接。", "连接测试失败", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			}
		}
	}

	[RelayCommand]
	private void ClearLogs()
	{
		DiagnosticLogs.Clear();
	}

	[RelayCommand]
	private void SaveAll()
	{
		MessageBox.Show("所有配置已成功保存至系统数据库中。", "保存配置成功", MessageBoxButton.OK, MessageBoxImage.Asterisk);
	}
}
