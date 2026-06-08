using System;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using MotorTestSystem.Services;
using SkiaSharp;

namespace MotorTestSystem.ViewModels
{
    public partial class DashboardViewModel : ViewModelBase
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

        [ObservableProperty]
        private bool _isCameraPlaying = true;

        [ObservableProperty]
        private string _cameraStatus = "连接正常 (192.168.1.200)";

        public ISeries[] OutputSeries { get; set; }
        public ISeries[] PassRateSeries { get; set; }
        public Axis[] XAxes { get; set; }
        public Axis[] YAxes { get; set; }

        public DashboardViewModel()
            : this(BackendRuntime.Shared.Repository)
        {
        }

        public DashboardViewModel(IMotorTestRepository repository)
        {
            _repository = repository;

            OutputSeries = new ISeries[]
            {
                new ColumnSeries<int>
                {
                    Name = "日产量",
                    Values = new[] { 180, 210, 230, 195, 240, 280, 256 },
                    Stroke = null,
                    Fill = new SolidColorPaint(SKColor.Parse("#00FFB2")),
                    Padding = 4
                }
            };

            PassRateSeries = new ISeries[]
            {
                new LineSeries<double>
                {
                    Name = "良品率 (%)",
                    Values = new[] { 98.2, 98.5, 97.8, 98.9, 99.1, 98.7, 98.88 },
                    Stroke = new SolidColorPaint(SKColor.Parse("#00DFFF"), 3),
                    Fill = new SolidColorPaint(SKColor.Parse("#1A00DFFF")),
                    GeometrySize = 8,
                    GeometryStroke = new SolidColorPaint(SKColor.Parse("#00DFFF"), 2)
                }
            };

            XAxes = new Axis[]
            {
                new Axis
                {
                    Labels = new[] { "周一", "周二", "周三", "周四", "周五", "周六", "今天" },
                    LabelsPaint = new SolidColorPaint(SKColor.Parse("#A0AAB2")),
                    SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#252830"))
                }
            };

            YAxes = new Axis[]
            {
                new Axis
                {
                    LabelsPaint = new SolidColorPaint(SKColor.Parse("#A0AAB2")),
                    SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#252830"))
                }
            };

            RefreshSummary();
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _refreshTimer.Tick += (_, _) => RefreshSummary();
            _refreshTimer.Start();
        }

        [RelayCommand]
        private void ToggleCamera()
        {
            IsCameraPlaying = !IsCameraPlaying;
            CameraStatus = IsCameraPlaying ? "连接正常 (192.168.1.200)" : "监控已暂停";
        }

        private void RefreshSummary()
        {
            DateTime start = DateTime.Now.Date;
            DateTime end = start.AddDays(1).AddTicks(-1);
            var summary = _repository.GetSummaryAsync(start, end).GetAwaiter().GetResult();

            TotalChecked = summary.TotalChecked;
            OkCount = summary.OkCount;
            NgCount = summary.NgCount;
            PassRate = summary.PassRate;
        }
    }
}
