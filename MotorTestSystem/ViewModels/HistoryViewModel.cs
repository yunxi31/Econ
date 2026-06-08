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

        [ObservableProperty]
        private DateTime _startDate = DateTime.Now.AddDays(-7);

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

        public List<string> StationStatuses { get; } = new() { "OK", "OK", "OK", "OK", "OK" };

        public ObservableCollection<MotorTestRecordModel> TestResults { get; } = new();

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
            
            // Add Row 1: Normal OK test
            TestResults.Add(new MotorTestRecordModel
            {
                Barcode = "M260608001",
                TestTime = DateTime.Now.AddMinutes(-5),
                FinalResult = "OK",
                NoLoadCurrent = 1.24,
                NoLoadSpeed = 3025,
                FwdNoise = 55.4,
                RevNoise = 56.1,
                LoadCurrent = 4.12,
                LoadSpeed = 2980
            });

            // Add Row 2: NG test with abnormal values
            TestResults.Add(new MotorTestRecordModel
            {
                Barcode = "M260608002",
                TestTime = DateTime.Now.AddMinutes(-12),
                FinalResult = "NG",
                NoLoadCurrent = 1.85, // Abnormal (> 1.5)
                NoLoadSpeed = 3050,
                FwdNoise = 68.5, // Abnormal (> 60.0)
                RevNoise = 69.2, // Abnormal (> 60.0)
                LoadCurrent = 4.85, // Abnormal (> 4.5)
                LoadSpeed = 2950
            });

            // Add Row 3: NG test with NULL/Missing values
            TestResults.Add(new MotorTestRecordModel
            {
                Barcode = "M260608003",
                TestTime = DateTime.Now.AddMinutes(-20),
                FinalResult = "NG",
                NoLoadCurrent = null,
                NoLoadSpeed = null,
                FwdNoise = null,
                RevNoise = null,
                LoadCurrent = null,
                LoadSpeed = null
            });

            // Let's add some more to represent 10 items for Page 1 of 3
            for (int i = 4; i <= 10; i++)
            {
                TestResults.Add(new MotorTestRecordModel
                {
                    Barcode = $"M26060800{i}",
                    TestTime = DateTime.Now.AddHours(-i),
                    FinalResult = i % 3 == 0 ? "OK" : "NG",
                    NoLoadCurrent = i % 3 == 0 ? 1.22 : 1.76,
                    NoLoadSpeed = 3010,
                    FwdNoise = i % 3 == 0 ? 54.2 : 62.4,
                    RevNoise = 55.0,
                    LoadCurrent = i % 3 == 0 ? 4.05 : null,
                    LoadSpeed = i % 3 == 0 ? 2990 : null
                });
            }
        }

        [RelayCommand]
        private void Search()
        {
            // Query mock logic
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
            StartDate = DateTime.Now.AddDays(-7);
            EndDate = DateTime.Now;
            Search();
        }

        [RelayCommand]
        private void Export()
        {
            string exportPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "电机电性能测试数据导出.csv");
            try
            {
                using var sw = new StreamWriter(exportPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                sw.WriteLine("Barcode,TestTime,FinalResult,NoLoadCurrent(A),NoLoadSpeed(r/min),FwdNoise(dB),RevNoise(dB),LoadCurrent(A),LoadSpeed(r/min)");

                foreach (var r in TestResults)
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

                MessageBox.Show($"成功导出 {TestResults.Count} 条记录至桌面:\n{exportPath}", "数据导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出数据失败: {ex.Message}", "导出错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
