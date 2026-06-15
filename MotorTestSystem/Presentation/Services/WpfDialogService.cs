using System;
using System.Windows;
using System.Windows.Documents;
using System.Threading.Tasks;
using Microsoft.Win32;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using MotorTestSystem.Services;
using MotorTestSystem.ViewModels;
using MotorTestSystem.Views;

namespace MotorTestSystem.Presentation.Services
{
    public class WpfDialogService : IDialogService
    {
        public Task<MessageBoxResult> ShowMessageAsync(string message, string title, MessageBoxButton button, MessageBoxImage icon)
        {
            var result = ModernMessageBox.Show(message, title, button, icon);
            return Task.FromResult(result);
        }

        public string? ShowSaveFileDialog(string filter, string defaultFileName)
        {
            var dialog = new SaveFileDialog
            {
                Filter = filter,
                FileName = defaultFileName
            };

            if (dialog.ShowDialog() == true)
            {
                return dialog.FileName;
            }
            return null;
        }

        public bool ShowPrintDialog(FlowDocument document)
        {
            var printDialog = new System.Windows.Controls.PrintDialog();
            if (printDialog.ShowDialog() != true) return false;

            var paginator = ((IDocumentPaginatorSource)document).DocumentPaginator;
            printDialog.PrintDocument(paginator, "电机追溯单");
            return true;
        }

        public void ShowReportWindow(
            MotorTestRecordModel motor,
            ISeries[] noLoadSeries,
            ISeries[] noiseSeries,
            Axis[] xAxes,
            Axis[] noLoadYAxes,
            Axis[] noiseYAxes,
            SolidColorPaint tooltipBg,
            SolidColorPaint tooltipText)
        {
            var reportWindow = new MotorReportWindow(motor);
            if (Application.Current != null && Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
            {
                reportWindow.Owner = Application.Current.MainWindow;
            }

            reportWindow.SetWaveformData(
                noLoadSeries, noiseSeries,
                xAxes, noLoadYAxes, noiseYAxes,
                tooltipBg, tooltipText);

            reportWindow.ShowDialog();
        }

        public UserEditResult? ShowUserEditDialog(string title, string account, string name, string role, bool isEnabled)
        {
            var dialogViewModel = new UserEditDialogViewModel
            {
                Title = title,
                Account = account,
                Name = name,
                SelectedRole = string.IsNullOrEmpty(role) ? "操作员" : role,
                IsEnabled = isEnabled
            };

            var dialog = new UserEditWindow
            {
                DataContext = dialogViewModel
            };

            if (Application.Current != null && Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
            {
                dialog.Owner = Application.Current.MainWindow;
            }

            if (dialog.ShowDialog() == true)
            {
                return new UserEditResult(
                    dialogViewModel.Account,
                    dialogViewModel.Name,
                    dialogViewModel.Password,
                    dialogViewModel.SelectedRole,
                    dialogViewModel.IsEnabled);
            }
            return null;
        }

        public void SetClipboardText(string text)
        {
            try
            {
                Clipboard.SetText(text);
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show($"复制到剪贴板失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
