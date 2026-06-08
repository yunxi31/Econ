using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace MotorTestSystem.ViewModels
{
    public partial class DashboardViewModel : ViewModelBase
    {
        [ObservableProperty]
        private int _totalChecked = 1256;

        [ObservableProperty]
        private int _okCount = 1242;

        [ObservableProperty]
        private int _ngCount = 14;

        [ObservableProperty]
        private double _passRate = 98.88;

        [ObservableProperty]
        private bool _isCameraPlaying = true;

        [ObservableProperty]
        private string _cameraStatus = "连接正常 (192.168.1.200)";

        public ISeries[] OutputSeries { get; set; }
        public ISeries[] PassRateSeries { get; set; }
        public Axis[] XAxes { get; set; }
        public Axis[] YAxes { get; set; }

        public DashboardViewModel()
        {
            // Initialize Production Statistics Charts
            OutputSeries = new ISeries[]
            {
                new ColumnSeries<int>
                {
                    Name = "日产量",
                    Values = new int[] { 180, 210, 230, 195, 240, 280, 256 },
                    Stroke = null,
                    Fill = new SolidColorPaint(SKColor.Parse("#00FFB2")), // Premium Mint Green
                    Padding = 4
                }
            };

            PassRateSeries = new ISeries[]
            {
                new LineSeries<double>
                {
                    Name = "良品率 (%)",
                    Values = new double[] { 98.2, 98.5, 97.8, 98.9, 99.1, 98.7, 98.88 },
                    Stroke = new SolidColorPaint(SKColor.Parse("#00DFFF"), 3), // Cyan line
                    Fill = new SolidColorPaint(SKColor.Parse("#1A00DFFF")), // Transparent Cyan fill
                    GeometrySize = 8,
                    GeometryStroke = new SolidColorPaint(SKColor.Parse("#00DFFF"), 2)
                }
            };

            XAxes = new Axis[]
            {
                new Axis
                {
                    Labels = new string[] { "周一", "周二", "周三", "周四", "周五", "周六", "今天" },
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
        }

        [RelayCommand]
        private void ToggleCamera()
        {
            IsCameraPlaying = !IsCameraPlaying;
            CameraStatus = IsCameraPlaying ? "连接正常 (192.168.1.200)" : "监控已暂停";
        }
    }
}
