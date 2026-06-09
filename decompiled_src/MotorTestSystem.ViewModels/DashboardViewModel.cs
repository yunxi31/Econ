using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using LiveChartsCore;
using LiveChartsCore.Painting;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Drawing.Geometries;
using LiveChartsCore.SkiaSharpView.Painting;
using MotorTestSystem.Models;
using MotorTestSystem.Services;
using SkiaSharp;

namespace MotorTestSystem.ViewModels;

public class DashboardViewModel : ViewModelBase
{
	private readonly IMotorTestRepository _repository;

	private readonly DispatcherTimer _refreshTimer;

	[ObservableProperty]
	private int _totalChecked;

	[ObservableProperty]
	private int _okCount;

	[ObservableProperty]
	private int _ngCount;

	[ObservableProperty]
	private double _passRate;

	public ISeries[] OutputSeries { get; set; }

	public ISeries[] PassRateSeries { get; set; }

	public ISeries[] DefectDistributionSeries { get; set; }

	public Axis[] XAxes { get; set; }

	public Axis[] YAxes { get; set; }

	public Axis[] PassRateYAxes { get; set; }

	public ObservableCollection<DefectItem> DefectList { get; } = new ObservableCollection<DefectItem>
	{
		new DefectItem
		{
			Name = "空载不合格",
			Percentage = 45.0,
			Color = "#FFA500"
		},
		new DefectItem
		{
			Name = "噪音不合格",
			Percentage = 35.0,
			Color = "#FF3366"
		},
		new DefectItem
		{
			Name = "负载不合格",
			Percentage = 20.0,
			Color = "#8E9AA7"
		}
	};

	public ObservableCollection<FaultReason> TopFaultList { get; } = new ObservableCollection<FaultReason>
	{
		new FaultReason
		{
			Rank = "01",
			Name = "电机起动电流超限",
			Count = 186,
			Color = "#FF3366"
		},
		new FaultReason
		{
			Rank = "02",
			Name = "空载噪声过大",
			Count = 142,
			Color = "#FFA500"
		},
		new FaultReason
		{
			Rank = "03",
			Name = "反电动势异常",
			Count = 95,
			Color = "#8E9AA7"
		},
		new FaultReason
		{
			Rank = "04",
			Name = "温升过高",
			Count = 82,
			Color = "#8E9AA7"
		},
		new FaultReason
		{
			Rank = "05",
			Name = "转子动平衡超差",
			Count = 58,
			Color = "#8E9AA7"
		}
	};

	public SolidColorPaint TooltipBgPaint { get; set; } = new SolidColorPaint(new SKColor((byte)24, (byte)25, (byte)36, (byte)230));

	public SolidColorPaint TooltipTextPaint { get; set; } = new SolidColorPaint(SKColors.White)
	{
		SKTypeface = SKTypeface.FromFamilyName("Segoe UI")
	};

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int TotalChecked
	{
		get
		{
			return _totalChecked;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(_totalChecked, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.TotalChecked);
				_totalChecked = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.TotalChecked);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int OkCount
	{
		get
		{
			return _okCount;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(_okCount, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.OkCount);
				_okCount = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.OkCount);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int NgCount
	{
		get
		{
			return _ngCount;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(_ngCount, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.NgCount);
				_ngCount = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.NgCount);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public double PassRate
	{
		get
		{
			return _passRate;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_passRate, value))
			{
				((ObservableObject)this).OnPropertyChanging(__KnownINotifyPropertyChangingArgs.PassRate);
				_passRate = value;
				((ObservableObject)this).OnPropertyChanged(__KnownINotifyPropertyChangedArgs.PassRate);
			}
		}
	}

	public DashboardViewModel()
		: this(BackendRuntime.Shared.Repository)
	{
	}

	public DashboardViewModel(IMotorTestRepository repository)
	{
		//IL_01e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f0: Expected O, but got Unknown
		//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0211: Expected O, but got Unknown
		//IL_0261: Unknown result type (might be due to invalid IL or missing references)
		//IL_0266: Unknown result type (might be due to invalid IL or missing references)
		//IL_0270: Expected O, but got Unknown
		//IL_02cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02dc: Expected O, but got Unknown
		//IL_033e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0348: Unknown result type (might be due to invalid IL or missing references)
		//IL_0352: Expected O, but got Unknown
		//IL_0361: Unknown result type (might be due to invalid IL or missing references)
		//IL_0366: Unknown result type (might be due to invalid IL or missing references)
		//IL_0372: Unknown result type (might be due to invalid IL or missing references)
		//IL_0377: Unknown result type (might be due to invalid IL or missing references)
		//IL_0386: Unknown result type (might be due to invalid IL or missing references)
		//IL_0395: Unknown result type (might be due to invalid IL or missing references)
		//IL_039c: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a6: Expected O, but got Unknown
		//IL_03bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d1: Expected O, but got Unknown
		//IL_03d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_03dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e7: Expected O, but got Unknown
		//IL_044a: Unknown result type (might be due to invalid IL or missing references)
		//IL_044f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0459: Expected O, but got Unknown
		//IL_04a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_04b6: Expected O, but got Unknown
		//IL_0504: Unknown result type (might be due to invalid IL or missing references)
		//IL_0509: Unknown result type (might be due to invalid IL or missing references)
		//IL_0513: Expected O, but got Unknown
		//IL_052d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0533: Expected O, but got Unknown
		//IL_057e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0583: Unknown result type (might be due to invalid IL or missing references)
		//IL_058d: Expected O, but got Unknown
		//IL_0594: Unknown result type (might be due to invalid IL or missing references)
		//IL_0599: Unknown result type (might be due to invalid IL or missing references)
		//IL_05a3: Expected O, but got Unknown
		//IL_05b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_05ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_05ce: Expected O, but got Unknown
		//IL_05cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_05e0: Expected O, but got Unknown
		//IL_05e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_05e8: Expected O, but got Unknown
		//IL_05e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_05f8: Expected O, but got Unknown
		//IL_05f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_05ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0604: Unknown result type (might be due to invalid IL or missing references)
		//IL_060e: Expected O, but got Unknown
		//IL_060e: Expected O, but got Unknown
		//IL_060f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0615: Unknown result type (might be due to invalid IL or missing references)
		//IL_061a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0624: Expected O, but got Unknown
		//IL_0624: Expected O, but got Unknown
		//IL_0626: Expected O, but got Unknown
		//IL_0635: Unknown result type (might be due to invalid IL or missing references)
		//IL_063a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0648: Expected O, but got Unknown
		//IL_0649: Unknown result type (might be due to invalid IL or missing references)
		//IL_0657: Expected O, but got Unknown
		//IL_0658: Unknown result type (might be due to invalid IL or missing references)
		//IL_065f: Expected O, but got Unknown
		//IL_0660: Unknown result type (might be due to invalid IL or missing references)
		//IL_066f: Expected O, but got Unknown
		//IL_0670: Unknown result type (might be due to invalid IL or missing references)
		//IL_0695: Expected O, but got Unknown
		//IL_0696: Unknown result type (might be due to invalid IL or missing references)
		//IL_069c: Unknown result type (might be due to invalid IL or missing references)
		//IL_06a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_06ab: Expected O, but got Unknown
		//IL_06ab: Expected O, but got Unknown
		//IL_06ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_06b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_06b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_06c1: Expected O, but got Unknown
		//IL_06c1: Expected O, but got Unknown
		//IL_06c3: Expected O, but got Unknown
		//IL_06d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_06d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_06f0: Expected O, but got Unknown
		_repository = repository;
		OutputSeries = (ISeries[])(object)new ISeries[2]
		{
			(ISeries)new StackedColumnSeries<int>
			{
				Name = "合格",
				Values = (IReadOnlyCollection<int>)(object)new int[7] { 1450, 1600, 1600, 1400, 1680, 880, 0 },
				Stroke = null,
				Fill = (Paint)new SolidColorPaint(SKColor.Parse("#00DFFF")),
				Padding = 8.0,
				MaxBarWidth = 32.0
			},
			(ISeries)new StackedColumnSeries<int>
			{
				Name = "不合格",
				Values = (IReadOnlyCollection<int>)(object)new int[7] { 150, 100, 250, 100, 50, 50, 0 },
				Stroke = null,
				Fill = (Paint)new SolidColorPaint(SKColor.Parse("#FF3366")),
				Padding = 8.0,
				MaxBarWidth = 32.0
			}
		};
		PassRateSeries = (ISeries[])(object)new ISeries[1] { (ISeries)new LineSeries<double>
		{
			Name = "实际",
			Values = (IReadOnlyCollection<double>)(object)new double[7] { 93.5, 94.8, 92.5, 96.2, 95.5, 97.2, 92.0 },
			Stroke = (Paint)new SolidColorPaint(SKColor.Parse("#00FFB2"), 4f),
			Fill = (Paint)new LinearGradientPaint((SKColor[])(object)new SKColor[2]
			{
				SKColor.Parse("#4000FFB2"),
				SKColor.Parse("#0000FFB2")
			}, new SKPoint(0.5f, 0f), new SKPoint(0.5f, 1f), (float[])null, (SKShaderTileMode)0),
			GeometrySize = 10.0,
			GeometryStroke = (Paint)new SolidColorPaint(SKColor.Parse("#00FFB2"), 2f),
			GeometryFill = (Paint)new SolidColorPaint(SKColor.Parse("#1A1D24")),
			LineSmoothness = 0.6
		} };
		DefectDistributionSeries = (ISeries[])(object)new ISeries[3]
		{
			(ISeries)new PieSeries<double>
			{
				Name = "空载不合格",
				Values = (IReadOnlyCollection<double>)(object)new double[1] { 45.0 },
				InnerRadius = 35.0,
				Fill = (Paint)new SolidColorPaint(SKColor.Parse("#FFA500")),
				Stroke = null
			},
			(ISeries)new PieSeries<double>
			{
				Name = "噪音不合格",
				Values = (IReadOnlyCollection<double>)(object)new double[1] { 35.0 },
				InnerRadius = 35.0,
				Fill = (Paint)new SolidColorPaint(SKColor.Parse("#FF3366")),
				Stroke = null
			},
			(ISeries)new PieSeries<double>
			{
				Name = "负载不合格",
				Values = (IReadOnlyCollection<double>)(object)new double[1] { 20.0 },
				InnerRadius = 35.0,
				Fill = (Paint)new SolidColorPaint(SKColor.Parse("#8E9AA7")),
				Stroke = null
			}
		};
		Axis[] array = new Axis[1];
		Axis val2 = new Axis();
		((CoreAxis<LabelGeometry, LineGeometry>)(object)val2).Labels = new string[7] { "08:00", "09:00", "10:00", "11:00", "12:00", "13:00", "14:00" };
		((CoreAxis<LabelGeometry, LineGeometry>)(object)val2).LabelsPaint = (Paint)new SolidColorPaint(SKColor.Parse("#6E7C8A"));
		((CoreAxis<LabelGeometry, LineGeometry>)(object)val2).SeparatorsPaint = (Paint)new SolidColorPaint(SKColor.Parse("#20232C"));
		array[0] = val2;
		XAxes = (Axis[])(object)array;
		Axis[] array2 = new Axis[1];
		Axis val3 = new Axis();
		((CoreAxis<LabelGeometry, LineGeometry>)val3).MinLimit = 0.0;
		((CoreAxis<LabelGeometry, LineGeometry>)val3).MaxLimit = 2000.0;
		((CoreAxis<LabelGeometry, LineGeometry>)val3).ForceStepToMin = true;
		((CoreAxis<LabelGeometry, LineGeometry>)val3).MinStep = 500.0;
		((CoreAxis<LabelGeometry, LineGeometry>)val3).LabelsPaint = (Paint)new SolidColorPaint(SKColor.Parse("#6E7C8A"));
		((CoreAxis<LabelGeometry, LineGeometry>)val3).SeparatorsPaint = (Paint)new SolidColorPaint(SKColor.Parse("#20232C"));
		array2[0] = val3;
		YAxes = (Axis[])(object)array2;
		Axis[] array3 = new Axis[1];
		Axis val4 = new Axis();
		((CoreAxis<LabelGeometry, LineGeometry>)val4).MinLimit = 85.0;
		((CoreAxis<LabelGeometry, LineGeometry>)val4).MaxLimit = 100.0;
		((CoreAxis<LabelGeometry, LineGeometry>)val4).ForceStepToMin = true;
		((CoreAxis<LabelGeometry, LineGeometry>)val4).MinStep = 5.0;
		((CoreAxis<LabelGeometry, LineGeometry>)val4).Labeler = (double val) => $"{val}%";
		((CoreAxis<LabelGeometry, LineGeometry>)val4).LabelsPaint = (Paint)new SolidColorPaint(SKColor.Parse("#6E7C8A"));
		((CoreAxis<LabelGeometry, LineGeometry>)val4).SeparatorsPaint = (Paint)new SolidColorPaint(SKColor.Parse("#20232C"));
		array3[0] = val4;
		PassRateYAxes = (Axis[])(object)array3;
		RefreshSummary();
		_refreshTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(5.0)
		};
		_refreshTimer.Tick += delegate
		{
			RefreshSummary();
		};
		_refreshTimer.Start();
	}

	private void RefreshSummary()
	{
		DateTime date = DateTime.Now.Date;
		DateTime endTime = date.AddDays(1.0).AddTicks(-1L);
		try
		{
			ProductionSummary result = _repository.GetSummaryAsync(date, endTime).GetAwaiter().GetResult();
			if (result.TotalChecked > 0)
			{
				TotalChecked = result.TotalChecked;
				OkCount = result.OkCount;
				NgCount = result.NgCount;
				PassRate = result.PassRate;
			}
			else
			{
				TotalChecked = 12458;
				OkCount = 11895;
				NgCount = 563;
				PassRate = 95.5;
			}
		}
		catch
		{
			TotalChecked = 12458;
			OkCount = 11895;
			NgCount = 563;
			PassRate = 95.5;
		}
	}
}
