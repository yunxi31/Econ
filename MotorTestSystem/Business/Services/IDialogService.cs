using System.Windows;
using System.Windows.Documents;
using System.Threading.Tasks;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using MotorTestSystem.ViewModels;

namespace MotorTestSystem.Services
{
    public interface IDialogService
    {
        Task<MessageBoxResult> ShowMessageAsync(string message, string title, MessageBoxButton button, MessageBoxImage icon);
        string? ShowSaveFileDialog(string filter, string defaultFileName);
        bool ShowPrintDialog(FlowDocument document);
        void ShowReportWindow(
            MotorTestRecordModel motor,
            ISeries[] noLoadSeries,
            ISeries[] noiseSeries,
            Axis[] xAxes,
            Axis[] noLoadYAxes,
            Axis[] noiseYAxes,
            SolidColorPaint tooltipBg,
            SolidColorPaint tooltipText);
        UserEditResult? ShowUserEditDialog(string title, string account, string name, string role, bool isEnabled);
        void SetClipboardText(string text);
    }
}
