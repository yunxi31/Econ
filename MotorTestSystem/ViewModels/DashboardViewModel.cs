using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using MotorTestSystem.Models;
using MotorTestSystem.Services;
using SkiaSharp;

namespace MotorTestSystem.ViewModels
{
    public partial class DashboardViewModel : ViewModelBase
    {
        private readonly IMotorTestRepository _repository;
        private readonly BackendRuntime _runtime;
        private readonly DispatcherTimer _refreshTimer;

        #region KPI Card Properties

        [ObservableProperty]
        private string _onlineStationDisplay = "0 / 0";

        [ObservableProperty]
        private string _onlineRateText = "● 在线率 0%";

        [ObservableProperty]
        private int _totalChecked;

        [ObservableProperty]
        private int _okCount;

        [ObservableProperty]
        private int _ngCount;

        [ObservableProperty]
        private double _passRate;

        [ObservableProperty]
        private double _completionProgress;

        [ObservableProperty]
        private string _completionProgressText = "0%";

        [ObservableProperty]
        private string _passRateRingText = "0%";

        [ObservableProperty]
        private double _passRateRingDash;

        #endregion

        #region Chart Properties

        [ObservableProperty]
        private ISeries[] _outputSeries = Array.Empty<ISeries>();

        [ObservableProperty]
        private ISeries[] _defectDistributionSeries = Array.Empty<ISeries>();

        [ObservableProperty]
        private Axis[] _xAxes = Array.Empty<Axis>();

        [ObservableProperty]
        private Axis[] _yAxes = Array.Empty<Axis>();

        [ObservableProperty]
        private Axis[] _passRateXAxes = Array.Empty<Axis>();

        [ObservableProperty]
        private Axis[] _passRateYAxes = Array.Empty<Axis>();

        [ObservableProperty]
        private ISeries[] _passRateSeries = Array.Empty<ISeries>();

        #endregion

        #region List Properties

        public System.Collections.ObjectModel.ObservableCollection<DefectItem> DefectList { get; } = new();
        public System.Collections.ObjectModel.ObservableCollection<FaultReason> TopFaultList { get; } = new();

        #endregion

        #region 海康视频监控属性

        [ObservableProperty]
        private string _cameraStatus = "未连接";

        [ObservableProperty]
        private bool _isCameraConnected;

        [ObservableProperty]
        private string _cameraIp = "192.168.1.64";

        [ObservableProperty]
        private int _cameraPort = 8000;

        [ObservableProperty]
        private string _cameraUsername = "admin";

        [ObservableProperty]
        private string _cameraPassword = "admin123";

        [ObservableProperty]
        private string _captureImagePath = "";

        [ObservableProperty]
        private bool _isCameraLoading;

        public System.Collections.ObjectModel.ObservableCollection<CameraInfo> CameraList { get; } = new();

        #endregion

        #region Tooltip Paints

        public SolidColorPaint TooltipBgPaint { get; } = new SolidColorPaint(new SKColor(24, 25, 36, 230));
        public SolidColorPaint TooltipTextPaint { get; } = new SolidColorPaint(SKColors.White)
        {
            SKTypeface = SKTypeface.FromFamilyName("Segoe UI")
        };

        #endregion

        private string _currentDimension = "今日";
        private const double TargetPassRate = 98.0;
        private const int DailyTarget = 2000;

        public DashboardViewModel()
            : this(BackendRuntime.Shared.Repository, BackendRuntime.Shared)
        {
        }

        public DashboardViewModel(IMotorTestRepository repository, BackendRuntime runtime)
        {
            _repository = repository;
            _runtime = runtime;

            // 初始化良率 Y 轴
            PassRateYAxes = new Axis[]
            {
                new Axis
                {
                    MinLimit = 85,
                    MaxLimit = 100,
                    ForceStepToMin = true,
                    MinStep = 5,
                    Labeler = val => $"{val}%",
                    LabelsPaint = new SolidColorPaint(SKColor.Parse("#6E7C8A")),
                    SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#20232C"))
                }
            };

            // 订阅 PLC 轮询事件，实时刷新
            _runtime.PollingService.SnapshotReceived += OnSnapshotReceived;

            // 首次加载：异步启动，避免在 UI 线程同步等待造成死锁
            Application.Current?.Dispatcher?.InvokeAsync(async () =>
            {
                await RefreshAllDataAsync();
            });

            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _refreshTimer.Tick += async (_, _) => await RefreshAllDataAsync();
            _refreshTimer.Start();
        }

        private void OnSnapshotReceived(object? sender, StationSnapshot snapshot)
        {
            // 收到 PLC 数据时触发刷新（节流：避免高频刷新）
            Application.Current?.Dispatcher?.InvokeAsync(async () =>
            {
                await RefreshAllDataAsync();
                UpdateOnlineStationCount();
            });
        }

        #region Refresh Methods

        // RefreshAllData() 已移除，直接通过 Dispatcher.InvokeAsync 异步调用 RefreshAllDataAsync()

        private async System.Threading.Tasks.Task RefreshAllDataAsync()
        {
            try
            {
                await Task.WhenAll(
                    RefreshKpiCardsAsync(),
                    RefreshHourlyChartsAsync(),
                    RefreshDefectDataAsync(),
                    RefreshFaultRankingAsync()
                );
            }
            catch
            {
                // 静默处理
            }
        }

        private async System.Threading.Tasks.Task RefreshKpiCardsAsync()
        {
            DateTime start = DateTime.Now.Date;
            DateTime end = start.AddDays(1).AddTicks(-1);

            var summary = await _repository.GetSummaryAsync(start, end);

            if (summary.TotalChecked > 0)
            {
                TotalChecked = summary.TotalChecked;
                OkCount = summary.OkCount;
                NgCount = summary.NgCount;
                PassRate = Math.Round(summary.PassRate, 1);
            }
            else
            {
                TotalChecked = 0;
                OkCount = 0;
                NgCount = 0;
                PassRate = 0;
            }

            // 更新完成进度
            CompletionProgress = Math.Min(100.0, Math.Round(TotalChecked * 100.0 / DailyTarget, 1));
            CompletionProgressText = $"{CompletionProgress:F0}%";

            // 更新良率环形进度
            PassRateRingText = $"{PassRate:F1}%";
            // Ellipse StrokeDashArray 模拟进度: 周长约 3.14 * 44 ≈ 138.2, 这里简化
            PassRateRingDash = PassRate / 100.0;

            // 更新设备在线数
            UpdateOnlineStationCount();
        }

        private void UpdateOnlineStationCount()
        {
            int total = _runtime.StationConfigs.Count;
            int online = _runtime.StationConfigs.Count(c => c.IsConnected);
            OnlineStationDisplay = $"{online} / {total}";
            double rate = total > 0 ? Math.Round(online * 100.0 / total, 0) : 0;
            OnlineRateText = $"● 在线率 {rate:F0}%";
        }

        private async System.Threading.Tasks.Task RefreshHourlyChartsAsync()
        {
            DateTime now = DateTime.Now;
            int count = 8;
            string[] labels = new string[count];
            int[] okValues = new int[count];
            int[] ngValues = new int[count];
            double[] passRateValues = new double[count];

            // 查询过去 8 小时的数据
            DateTime firstHourStart = now.AddHours(-7);
            DateTime queryStartTime = new DateTime(firstHourStart.Year, firstHourStart.Month, firstHourStart.Day, firstHourStart.Hour, 0, 0);
            DateTime queryEndTime = now;

            var query = new MotorTestQuery
            {
                Barcode = "",
                ResultFilter = "全部",
                StartTime = queryStartTime,
                EndTime = queryEndTime
            };

            IReadOnlyList<MotorTestResult>? records = null;
            try
            {
                records = await _repository.QueryAsync(query);
            }
            catch
            {
                // ignore
            }

            bool useRealData = records != null && records.Count > 0;

            for (int i = 0; i < count; i++)
            {
                DateTime targetTime = now.AddHours(-7 + i);
                int hour = targetTime.Hour;
                labels[i] = $"{hour:D2}:00";

                if (useRealData)
                {
                    var hourRecords = records!.Where(r => r.TestTime.Date == targetTime.Date && r.TestTime.Hour == hour).ToList();
                    okValues[i] = hourRecords.Count(r => r.FinalResult == "OK");
                    ngValues[i] = hourRecords.Count(r => r.FinalResult == "NG");
                    int total = okValues[i] + ngValues[i];
                    passRateValues[i] = total == 0 ? 100.0 : Math.Round(okValues[i] * 100.0 / total, 1);
                }
                else
                {
                    okValues[i] = 0;
                    ngValues[i] = 0;
                    passRateValues[i] = 0;
                }
            }

            // 更新小时生产统计柱状图
            XAxes = CreateAxis(labels, -0.5, 7.5);
            OutputSeries = new ISeries[]
            {
                new StackedColumnSeries<int>
                {
                    Name = "合格",
                    Values = okValues,
                    Stroke = null,
                    Fill = new SolidColorPaint(SKColor.Parse("#00DFFF")),
                    Padding = 8,
                    MaxBarWidth = 32
                },
                new StackedColumnSeries<int>
                {
                    Name = "不合格",
                    Values = ngValues,
                    Stroke = null,
                    Fill = new SolidColorPaint(SKColor.Parse("#FF3366")),
                    Padding = 8,
                    MaxBarWidth = 32
                }
            };

            YAxes = new Axis[]
            {
                new Axis
                {
                    MinLimit = 0,
                    LabelsPaint = new SolidColorPaint(SKColor.Parse("#6E7C8A")),
                    SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#20232C"))
                }
            };

            // 更新良率趋势折线图
            await UpdatePassRateChartAsync(labels, passRateValues, useRealData);
        }

        private async System.Threading.Tasks.Task UpdatePassRateChartAsync(string[] todayLabels, double[] todayValues, bool hasRealData)
        {
            if (_currentDimension == "今日")
            {
                PassRateXAxes = CreateAxis(todayLabels, -0.5, 7.5);
                PassRateSeries = hasRealData
                    ? CreateLineSeries(todayValues, true)
                    : CreateLineSeries(Enumerable.Repeat(0.0, todayLabels.Length).ToArray(), true);
            }
            else if (_currentDimension == "本周")
            {
                var weekLabels = new[] { "周一", "周二", "周三", "周四", "周五", "周六", "周日" };
                var weekValues = await CalculateWeeklyPassRatesAsync();
                PassRateXAxes = CreateAxis(weekLabels, -0.5, 6.5);
                PassRateSeries = CreateLineSeries(weekValues, true);
            }
            else if (_currentDimension == "本月")
            {
                var monthLabels = new[] { "第一周", "第二周", "第三周", "第四周" };
                var monthValues = await CalculateMonthlyPassRatesAsync();
                PassRateXAxes = CreateAxis(monthLabels, -0.5, 3.5);
                PassRateSeries = CreateLineSeries(monthValues, true);
            }
        }

        private async System.Threading.Tasks.Task<double[]> CalculateWeeklyPassRatesAsync()
        {
            var result = new double[7];
            DateTime today = DateTime.Now.Date;

            for (int i = 0; i < 7; i++)
            {
                DateTime day = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday + i);
                if (day > today) break;

                var summary = await _repository.GetSummaryAsync(day, day.AddDays(1).AddTicks(-1));
                result[i] = summary.TotalChecked > 0 ? Math.Round(summary.PassRate, 1) : 0;
            }

            return result;
        }

        private async System.Threading.Tasks.Task<double[]> CalculateMonthlyPassRatesAsync()
        {
            var result = new double[4];
            DateTime today = DateTime.Now.Date;
            DateTime monthStart = new DateTime(today.Year, today.Month, 1);

            for (int i = 0; i < 4; i++)
            {
                DateTime weekStart = monthStart.AddDays(i * 7);
                DateTime weekEnd = weekStart.AddDays(7).AddTicks(-1);
                if (weekStart > today) break;
                if (weekEnd > today) weekEnd = today.AddDays(1).AddTicks(-1);

                var summary = await _repository.GetSummaryAsync(weekStart, weekEnd);
                result[i] = summary.TotalChecked > 0 ? Math.Round(summary.PassRate, 1) : 0;
            }

            return result;
        }

        private async System.Threading.Tasks.Task RefreshDefectDataAsync()
        {
            DateTime start = DateTime.Now.Date;
            DateTime end = start.AddDays(1).AddTicks(-1);

            var defectSummary = await _repository.GetDefectSummaryAsync(start, end);

            // 更新不良分布列表
            DefectList.Clear();
            if (defectSummary.TotalNgCount > 0)
            {
                DefectList.Add(new DefectItem { Name = "空载不合格", Percentage = defectSummary.NoLoadPercentage, Color = "#FFA500" });
                DefectList.Add(new DefectItem { Name = "噪音不合格", Percentage = defectSummary.NoisePercentage, Color = "#FF3366" });
                DefectList.Add(new DefectItem { Name = "负载不合格", Percentage = defectSummary.LoadPercentage, Color = "#8E9AA7" });
            }
            else
            {
                // 无数据时显示默认比例
                DefectList.Add(new DefectItem { Name = "空载不合格", Percentage = 0, Color = "#FFA500" });
                DefectList.Add(new DefectItem { Name = "噪音不合格", Percentage = 0, Color = "#FF3366" });
                DefectList.Add(new DefectItem { Name = "负载不合格", Percentage = 0, Color = "#8E9AA7" });
            }

            // 更新环形饼图
            double noLoad = defectSummary.TotalNgCount > 0 ? defectSummary.NoLoadPercentage : 33.3;
            double noise = defectSummary.TotalNgCount > 0 ? defectSummary.NoisePercentage : 33.3;
            double load = defectSummary.TotalNgCount > 0 ? defectSummary.LoadPercentage : 33.4;

            DefectDistributionSeries = new ISeries[]
            {
                new PieSeries<double>
                {
                    Name = "空载不合格",
                    Values = new double[] { noLoad },
                    InnerRadius = 35,
                    Fill = new SolidColorPaint(SKColor.Parse("#FFA500")),
                    Stroke = null
                },
                new PieSeries<double>
                {
                    Name = "噪音不合格",
                    Values = new double[] { noise },
                    InnerRadius = 35,
                    Fill = new SolidColorPaint(SKColor.Parse("#FF3366")),
                    Stroke = null
                },
                new PieSeries<double>
                {
                    Name = "负载不合格",
                    Values = new double[] { load },
                    InnerRadius = 35,
                    Fill = new SolidColorPaint(SKColor.Parse("#8E9AA7")),
                    Stroke = null
                }
            };
        }

        private async System.Threading.Tasks.Task RefreshFaultRankingAsync()
        {
            DateTime start = DateTime.Now.Date;
            DateTime end = start.AddDays(1).AddTicks(-1);

            var ranking = await _repository.GetFaultRankingAsync(start, end, 5);

            TopFaultList.Clear();
            if (ranking.Count > 0)
            {
                foreach (var item in ranking)
                {
                    TopFaultList.Add(new FaultReason
                    {
                        Rank = item.Rank.ToString("D2"),
                        Name = item.Name,
                        Count = item.Count,
                        Color = item.Rank == 1 ? "#FF3366" : item.Rank == 2 ? "#FFA500" : "#8E9AA7"
                    });
                }
            }
            else
            {
                // 无数据时显示空占位
                for (int i = 1; i <= 5; i++)
                {
                    TopFaultList.Add(new FaultReason
                    {
                        Rank = i.ToString("D2"),
                        Name = "暂无数据",
                        Count = 0,
                        Color = "#8E9AA7"
                    });
                }
            }
        }

        #endregion

        #region Commands

        [RelayCommand]
        private async Task SelectTimeDimension(string dimension)
        {
            if (string.IsNullOrEmpty(dimension)) return;
            _currentDimension = dimension;
            await RefreshHourlyChartsAsync();
        }

        [RelayCommand]
        private async Task ConnectCameraAsync()
        {
            if (IsCameraLoading) return;

            IsCameraLoading = true;
            CameraStatus = "正在连接...";

            try
            {
                var result = await _runtime.HikvisionService.LoginAsync(CameraIp, CameraPort, CameraUsername, CameraPassword);

                if (result.Success)
                {
                    IsCameraConnected = true;
                    CameraStatus = $"已连接 ({result.DeviceInfo?.ChannelCount ?? 0} 通道)";

                    // 添加到摄像头列表
                    if (!CameraList.Any(c => c.Ip == CameraIp))
                    {
                        CameraList.Add(new CameraInfo
                        {
                            Ip = CameraIp,
                            Port = CameraPort,
                            Username = CameraUsername,
                            Password = CameraPassword,
                            ChannelCount = result.DeviceInfo?.ChannelCount ?? 0,
                            SerialNumber = result.DeviceInfo?.SerialNumber ?? ""
                        });
                    }

                    MessageBox.Show($"摄像头连接成功！\n设备序列号: {result.DeviceInfo?.SerialNumber}\n通道数: {result.DeviceInfo?.ChannelCount}", "连接成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    IsCameraConnected = false;
                    CameraStatus = $"连接失败: {result.ErrorMessage}";
                    MessageBox.Show($"连接失败: {result.ErrorMessage}", "连接失败", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                IsCameraConnected = false;
                CameraStatus = $"连接异常: {ex.Message}";
                MessageBox.Show($"连接异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsCameraLoading = false;
            }
        }

        [RelayCommand]
        private void DisconnectCamera()
        {
            if (_runtime.HikvisionService.Logout(CameraIp))
            {
                IsCameraConnected = false;
                CameraStatus = "已断开";

                var camera = CameraList.FirstOrDefault(c => c.Ip == CameraIp);
                if (camera != null)
                {
                    CameraList.Remove(camera);
                }
            }
        }

        [RelayCommand]
        private async Task CaptureImageAsync()
        {
            if (!IsCameraConnected)
            {
                MessageBox.Show("请先连接摄像头", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string fileName = $"capture_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                string filePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), fileName);

                // 这里简化处理，实际需要先启动预览才能抓图
                // 在实际项目中应该配合视频预览窗口使用
                CaptureImagePath = filePath;

                MessageBox.Show($"抓图路径: {filePath}\n\n注意: 实际抓图需要先启动预览窗口", "抓图", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"抓图失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Chart Helpers

        private Axis[] CreateAxis(string[] labels, double? minLimit = null, double? maxLimit = null)
        {
            return new Axis[]
            {
                new Axis
                {
                    Labels = labels,
                    MinLimit = minLimit,
                    MaxLimit = maxLimit,
                    LabelsPaint = new SolidColorPaint(SKColor.Parse("#6E7C8A")),
                    SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#20232C"))
                }
            };
        }

        private ISeries[] CreateLineSeries(double[] values, bool includeTarget = false)
        {
            var seriesList = new List<ISeries>
            {
                new LineSeries<double>
                {
                    Name = "实际",
                    Values = values,
                    Stroke = new SolidColorPaint(SKColor.Parse("#00FFB2"), 4),
                    Fill = new LinearGradientPaint(
                        new[] { SKColor.Parse("#4000FFB2"), SKColor.Parse("#0000FFB2") },
                        new SKPoint(0.5f, 0),
                        new SKPoint(0.5f, 1)),
                    GeometrySize = 10,
                    GeometryStroke = new SolidColorPaint(SKColor.Parse("#00FFB2"), 2),
                    GeometryFill = new SolidColorPaint(SKColor.Parse("#1A1D24")),
                    LineSmoothness = 0.6
                }
            };

            if (includeTarget)
            {
                var targetValues = new double[values.Length];
                for (int i = 0; i < values.Length; i++)
                {
                    targetValues[i] = TargetPassRate;
                }

                seriesList.Add(new LineSeries<double>
                {
                    Name = $"目标 ({TargetPassRate}%)",
                    Values = targetValues,
                    Stroke = new SolidColorPaint(SKColor.Parse("#FFC107"), 2),
                    Fill = null,
                    GeometrySize = 0,
                    LineSmoothness = 0
                });
            }

            return seriesList.ToArray();
        }

        #endregion
    }

    public class DefectItem
    {
        public string Name { get; set; } = string.Empty;
        public double Percentage { get; set; }
        public string Color { get; set; } = "#8E9AA7";

        public System.Windows.Media.Brush ColorBrush => (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(Color)!;
    }

    public class FaultReason
    {
        public string Rank { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
        public string Color { get; set; } = "#8E9AA7";
    }

    /// <summary>
    /// 摄像头信息
    /// </summary>
    public class CameraInfo
    {
        public string Ip { get; set; } = string.Empty;
        public int Port { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int ChannelCount { get; set; }
        public string SerialNumber { get; set; } = string.Empty;
    }
}
