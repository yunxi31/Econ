using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MotorTestSystem.Models;

namespace MotorTestSystem.ViewModels
{
    public partial class HistoryViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _searchBarcode = string.Empty;

        [ObservableProperty]
        private string _selectedResultFilter = "全部"; // 全部, OK, NG

        public List<string> ResultFilters { get; } = new() { "全部", "OK", "NG" };

        [ObservableProperty]
        private DateTime _startDate = DateTime.Now.AddDays(-7);

        [ObservableProperty]
        private DateTime _endDate = DateTime.Now;

        public ObservableCollection<MotorTestResult> TestResults { get; } = new();

        private readonly List<MotorTestResult> _mockDatabase = new();

        public HistoryViewModel()
        {
            GenerateMockData();
            Search(); // Load initial data
        }

        private void GenerateMockData()
        {
            var baseTime = DateTime.Now.AddDays(-5);
            var random = new Random();

            for (int i = 0; i < 50; i++)
            {
                string barcode = $"DES-SR-150GEN{1992900399100 + i}";
                var time = baseTime.AddMinutes(random.Next(10, 1400 * i));
                
                // Normal case
                double noLoadCurrent = Math.Round(1.8 + random.NextDouble() * 0.4, 3);
                int noLoadSpeed = random.Next(2000, 2150);
                double shaftLength = Math.Round(32.4 + random.NextDouble() * 0.1, 3);
                double knurlDiameter = Math.Round(4.4 + random.NextDouble() * 0.05, 3);
                string noLoadResult = "OK";

                double fwdNoise = Math.Round(60.0 + random.NextDouble() * 8.0, 1);
                double revNoise = Math.Round(52.0 + random.NextDouble() * 6.0, 1);
                double noiseDiff = Math.Round(Math.Abs(fwdNoise - revNoise), 1);
                string noiseResult = noiseDiff > 10.0 ? "NG" : "OK";

                double loadCurrent = Math.Round(2.2 + random.NextDouble() * 0.5, 3);
                int loadSpeed = random.Next(1150, 1250);
                string loadResult = "OK";

                // Introduce mock NG and NULL cases
                if (i % 12 == 0) // NG Case
                {
                    fwdNoise = 75.5;
                    noiseDiff = 15.0;
                    noiseResult = "NG";
                }
                else if (i % 15 == 0) // Missing stage (NULL)
                {
                    loadCurrent = 0;
                    loadSpeed = 0;
                    loadResult = null; // Stage 3 missing
                }

                string finalResult = (noLoadResult == "OK" && noiseResult == "OK" && loadResult == "OK") ? "OK" : "NG";

                _mockDatabase.Add(new MotorTestResult
                {
                    Barcode = barcode,
                    TestTime = time,
                    NoLoadCurrent = noLoadCurrent,
                    NoLoadSpeed = noLoadSpeed,
                    ShaftLength = shaftLength,
                    KnurlDiameter = knurlDiameter,
                    NoLoadResult = noLoadResult,

                    FwdNoise = fwdNoise,
                    RevNoise = revNoise,
                    NoiseDiff = noiseDiff,
                    NoiseResult = noiseResult,

                    LoadCurrent = loadResult != null ? loadCurrent : null,
                    LoadSpeed = loadResult != null ? loadSpeed : null,
                    LoadResult = loadResult,

                    FinalResult = finalResult
                });
            }
        }

        [RelayCommand]
        private void Search()
        {
            TestResults.Clear();
            var filtered = _mockDatabase
                .Where(r => string.IsNullOrEmpty(SearchBarcode) || r.Barcode.Contains(SearchBarcode, StringComparison.OrdinalIgnoreCase))
                .Where(r => SelectedResultFilter == "全部" || r.FinalResult == SelectedResultFilter)
                .Where(r => r.TestTime >= StartDate.Date && r.TestTime <= EndDate.Date.AddDays(1))
                .OrderByDescending(r => r.TestTime);

            foreach (var record in filtered)
            {
                TestResults.Add(record);
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
            // Simulate Excel Export
            string exportPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "电机电性能测试数据导出.csv");
            try
            {
                using (var sw = new StreamWriter(exportPath, false, System.Text.Encoding.UTF8))
                {
                    // Header
                    sw.WriteLine("电机条码,检测时间,最终结果,空载电流(A),空载转速(r/min),轴伸长度(mm),滚花直径(mm),空载结果,正转噪音(dB),反转噪音(dB),噪音差(dB),噪音结果,负载电流(A),负载转速(r/min),负载结果");
                    
                    foreach (var r in TestResults)
                    {
                        sw.WriteLine($"{r.Barcode},{r.TestTime:yyyy-MM-dd HH:mm:ss},{r.FinalResult}," +
                                     $"{r.NoLoadCurrent},{r.NoLoadSpeed},{r.ShaftLength},{r.KnurlDiameter},{r.NoLoadResult}," +
                                     $"{r.FwdNoise},{r.RevNoise},{r.NoiseDiff},{r.NoiseResult}," +
                                     $"{r.LoadCurrent},{r.LoadSpeed},{r.LoadResult}");
                    }
                }
                MessageBox.Show($"成功导出 {TestResults.Count} 条记录至桌面:\n{exportPath}", "数据导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出数据失败: {ex.Message}", "导出错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
