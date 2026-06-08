using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MotorTestSystem.Models;
using MotorTestSystem.Services;

namespace MotorTestSystem.ViewModels
{
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

        public ObservableCollection<MotorTestResult> TestResults { get; } = new();

        public HistoryViewModel()
            : this(BackendRuntime.Shared.Repository)
        {
        }

        public HistoryViewModel(IMotorTestRepository repository)
        {
            _repository = repository;
            Search();
        }

        [RelayCommand]
        private void Search()
        {
            var query = new MotorTestQuery
            {
                Barcode = SearchBarcode,
                ResultFilter = SelectedResultFilter,
                StartTime = StartDate.Date,
                EndTime = EndDate.Date.AddDays(1).AddTicks(-1)
            };

            var results = _repository.QueryAsync(query).GetAwaiter().GetResult();
            TestResults.Clear();
            foreach (var record in results)
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
            string exportPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "电机电性能测试数据导出.csv");
            try
            {
                using var sw = new StreamWriter(exportPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                sw.WriteLine("Barcode,TestTime,FinalResult,NoLoadCurrent(A),NoLoadSpeed(r/min),ShaftLength(mm),KnurlDiameter(mm),NoLoadResult,FwdNoise(dB),RevNoise(dB),NoiseDiff(dB),NoiseResult,LoadCurrent(A),LoadSpeed(r/min),LoadResult");

                foreach (var r in TestResults)
                {
                    sw.WriteLine(string.Join(",",
                        Escape(r.Barcode),
                        r.TestTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        r.FinalResult,
                        r.NoLoadCurrent,
                        r.NoLoadSpeed,
                        r.ShaftLength,
                        r.KnurlDiameter,
                        r.NoLoadResult,
                        r.FwdNoise,
                        r.RevNoise,
                        r.NoiseDiff,
                        r.NoiseResult,
                        r.LoadCurrent,
                        r.LoadSpeed,
                        r.LoadResult));
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
