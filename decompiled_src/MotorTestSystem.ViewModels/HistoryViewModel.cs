using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using MotorTestSystem.Services;

namespace MotorTestSystem.ViewModels;

public class HistoryViewModel : ViewModelBase
{
	private readonly IMotorTestRepository _repository;

	[ObservableProperty]
	private string _searchBarcode = string.Empty;

	[ObservableProperty]
	private string _selectedResultFilter = "全部";

	[ObservableProperty]
	private DateTime _startDate = DateTime.Now.AddDays(-7.0);

	[ObservableProperty]
	private DateTime _endDate = DateTime.Now;

	[ObservableProperty]
	private int _totalTestsCount = 30;

	[ObservableProperty]
	private int _passedCount = 7;

	[ObservableProperty]
	private int _failedCount = 23;

	[ObservableProperty]
	private string _passRateString = "23.33%";

	[ObservableProperty]
	private int _currentPage = 1;

	[ObservableProperty]
	private int _totalPages = 3;

	[ObservableProperty]
	private int _pageSize = 10;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? searchCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? previousPageCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? nextPageCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? resetCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? exportCommand;

	public List<string> ResultFilters { get; } = new List<string> { "全部", "OK", "NG" };

	public List<string> StationStatuses { get; } = new List<string> { "OK", "OK", "OK", "OK", "OK" };

	public ObservableCollection<MotorTestRecordModel> TestResults { get; } = new ObservableCollection<MotorTestRecordModel>();

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string SearchBarcode
	{
		get
		{
			return _searchBarcode;
		}
		[MemberNotNull("_searchBarcode")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_searchBarcode, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SearchBarcode);
				_searchBarcode = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SearchBarcode);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string SelectedResultFilter
	{
		get
		{
			return _selectedResultFilter;
		}
		[MemberNotNull("_selectedResultFilter")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_selectedResultFilter, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedResultFilter);
				_selectedResultFilter = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedResultFilter);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public DateTime StartDate
	{
		get
		{
			return _startDate;
		}
		set
		{
			if (!EqualityComparer<DateTime>.Default.Equals(_startDate, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.StartDate);
				_startDate = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.StartDate);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public DateTime EndDate
	{
		get
		{
			return _endDate;
		}
		set
		{
			if (!EqualityComparer<DateTime>.Default.Equals(_endDate, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.EndDate);
				_endDate = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.EndDate);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int TotalTestsCount
	{
		get
		{
			return _totalTestsCount;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(_totalTestsCount, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.TotalTestsCount);
				_totalTestsCount = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.TotalTestsCount);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int PassedCount
	{
		get
		{
			return _passedCount;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(_passedCount, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.PassedCount);
				_passedCount = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.PassedCount);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int FailedCount
	{
		get
		{
			return _failedCount;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(_failedCount, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.FailedCount);
				_failedCount = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.FailedCount);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string PassRateString
	{
		get
		{
			return _passRateString;
		}
		[MemberNotNull("_passRateString")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_passRateString, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.PassRateString);
				_passRateString = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.PassRateString);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int CurrentPage
	{
		get
		{
			return _currentPage;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(_currentPage, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CurrentPage);
				_currentPage = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CurrentPage);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int TotalPages
	{
		get
		{
			return _totalPages;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(_totalPages, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.TotalPages);
				_totalPages = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.TotalPages);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int PageSize
	{
		get
		{
			return _pageSize;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(_pageSize, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.PageSize);
				_pageSize = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.PageSize);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand SearchCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected O, but got Unknown
			//IL_0023: Expected O, but got Unknown
			RelayCommand obj = searchCommand;
			if (obj == null)
			{
				RelayCommand val = new RelayCommand((Action)Search);
				RelayCommand val2 = val;
				searchCommand = val;
				obj = val2;
			}
			return (IRelayCommand)(object)obj;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand PreviousPageCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected O, but got Unknown
			//IL_0023: Expected O, but got Unknown
			RelayCommand obj = previousPageCommand;
			if (obj == null)
			{
				RelayCommand val = new RelayCommand((Action)PreviousPage);
				RelayCommand val2 = val;
				previousPageCommand = val;
				obj = val2;
			}
			return (IRelayCommand)(object)obj;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand NextPageCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected O, but got Unknown
			//IL_0023: Expected O, but got Unknown
			RelayCommand obj = nextPageCommand;
			if (obj == null)
			{
				RelayCommand val = new RelayCommand((Action)NextPage);
				RelayCommand val2 = val;
				nextPageCommand = val;
				obj = val2;
			}
			return (IRelayCommand)(object)obj;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand ResetCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected O, but got Unknown
			//IL_0023: Expected O, but got Unknown
			RelayCommand obj = resetCommand;
			if (obj == null)
			{
				RelayCommand val = new RelayCommand((Action)Reset);
				RelayCommand val2 = val;
				resetCommand = val;
				obj = val2;
			}
			return (IRelayCommand)(object)obj;
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand ExportCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected O, but got Unknown
			//IL_0023: Expected O, but got Unknown
			RelayCommand obj = exportCommand;
			if (obj == null)
			{
				RelayCommand val = new RelayCommand((Action)Export);
				RelayCommand val2 = val;
				exportCommand = val;
				obj = val2;
			}
			return (IRelayCommand)(object)obj;
		}
	}

	public HistoryViewModel()
		: this(BackendRuntime.Shared.Repository)
	{
	}

	public HistoryViewModel(IMotorTestRepository repository)
	{
		_repository = repository;
		LoadMockData();
	}

	private void LoadMockData()
	{
		TestResults.Clear();
		TestResults.Add(new MotorTestRecordModel
		{
			Barcode = "M260608001",
			TestTime = DateTime.Now.AddMinutes(-5.0),
			FinalResult = "OK",
			NoLoadCurrent = 1.24,
			NoLoadSpeed = 3025.0,
			FwdNoise = 55.4,
			RevNoise = 56.1,
			LoadCurrent = 4.12,
			LoadSpeed = 2980.0
		});
		TestResults.Add(new MotorTestRecordModel
		{
			Barcode = "M260608002",
			TestTime = DateTime.Now.AddMinutes(-12.0),
			FinalResult = "NG",
			NoLoadCurrent = 1.85,
			NoLoadSpeed = 3050.0,
			FwdNoise = 68.5,
			RevNoise = 69.2,
			LoadCurrent = 4.85,
			LoadSpeed = 2950.0
		});
		TestResults.Add(new MotorTestRecordModel
		{
			Barcode = "M260608003",
			TestTime = DateTime.Now.AddMinutes(-20.0),
			FinalResult = "NG",
			NoLoadCurrent = null,
			NoLoadSpeed = null,
			FwdNoise = null,
			RevNoise = null,
			LoadCurrent = null,
			LoadSpeed = null
		});
		for (int i = 4; i <= 10; i++)
		{
			TestResults.Add(new MotorTestRecordModel
			{
				Barcode = $"M26060800{i}",
				TestTime = DateTime.Now.AddHours(-i),
				FinalResult = ((i % 3 == 0) ? "OK" : "NG"),
				NoLoadCurrent = ((i % 3 == 0) ? 1.22 : 1.76),
				NoLoadSpeed = 3010.0,
				FwdNoise = ((i % 3 == 0) ? 54.2 : 62.4),
				RevNoise = 55.0,
				LoadCurrent = ((i % 3 == 0) ? new double?(4.05) : ((double?)null)),
				LoadSpeed = ((i % 3 == 0) ? new double?(2990.0) : ((double?)null))
			});
		}
	}

	[RelayCommand]
	private void Search()
	{
		LoadMockData();
	}

	[RelayCommand]
	private void PreviousPage()
	{
		if (CurrentPage > 1)
		{
			CurrentPage--;
		}
	}

	[RelayCommand]
	private void NextPage()
	{
		if (CurrentPage < TotalPages)
		{
			CurrentPage++;
		}
	}

	[RelayCommand]
	private void Reset()
	{
		SearchBarcode = string.Empty;
		SelectedResultFilter = "全部";
		StartDate = DateTime.Now.AddDays(-7.0);
		EndDate = DateTime.Now;
		Search();
	}

	[RelayCommand]
	private void Export()
	{
		string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "电机电性能测试数据导出.csv");
		try
		{
			using StreamWriter streamWriter = new StreamWriter(text, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
			streamWriter.WriteLine("Barcode,TestTime,FinalResult,NoLoadCurrent(A),NoLoadSpeed(r/min),FwdNoise(dB),RevNoise(dB),LoadCurrent(A),LoadSpeed(r/min)");
			foreach (MotorTestRecordModel testResult in TestResults)
			{
				streamWriter.WriteLine(string.Join(",", Escape(testResult.Barcode), testResult.TestTime.ToString("yyyy-MM-dd HH:mm:ss"), testResult.FinalResult, testResult.NoLoadCurrentText, testResult.NoLoadSpeedText, testResult.FwdNoiseText, testResult.RevNoiseText, testResult.LoadCurrentText, testResult.LoadSpeedText));
			}
			MessageBox.Show($"成功导出 {TestResults.Count} 条记录至桌面:\n{text}", "数据导出成功", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
		catch (Exception ex)
		{
			MessageBox.Show("导出数据失败: " + ex.Message, "导出错误", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private static string Escape(object? value)
	{
		string text = value?.ToString() ?? string.Empty;
		return (text.Contains(',') || text.Contains('"') || text.Contains('\n')) ? ("\"" + text.Replace("\"", "\"\"") + "\"") : text;
	}
}
