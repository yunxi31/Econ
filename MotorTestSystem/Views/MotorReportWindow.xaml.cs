using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Xps;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using MotorTestSystem.ViewModels;
using SkiaSharp;

namespace MotorTestSystem.Views
{
    public class ThresholdCheckItem
    {
        public string Item { get; set; } = string.Empty;
        public string Actual { get; set; } = string.Empty;
        public string Lower { get; set; } = string.Empty;
        public string Upper { get; set; } = string.Empty;
        public string Judge { get; set; } = string.Empty;
        public bool IsPass { get; set; }
    }

    public partial class MotorReportWindow : Window
    {
        private readonly MotorTestRecordModel _motor;

        public MotorReportWindow(MotorTestRecordModel motor)
        {
            InitializeComponent();
            _motor = motor;

            // 标题栏条码
            ReportBarcode.Text = $"[{motor.Barcode}]";

            // 一、基本信息
            TxtBarcode.Text = motor.Barcode;
            TxtTestTime.Text = motor.TestTime.ToString("yyyy-MM-dd HH:mm:ss");
            TxtReportTime.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // 最终判定徽章
            SetResultBadge(FinalResultBadge, TxtFinalResult, motor.FinalResult);

            // 二、空载测试
            TxtNoLoadCurrent.Text = motor.NoLoadCurrent?.ToString("F3") ?? "-";
            TxtNoLoadSpeed.Text = motor.NoLoadSpeed?.ToString("F0") ?? "-";
            TxtShaftLength.Text = motor.ShaftLength?.ToString("F3") ?? "-";
            TxtKnurlDiameter.Text = motor.KnurlDiameter?.ToString("F3") ?? "-";
            SetResultBadge(NoLoadResultBadge, TxtNoLoadResult,
                motor.NoLoadResult ?? GetStageResult(motor, "NoLoad"));

            // 三、噪音测试
            TxtFwdNoise.Text = motor.FwdNoise?.ToString("F1") ?? "-";
            TxtRevNoise.Text = motor.RevNoise?.ToString("F1") ?? "-";
            TxtNoiseDiff.Text = motor.NoiseDiff?.ToString("F1") ?? "-";
            SetResultBadge(NoiseResultBadge, TxtNoiseResult,
                motor.NoiseResult ?? GetStageResult(motor, "Noise"));

            // 四、负载测试
            TxtLoadCurrent.Text = motor.LoadCurrent?.ToString("F3") ?? "-";
            TxtLoadSpeed.Text = motor.LoadSpeed?.ToString("F0") ?? "-";
            SetResultBadge(LoadResultBadge, TxtLoadResult,
                motor.LoadResult ?? GetStageResult(motor, "Load"));

            // 五、阈值判定表
            PopulateThresholdGrid(motor);

            // 七、结论
            TxtConclusion.Text = GenerateConclusion(motor);

            // 设置 DataContext 以便波形图绑定
            // 注意：波形图数据由外部通过 ViewModel 传入
        }

        /// <summary>
        /// 设置波形图数据（由 HistoryViewModel 调用）
        /// </summary>
        public void SetWaveformData(
            ISeries[] noLoadSeries, ISeries[] noiseSeries,
            Axis[] xAxes, Axis[] noLoadYAxes, Axis[] noiseYAxes,
            SolidColorPaint tooltipBg, SolidColorPaint tooltipText)
        {
            var vm = new ReportWaveformViewModel
            {
                NoLoadWaveformSeries = noLoadSeries,
                NoiseWaveformSeries = noiseSeries,
                WaveformXAxes = xAxes,
                NoLoadWaveformYAxes = noLoadYAxes,
                NoiseWaveformYAxes = noiseYAxes,
                WaveformTooltipBgPaint = tooltipBg,
                WaveformTooltipTextPaint = tooltipText
            };
            DataContext = vm;
        }

        private void PopulateThresholdGrid(MotorTestRecordModel motor)
        {
            var items = new List<ThresholdCheckItem>();

            // 空载电流: 上限 2.5A
            items.Add(new ThresholdCheckItem
            {
                Item = "空载电流 (A)",
                Actual = motor.NoLoadCurrent?.ToString("F3") ?? "-",
                Lower = "-",
                Upper = "2.500",
                Judge = motor.NoLoadCurrent.HasValue ? (motor.NoLoadCurrent > 2.5 ? "NG" : "OK") : "-",
                IsPass = !motor.NoLoadCurrent.HasValue || motor.NoLoadCurrent <= 2.5
            });

            // 空载转速: 1800~2200 r/min
            items.Add(new ThresholdCheckItem
            {
                Item = "空载转速 (r/min)",
                Actual = motor.NoLoadSpeed?.ToString("F0") ?? "-",
                Lower = "1800",
                Upper = "2200",
                Judge = motor.NoLoadSpeed.HasValue
                    ? (motor.NoLoadSpeed < 1800 || motor.NoLoadSpeed > 2200 ? "NG" : "OK") : "-",
                IsPass = !motor.NoLoadSpeed.HasValue || (motor.NoLoadSpeed >= 1800 && motor.NoLoadSpeed <= 2200)
            });

            // 正转噪音: 上限 70 dB
            items.Add(new ThresholdCheckItem
            {
                Item = "正转噪音 (dB)",
                Actual = motor.FwdNoise?.ToString("F1") ?? "-",
                Lower = "-",
                Upper = "70.0",
                Judge = motor.FwdNoise.HasValue ? (motor.FwdNoise > 70.0 ? "NG" : "OK") : "-",
                IsPass = !motor.FwdNoise.HasValue || motor.FwdNoise <= 70.0
            });

            // 反转噪音: 上限 70 dB
            items.Add(new ThresholdCheckItem
            {
                Item = "反转噪音 (dB)",
                Actual = motor.RevNoise?.ToString("F1") ?? "-",
                Lower = "-",
                Upper = "70.0",
                Judge = motor.RevNoise.HasValue ? (motor.RevNoise > 70.0 ? "NG" : "OK") : "-",
                IsPass = !motor.RevNoise.HasValue || motor.RevNoise <= 70.0
            });

            // 负载电流: 上限 3.0A
            items.Add(new ThresholdCheckItem
            {
                Item = "负载电流 (A)",
                Actual = motor.LoadCurrent?.ToString("F3") ?? "-",
                Lower = "-",
                Upper = "3.000",
                Judge = motor.LoadCurrent.HasValue ? (motor.LoadCurrent > 3.0 ? "NG" : "OK") : "-",
                IsPass = !motor.LoadCurrent.HasValue || motor.LoadCurrent <= 3.0
            });

            // 负载转速: 下限 1000 r/min
            items.Add(new ThresholdCheckItem
            {
                Item = "负载转速 (r/min)",
                Actual = motor.LoadSpeed?.ToString("F0") ?? "-",
                Lower = "1000",
                Upper = "-",
                Judge = motor.LoadSpeed.HasValue
                    ? (motor.LoadSpeed < 1000 ? "NG" : "OK") : "-",
                IsPass = !motor.LoadSpeed.HasValue || motor.LoadSpeed >= 1000
            });

            ThresholdGrid.ItemsSource = items;
        }

        private static string GetStageResult(MotorTestRecordModel motor, string stage)
        {
            // 基于阈值判定各阶段结果
            return stage switch
            {
                "NoLoad" => motor.IsNoLoadCurrentAbnormal || motor.IsNoLoadSpeedAbnormal ? "NG" : "OK",
                "Noise" => motor.IsFwdNoiseAbnormal || motor.IsRevNoiseAbnormal ? "NG" : "OK",
                "Load" => motor.IsLoadCurrentAbnormal || motor.IsLoadSpeedAbnormal ? "NG" : "OK",
                _ => "-"
            };
        }

        private static string GenerateConclusion(MotorTestRecordModel motor)
        {
            bool isOk = motor.FinalResult == "OK";
            string barcode = motor.Barcode;
            string time = motor.TestTime.ToString("yyyy-MM-dd HH:mm:ss");

            if (isOk)
            {
                return $"条码 {barcode} 的电机已于 {time} 完成全部电性能测试，" +
                       "空载电流、空载转速、正/反转噪音、负载电流、负载转速等各项指标均在标准范围内，" +
                       "综合判定：合格（OK）。该产品可进入下一工序。";
            }
            else
            {
                var reasons = new List<string>();
                if (motor.IsNoLoadCurrentAbnormal) reasons.Add($"空载电流超限（{motor.NoLoadCurrent:F3}A > 2.500A）");
                if (motor.IsNoLoadSpeedAbnormal) reasons.Add($"空载转速异常（{motor.NoLoadSpeed:F0} r/min）");
                if (motor.IsFwdNoiseAbnormal) reasons.Add($"正转噪音超限（{motor.FwdNoise:F1}dB > 70.0dB）");
                if (motor.IsRevNoiseAbnormal) reasons.Add($"反转噪音超限（{motor.RevNoise:F1}dB > 70.0dB）");
                if (motor.IsLoadCurrentAbnormal) reasons.Add($"负载电流超限（{motor.LoadCurrent:F3}A > 3.000A）");
                if (motor.IsLoadSpeedAbnormal) reasons.Add($"负载转速异常（{motor.LoadSpeed:F0} r/min）");

                string reasonText = reasons.Count > 0 ? "不合格项：" + string.Join("；", reasons) + "。" : "";
                return $"条码 {barcode} 的电机已于 {time} 完成全部电性能测试，" +
                       $"{reasonText}综合判定：不合格（NG）。该产品需返修或报废处理。";
            }
        }

        private static void SetResultBadge(System.Windows.Controls.Border badge, TextBlock textBlock, string result)
        {
            if (result == "OK")
            {
                badge.Style = (Style)badge.FindResource("ResultBadgeOK");
                textBlock.Text = "OK";
                textBlock.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#26DE81"));
            }
            else if (result == "NG")
            {
                badge.Style = (Style)badge.FindResource("ResultBadgeNG");
                textBlock.Text = "NG";
                textBlock.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#F74C5B"));
            }
            else
            {
                badge.Style = (Style)badge.FindResource("ResultBadgeOK");
                textBlock.Text = result;
                textBlock.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#8A8D99"));
            }
        }

        private async void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var printDialog = new System.Windows.Controls.PrintDialog();
                if (printDialog.ShowDialog() != true) return;

                // 禁用打印按钮，防止重复点击
                if (sender is Button btn) btn.IsEnabled = false;
                Cursor = System.Windows.Input.Cursors.Wait;

                // 在 UI 线程构建并预分页
                var doc = TraceDocumentBuilder.Build(_motor);
                var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
                paginator.ComputePageCount();

                // 使用异步写入器（静态方法）
                var writer = System.Printing.PrintQueue.CreateXpsDocumentWriter(printDialog.PrintQueue);
                var tcs = new TaskCompletionSource<bool>();

                void OnCompleted(object? s, AsyncCompletedEventArgs ev)
                {
                    writer.WritingCompleted -= OnCompleted;
                    if (ev.Error != null) tcs.TrySetException(ev.Error);
                    else if (ev.Cancelled) tcs.TrySetCanceled();
                    else tcs.TrySetResult(true);
                }

                writer.WritingCompleted += OnCompleted;
                writer.WriteAsync(paginator, $"电机追溯单 - {_motor.Barcode}");

                // 30 秒超时
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                using var reg = cts.Token.Register(() =>
                {
                    writer.CancelAsync();
                    tcs.TrySetCanceled();
                });

                await tcs.Task;

                ModernMessageBox.Show($"电机 [{_motor.Barcode}] 的追溯单已发送至打印机。",
                    "打印成功", MessageBoxButton.OK, (MessageBoxImage)99);
            }
            catch (OperationCanceledException)
            {
                ModernMessageBox.Show("打印超时已取消，请检查打印机连接状态后重试。",
                    "打印取消", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show($"打印失败: {ex.Message}",
                    "打印错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Cursor = System.Windows.Input.Cursors.Arrow;
                if (sender is Button btn2) btn2.IsEnabled = true;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void UIElement_OnPreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = true;
                var eventArg = new System.Windows.Input.MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source = sender
                };
                MainScrollViewer?.RaiseEvent(eventArg);
            }
        }
    }

    /// <summary>
    /// 报告窗口专用的波形图 ViewModel
    /// </summary>
    public class ReportWaveformViewModel
    {
        public ISeries[] NoLoadWaveformSeries { get; set; } = Array.Empty<ISeries>();
        public ISeries[] NoiseWaveformSeries { get; set; } = Array.Empty<ISeries>();
        public Axis[] WaveformXAxes { get; set; } = Array.Empty<Axis>();
        public Axis[] NoLoadWaveformYAxes { get; set; } = Array.Empty<Axis>();
        public Axis[] NoiseWaveformYAxes { get; set; } = Array.Empty<Axis>();
        public SolidColorPaint WaveformTooltipBgPaint { get; set; } = new SolidColorPaint(new SkiaSharp.SKColor(24, 25, 36, 230));
        public SolidColorPaint WaveformTooltipTextPaint { get; set; } = new SolidColorPaint(SkiaSharp.SKColors.White);
    }
}
