using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Xps;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using MotorTestSystem.Models;
using MotorTestSystem.Services;
using MotorTestSystem.Views;
using SkiaSharp;

// 快捷时间过滤枚举
public enum QuickTimeFilter { LastHour, CurrentShift, Today }

namespace MotorTestSystem.ViewModels
{
    public class MotorTestRecordModel
    {
        public string Barcode { get; set; } = string.Empty;
        public DateTime TestTime { get; set; }
        public string FinalResult { get; set; } = "OK"; // "OK" or "NG"
        
        public double? NoLoadCurrent { get; set; }
        public double? NoLoadSpeed { get; set; }
        public double? ShaftLength { get; set; }
        public double? KnurlDiameter { get; set; }
        public string? NoLoadResult { get; set; }
        public double? FwdNoise { get; set; }
        public double? RevNoise { get; set; }
        public double? NoiseDiff { get; set; }
        public string? NoiseResult { get; set; }
        public double? LoadCurrent { get; set; }
        public double? LoadSpeed { get; set; }
        public string? LoadResult { get; set; }

        // Helper properties for XAML Binding:
        public string NoLoadCurrentText => NoLoadCurrent?.ToString("F2") ?? "NULL";
        public string NoLoadSpeedText => NoLoadSpeed?.ToString("F0") ?? "NULL";
        public string FwdNoiseText => FwdNoise?.ToString("F1") ?? "NULL";
        public string RevNoiseText => RevNoise?.ToString("F1") ?? "NULL";
        public string LoadCurrentText => LoadCurrent?.ToString("F2") ?? "NULL";
        public string LoadSpeedText => LoadSpeed?.ToString("F0") ?? "NULL";

        // Highlighting/Color properties:
        public bool IsNoLoadCurrentAbnormal => NoLoadCurrent > 2.5;
        public bool IsNoLoadSpeedAbnormal => NoLoadSpeed < 1800 || NoLoadSpeed > 2200;
        public bool IsFwdNoiseAbnormal => FwdNoise > 70.0;
        public bool IsRevNoiseAbnormal => RevNoise > 70.0;
        public bool IsLoadCurrentAbnormal => LoadCurrent > 3.0;
        public bool IsLoadSpeedAbnormal => LoadSpeed < 1000;
    }

    public partial class HistoryViewModel : ViewModelBase
    {
        private readonly IMotorTestRepository _repository;

        [ObservableProperty]
        private string _searchBarcode = string.Empty;

        [ObservableProperty]
        private string _selectedResultFilter = "全部";

        public List<string> ResultFilters { get; } = new() { "全部", "OK", "NG" };

        private bool _isApplyingQuickFilter;
        private bool _isResetting;

        private DateTime _startDate = DateTime.Today.AddDays(-7);
        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                // 如果是同一天，且新值的时间部分为 00:00:00，而旧值的时间部分不为 00:00:00，则忽略（防止 DatePicker 失去焦点时清除精确时分秒）
                if (!_isResetting && !_isApplyingQuickFilter && value.Date == _startDate.Date && value.TimeOfDay == TimeSpan.Zero && _startDate.TimeOfDay != TimeSpan.Zero)
                {
                    return;
                }
                if (SetProperty(ref _startDate, value))
                {
                    if (!_isApplyingQuickFilter)
                    {
                        SelectedQuickFilter = null;
                    }
                }
            }
        }

        private DateTime _endDate = DateTime.Today;
        public DateTime EndDate
        {
            get => _endDate;
            set
            {
                // 如果是同一天，且新值的时间部分为 00:00:00，而旧值的时间部分不为 00:00:00，则忽略（防止 DatePicker 失去焦点时清除精确时分秒）
                if (!_isResetting && !_isApplyingQuickFilter && value.Date == _endDate.Date && value.TimeOfDay == TimeSpan.Zero && _endDate.TimeOfDay != TimeSpan.Zero)
                {
                    return;
                }
                if (SetProperty(ref _endDate, value))
                {
                    if (!_isApplyingQuickFilter)
                    {
                        SelectedQuickFilter = null;
                    }
                }
            }
        }

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

        // ---- 任务二：选中行联动 ----
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSelectedMotor))]
        [NotifyPropertyChangedFor(nameof(SelectedMotorResult))]
        private MotorTestRecordModel? _selectedMotor;

        public bool HasSelectedMotor => SelectedMotor != null;

        /// <summary>返回选中电机的 FinalResult，用于右侧徽章联动</summary>
        public string SelectedMotorResult => SelectedMotor?.FinalResult ?? string.Empty;

        // ---- 打印状态 ----
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotPrinting))]
        private bool _isPrinting;

        [ObservableProperty]
        private string _printStatus = string.Empty;

        public bool IsNotPrinting => !IsPrinting;

        #region 波形图属性

        [ObservableProperty]
        private ISeries[] _noLoadWaveformSeries = Array.Empty<ISeries>();

        [ObservableProperty]
        private ISeries[] _noiseWaveformSeries = Array.Empty<ISeries>();

        [ObservableProperty]
        private Axis[] _waveformXAxes = Array.Empty<Axis>();

        [ObservableProperty]
        private Axis[] _noLoadWaveformYAxes = Array.Empty<Axis>();

        [ObservableProperty]
        private Axis[] _noiseWaveformYAxes = Array.Empty<Axis>();

        public SolidColorPaint WaveformTooltipBgPaint { get; } = new SolidColorPaint(new SKColor(24, 25, 36, 230));
        public SolidColorPaint WaveformTooltipTextPaint { get; } = new SolidColorPaint(SKColors.White)
        {
            SKTypeface = SKTypeface.FromFamilyName("Segoe UI")
        };

        #endregion

        partial void OnSelectedMotorChanged(MotorTestRecordModel? value)
        {
            if (value != null)
            {
                GenerateWaveformData(value);
            }
            else
            {
                NoLoadWaveformSeries = Array.Empty<ISeries>();
                NoiseWaveformSeries = Array.Empty<ISeries>();
            }
        }

        /// <summary>
        /// 根据选中电机的测试数据，生成模拟波形数据
        /// </summary>
        private void GenerateWaveformData(MotorTestRecordModel motor)
        {
            var rng = new Random(motor.Barcode.GetHashCode());

            // ---- 空载电流波形 ----
            // 模拟一个更真实的启动冲击 → 稳态波动的电流曲线（采样500点）
            double baseCurrent = motor.NoLoadCurrent ?? 1.2;
            double peakCurrent = baseCurrent * 2.8; // 启动冲击峰值
            int sampleCount = 500;
            var currentData = new double[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                double t = i / (double)sampleCount;
                
                // 1. 启动冲击与衰减复合波形（从0起动，快速上升至peak，后指数衰减）
                double startupShock = 0;
                if (t < 0.15)
                {
                    double rise = 1.0 - Math.Exp(-t / 0.002);
                    double decay = Math.Exp(-t / 0.015);
                    startupShock = (peakCurrent - baseCurrent) * rise * decay;
                }

                // 2. 稳态高频与低频正弦微幅波动
                double ripple = Math.Sin(i * 0.25) * 0.02 * baseCurrent + Math.Sin(i * 0.8) * 0.005 * baseCurrent;
                
                // 3. 随机毛刺噪声
                double noise = (rng.NextDouble() - 0.5) * 0.03 * baseCurrent;

                if (t < 0.001)
                {
                    currentData[i] = 0;
                }
                else
                {
                    double initialFilter = 1.0 - Math.Exp(-t / 0.001);
                    currentData[i] = (baseCurrent + startupShock + ripple + noise) * initialFilter;
                }
            }

            // 阈值线
            double currentLimit = 2.5; // 空载电流上限一致

            NoLoadWaveformSeries = new ISeries[]
            {
                new LineSeries<double>
                {
                    Name = "空载电流",
                    Values = currentData,
                    Stroke = new SolidColorPaint(SKColor.Parse("#00DFFF"), 2),
                    Fill = new SolidColorPaint(SKColor.Parse("#1400DFFF")), // 填充半透明区域
                    GeometrySize = 0,
                    LineSmoothness = 0.1, // 减小平滑度，避免尖峰失真
                    IsVisibleAtLegend = true
                },
                new LineSeries<double>
                {
                    Name = $"上限 ({currentLimit} A)",
                    Values = Enumerable.Repeat(currentLimit, sampleCount).ToArray(),
                    Stroke = new SolidColorPaint(SKColor.Parse("#F74C5B"), 1.5f)
                    {
                        PathEffect = new DashEffect(new float[] { 6, 4 }, 0)
                    },
                    Fill = null,
                    GeometrySize = 0,
                    LineSmoothness = 0
                }
            };

            NoLoadWaveformYAxes = new Axis[]
            {
                new Axis
                {
                    LabelsPaint = new SolidColorPaint(SKColor.Parse("#6E7C8A")),
                    SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#20232C")),
                    MinLimit = 0,
                    MaxLimit = Math.Max(peakCurrent + 0.5, currentLimit + 1),
                    Labeler = val => $"{val:F1}"
                }
            };

            // ---- 噪音波形 ----
            // 模拟高频采样的正转/反转噪音频谱时间序列
            double fwdBase = motor.FwdNoise ?? 55.0;
            double revBase = motor.RevNoise ?? 55.0;
            var fwdNoiseData = new double[sampleCount];
            var revNoiseData = new double[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                // 正转噪音：高频颤动 + 随机声学噪声
                double fwdRipple = Math.Sin(i * 0.6) * 0.6 + Math.Sin(i * 1.4) * 0.3;
                double fwdNoise = (rng.NextDouble() - 0.5) * 4.0;
                fwdNoiseData[i] = fwdBase + fwdRipple + fwdNoise;

                // 反转噪音：高频颤动 + 随机声学噪声
                double revRipple = Math.Sin(i * 0.7 + 1.5) * 0.5 + Math.Sin(i * 1.5) * 0.2;
                double revNoise = (rng.NextDouble() - 0.5) * 3.8;
                revNoiseData[i] = revBase + revRipple + revNoise;
            }

            double noiseLimit = 70.0; // 噪音上限一致
            double noiseMax = Math.Max(fwdBase, revBase) + 15;

            NoiseWaveformSeries = new ISeries[]
            {
                new LineSeries<double>
                {
                    Name = "正转噪音",
                    Values = fwdNoiseData,
                    Stroke = new SolidColorPaint(SKColor.Parse("#FFA500"), 1.8f),
                    Fill = new SolidColorPaint(SKColor.Parse("#0AFFA500")), // 填充半透明区域
                    GeometrySize = 0,
                    LineSmoothness = 0.15
                },
                new LineSeries<double>
                {
                    Name = "反转噪音",
                    Values = revNoiseData,
                    Stroke = new SolidColorPaint(SKColor.Parse("#A855F7"), 1.8f),
                    Fill = new SolidColorPaint(SKColor.Parse("#0AA855F7")), // 填充半透明区域
                    GeometrySize = 0,
                    LineSmoothness = 0.15
                },
                new LineSeries<double>
                {
                    Name = $"上限 ({noiseLimit} dB)",
                    Values = Enumerable.Repeat(noiseLimit, sampleCount).ToArray(),
                    Stroke = new SolidColorPaint(SKColor.Parse("#F74C5B"), 1.5f)
                    {
                        PathEffect = new DashEffect(new float[] { 6, 4 }, 0)
                    },
                    Fill = null,
                    GeometrySize = 0,
                    LineSmoothness = 0
                }
            };

            NoiseWaveformYAxes = new Axis[]
            {
                new Axis
                {
                    LabelsPaint = new SolidColorPaint(SKColor.Parse("#6E7C8A")),
                    SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#20232C")),
                    MinLimit = Math.Min(fwdBase, revBase) - 15,
                    MaxLimit = Math.Max(noiseMax, noiseLimit + 5),
                    Labeler = val => $"{val:F0}"
                }
            };

            WaveformXAxes = new Axis[]
            {
                new Axis
                {
                    LabelsPaint = new SolidColorPaint(SKColor.Parse("#6E7C8A")),
                    SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#20232C")),
                    MinLimit = 0,
                    MaxLimit = sampleCount,
                    Labeler = val => $"{(int)val}"
                }
            };
        }

        // ---- 任务一：快捷时间过滤 ----
        [ObservableProperty]
        private QuickTimeFilter? _selectedQuickFilter = null;

        partial void OnSelectedQuickFilterChanged(QuickTimeFilter? value)
        {
            if (value == null) return;

            _isApplyingQuickFilter = true;
            try
            {
                switch (value.Value)
                {
                    case QuickTimeFilter.LastHour:
                        StartDate = DateTime.Now.AddHours(-1);
                        EndDate = DateTime.Now;
                        break;
                    case QuickTimeFilter.CurrentShift:
                        var now = DateTime.Now;
                        DateTime shiftStart, shiftEnd;
                        if (now.Hour >= 8 && now.Hour < 20)
                        {
                            // 白班 8:00 - 20:00
                            shiftStart = now.Date.AddHours(8);
                            shiftEnd = now.Date.AddHours(20);
                        }
                        else
                        {
                            // 夜班 20:00 - 次日 8:00
                            if (now.Hour >= 20)
                            {
                                shiftStart = now.Date.AddHours(20);
                                shiftEnd = now.Date.AddDays(1).AddHours(8);
                            }
                            else
                            {
                                shiftStart = now.Date.AddDays(-1).AddHours(20);
                                shiftEnd = now.Date.AddHours(8);
                            }
                        }
                        StartDate = shiftStart;
                        EndDate = shiftEnd;
                        break;
                    case QuickTimeFilter.Today:
                        StartDate = DateTime.Today;
                        EndDate = DateTime.Today.AddHours(23).AddMinutes(59).AddSeconds(59);
                        break;
                }
            }
            finally
            {
                _isApplyingQuickFilter = false;
            }

            if (!_isResetting)
            {
                _ = SearchAsync();
            }
        }

        partial void OnSelectedResultFilterChanged(string value)
        {
            if (!_isResetting)
            {
                _ = SearchAsync();
            }
        }

        partial void OnSearchBarcodeChanged(string value)
        {
            if (string.IsNullOrEmpty(value) && !_isResetting)
            {
                _ = SearchAsync();
            }
        }

        public List<string> StationStatuses { get; } = new() { "OK", "OK", "OK", "OK", "OK" };

        public ObservableCollection<MotorTestRecordModel> TestResults { get; } = new();

        private List<MotorTestResult> _cachedResults = new();

        public HistoryViewModel()
            : this(BackendRuntime.Shared.Repository)
        {
        }

        public HistoryViewModel(IMotorTestRepository repository)
        {
            _repository = repository;
            // Initialize with an initial search to populate UI
            SearchCommand.Execute(null);
        }

        partial void OnCurrentPageChanged(int value)
        {
            RenderCurrentPage();
        }

        private void RenderCurrentPage()
        {
            TestResults.Clear();
            if (_cachedResults == null || _cachedResults.Count == 0) return;

            var pageItems = _cachedResults
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize);

            foreach (var r in pageItems)
            {
                TestResults.Add(new MotorTestRecordModel
                {
                    Barcode = r.Barcode,
                    TestTime = r.TestTime,
                    FinalResult = r.FinalResult,
                    NoLoadCurrent = r.NoLoadCurrent,
                    NoLoadSpeed = r.NoLoadSpeed,
                    ShaftLength = r.ShaftLength,
                    KnurlDiameter = r.KnurlDiameter,
                    NoLoadResult = r.NoLoadResult,
                    FwdNoise = r.FwdNoise,
                    RevNoise = r.RevNoise,
                    NoiseDiff = r.NoiseDiff,
                    NoiseResult = r.NoiseResult,
                    LoadCurrent = r.LoadCurrent,
                    LoadSpeed = r.LoadSpeed,
                    LoadResult = r.LoadResult
                });
            }
        }

        [RelayCommand]
        private async Task SearchAsync()
        {
            var query = new MotorTestQuery
            {
                Barcode = SearchBarcode,
                ResultFilter = SelectedResultFilter,
                StartTime = StartDate,
                EndTime = EndDate.TimeOfDay == TimeSpan.Zero ? EndDate.Date.AddHours(23).AddMinutes(59).AddSeconds(59) : EndDate
            };

            var raw = await _repository.QueryAsync(query);
            _cachedResults = raw.ToList();

            // Calculate statistic aggregates
            TotalTestsCount = _cachedResults.Count;
            PassedCount = _cachedResults.Count(r => r.FinalResult == "OK");
            FailedCount = _cachedResults.Count(r => r.FinalResult == "NG");
            PassRateString = TotalTestsCount == 0 ? "0.00%" : $"{(PassedCount * 100.0 / TotalTestsCount):F2}%";

            // Recalculate pagination metadata
            TotalPages = (int)Math.Ceiling((double)TotalTestsCount / PageSize);
            if (TotalPages < 1) TotalPages = 1;

            if (CurrentPage != 1)
            {
                CurrentPage = 1; // Property trigger will call RenderCurrentPage()
            }
            else
            {
                RenderCurrentPage();
            }
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
            _isResetting = true;
            try
            {
                SearchBarcode = string.Empty;
                SelectedResultFilter = "全部";
                SelectedQuickFilter = null;
                StartDate = DateTime.Today.AddDays(-7);
                EndDate = DateTime.Today;
                SelectedMotor = null;
            }
            finally
            {
                _isResetting = false;
            }
            SearchCommand.Execute(null);
        }

        [RelayCommand]
        private void Export()
        {
            if (_cachedResults == null || _cachedResults.Count == 0)
            {
                ModernMessageBox.Show("当前没有符合条件的测试数据可供导出！", "导出提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string exportPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "电机电性能测试数据导出.csv");
            try
            {
                using var sw = new StreamWriter(exportPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                sw.WriteLine("Barcode,TestTime,FinalResult,NoLoadCurrent(A),NoLoadSpeed(r/min),FwdNoise(dB),RevNoise(dB),LoadCurrent(A),LoadSpeed(r/min)");

                var allModels = _cachedResults.Select(r => new MotorTestRecordModel
                {
                    Barcode = r.Barcode,
                    TestTime = r.TestTime,
                    FinalResult = r.FinalResult,
                    NoLoadCurrent = r.NoLoadCurrent,
                    NoLoadSpeed = r.NoLoadSpeed,
                    ShaftLength = r.ShaftLength,
                    KnurlDiameter = r.KnurlDiameter,
                    NoLoadResult = r.NoLoadResult,
                    FwdNoise = r.FwdNoise,
                    RevNoise = r.RevNoise,
                    NoiseDiff = r.NoiseDiff,
                    NoiseResult = r.NoiseResult,
                    LoadCurrent = r.LoadCurrent,
                    LoadSpeed = r.LoadSpeed,
                    LoadResult = r.LoadResult
                }).ToList();

                foreach (var r in allModels)
                {
                    sw.WriteLine(string.Join(",",
                         Escape(r.Barcode),
                         r.TestTime.ToString("yyyy-MM-dd HH:mm:ss"),
                         r.FinalResult,
                         r.NoLoadCurrentText,
                         r.NoLoadSpeedText,
                         r.FwdNoiseText,
                         r.RevNoiseText,
                         r.LoadCurrentText,
                         r.LoadSpeedText));
                }

                ModernMessageBox.Show($"成功导出 {allModels.Count} 条记录至桌面:\n{exportPath}", "数据导出成功", MessageBoxButton.OK, (MessageBoxImage)99);
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show($"导出数据失败: {ex.Message}", "导出错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ---- 任务三：操作面板命令 ----
        [RelayCommand]
        private async Task PrintTraceAsync(CancellationToken cancellationToken)
        {
            if (SelectedMotor == null) return;

            var printDialog = new System.Windows.Controls.PrintDialog();
            if (printDialog.ShowDialog() != true) return;

            IsPrinting = true;
            PrintStatus = "正在准备追溯单...";

            try
            {
                // 1. 在 UI 线程构建 FlowDocument（WPF 要求）
                var doc = TraceDocumentBuilder.Build(SelectedMotor);
                var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;

                // 2. 在 UI 线程预分页（避免后台分页跨线程问题）
                PrintStatus = "正在分页...";
                paginator.ComputePageCount();

                // 3. 获取异步打印写入器（静态方法）
                var writer = System.Printing.PrintQueue.CreateXpsDocumentWriter(printDialog.PrintQueue);
                PrintStatus = "正在发送至打印机...";

                // 4. 异步写入 + 超时控制
                var tcs = new TaskCompletionSource<bool>();

                void OnWritingCompleted(object? s, AsyncCompletedEventArgs e)
                {
                    writer.WritingCompleted -= OnWritingCompleted;
                    if (e.Error != null)
                        tcs.TrySetException(e.Error);
                    else if (e.Cancelled)
                        tcs.TrySetCanceled();
                    else
                        tcs.TrySetResult(true);
                }

                writer.WritingCompleted += OnWritingCompleted;
                writer.WriteAsync(paginator, $"电机追溯单 - {SelectedMotor.Barcode}");

                // 30 秒超时自动取消
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                using var timeoutReg = timeoutCts.Token.Register(() =>
                {
                    writer.CancelAsync();
                    tcs.TrySetCanceled();
                });

                // 同时响应外部取消（如用户关闭页面）
                using var externalReg = cancellationToken.Register(() =>
                {
                    writer.CancelAsync();
                    tcs.TrySetCanceled();
                });

                await tcs.Task;

                ModernMessageBox.Show(
                    $"电机 [{SelectedMotor.Barcode}] 的追溯单已发送至打印机。",
                    "打印成功", MessageBoxButton.OK, (MessageBoxImage)99);
            }
            catch (OperationCanceledException)
            {
                ModernMessageBox.Show("打印超时已取消，请检查打印机连接状态后重试。",
                    "打印取消", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show($"打印失败: {ex.Message}", "打印错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsPrinting = false;
                PrintStatus = string.Empty;
            }
        }

        [RelayCommand]
        private void ViewReport()
        {
            if (SelectedMotor == null) return;

            try
            {
                var reportWindow = new MotorReportWindow(SelectedMotor);
                reportWindow.Owner = Application.Current.MainWindow;

                // 传入当前波形图数据到报告窗口
                reportWindow.SetWaveformData(
                    NoLoadWaveformSeries, NoiseWaveformSeries,
                    WaveformXAxes, NoLoadWaveformYAxes, NoiseWaveformYAxes,
                    WaveformTooltipBgPaint, WaveformTooltipTextPaint);

                reportWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show($"打开报告失败: {ex.Message}", "报告错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void CopyBarcode(string barcode)
        {
            if (!string.IsNullOrEmpty(barcode))
            {
                try
                {
                    Clipboard.SetText(barcode);
                }
                catch (Exception ex)
                {
                    ModernMessageBox.Show($"复制条码失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private static string Escape(object? value)
        {
            string text = value?.ToString() ?? string.Empty;
            return text.Contains(',') || text.Contains('"') || text.Contains('\n')
                ? $"\"{text.Replace("\"", "\"\"")}\""
                : text;
        }
    }
}
