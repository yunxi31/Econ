using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace MotorTestSystem.Views
{
    public partial class ModernMessageBox : Window
    {
        private MessageBoxResult _result = MessageBoxResult.None;

        public ModernMessageBox()
        {
            InitializeComponent();
        }

        public static MessageBoxResult Show(string message, string title = "系统提示", MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.Information)
        {
            MessageBoxResult result = MessageBoxResult.None;
            
            // 确保在 UI 线程执行
            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    result = ShowInternal(message, title, button, icon);
                });
            }
            else
            {
                result = ShowInternal(message, title, button, icon);
            }

            return result;
        }

        private static MessageBoxResult ShowInternal(string message, string title, MessageBoxButton button, MessageBoxImage icon)
        {
            var dialog = new ModernMessageBox();
            dialog.TitleText.Text = title;
            dialog.MessageText.Text = message;

            // 设置图标类型和色彩
            switch (icon)
            {
                case MessageBoxImage.Information:
                    dialog.MsgIcon.Kind = PackIconKind.InformationOutline;
                    dialog.MsgIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#12DDF7"));
                    break;
                case MessageBoxImage.Warning:
                    dialog.MsgIcon.Kind = PackIconKind.AlertOutline;
                    dialog.MsgIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCC00"));
                    break;
                case MessageBoxImage.Error: // or Hand / Stop
                    dialog.MsgIcon.Kind = PackIconKind.CloseCircleOutline;
                    dialog.MsgIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF5A5F"));
                    break;
                case MessageBoxImage.Question:
                    dialog.MsgIcon.Kind = PackIconKind.HelpCircleOutline;
                    dialog.MsgIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A277FF"));
                    break;
                default:
                    // 默认为 Success (使用 Information 图标，但绿色)
                    dialog.MsgIcon.Kind = PackIconKind.CheckCircleOutline;
                    dialog.MsgIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00E68A"));
                    break;
            }

            // 设置按钮
            switch (button)
            {
                case MessageBoxButton.OK:
                    dialog.BtnOK.Content = "确定";
                    dialog.BtnCancel.Visibility = Visibility.Collapsed;
                    break;
                case MessageBoxButton.OKCancel:
                    dialog.BtnOK.Content = "确定";
                    dialog.BtnCancel.Content = "取消";
                    dialog.BtnCancel.Visibility = Visibility.Visible;
                    break;
                case MessageBoxButton.YesNo:
                    dialog.BtnOK.Content = "是";
                    dialog.BtnCancel.Content = "否";
                    dialog.BtnCancel.Visibility = Visibility.Visible;
                    break;
                default:
                    dialog.BtnOK.Content = "确定";
                    dialog.BtnCancel.Visibility = Visibility.Collapsed;
                    break;
            }

            // 设置 Owner 居中
            if (Application.Current != null && Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
            {
                dialog.Owner = Application.Current.MainWindow;
            }

            dialog.ShowDialog();
            return dialog._result;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _result = MessageBoxResult.Cancel;
            Close();
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            _result = BtnOK.Content.ToString() == "是" ? MessageBoxResult.Yes : MessageBoxResult.OK;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            _result = BtnCancel.Content.ToString() == "否" ? MessageBoxResult.No : MessageBoxResult.Cancel;
            Close();
        }
    }
}
