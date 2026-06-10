using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MotorTestSystem.Models;
using MotorTestSystem.Services;
using MotorTestSystem.Views;

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
        public double? FwdNoise { get; set; }
        public double? RevNoise { get; set; }
        public double? LoadCurrent { get; set; }
        public double? LoadSpeed { get; set; }

        // Helper properties for XAML Binding:
        public string NoLoadCurrentText => NoLoadCurrent?.ToString("F2") ?? "NULL";
        public string NoLoadSpeedText => NoLoadSpeed?.ToString("F0") ?? "NULL";
        public string FwdNoiseText => FwdNoise?.ToString("F1") ?? "NULL";
        public string RevNoiseText => RevNoise?.ToString("F1") ?? "NULL";
        public string LoadCurrentText => LoadCurrent?.ToString("F2") ?? "NULL";
        public string LoadSpeedText => LoadSpeed?.ToString("F0") ?? "NULL";

        // Highlighting/Color properties:
        public bool IsNoLoadCurrentAbnormal => NoLoadCurrent > 1.5;
        public bool IsNoLoadSpeedAbnormal => NoLoadSpeed < 2900 || NoLoadSpeed > 3100;
        public bool IsFwdNoiseAbnormal => FwdNoise > 60.0;
        public bool IsRevNoiseAbnormal => RevNoise > 60.0;
        public bool IsLoadCurrentAbnormal => LoadCurrent > 4.5;
        public bool IsLoadSpeedAbnormal => LoadSpeed < 2900 || LoadSpeed > 3100;
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
                    FwdNoise = r.FwdNoise,
                    RevNoise = r.RevNoise,
                    LoadCurrent = r.LoadCurrent,
                    LoadSpeed = r.LoadSpeed
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
                    FwdNoise = r.FwdNoise,
                    RevNoise = r.RevNoise,
                    LoadCurrent = r.LoadCurrent,
                    LoadSpeed = r.LoadSpeed
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
        private void PrintTrace()
        {
            if (SelectedMotor == null) return;
            ModernMessageBox.Show($"正在打印电机 [{SelectedMotor.Barcode}] 的追溯单...", "打印追溯单",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void ViewReport()
        {
            if (SelectedMotor == null) return;
            ModernMessageBox.Show($"正在打开电机 [{SelectedMotor.Barcode}] 的完整报告...", "查看报告",
                MessageBoxButton.OK, MessageBoxImage.Information);
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
