using System;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MotorTestSystem.Services;

namespace MotorTestSystem;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private bool _isFullscreen = false;
    private double _normalWidth = 1280;
    private double _normalHeight = 920;
    private double _normalLeft = 0;
    private double _normalTop = 0;
    private WindowState _normalState = WindowState.Normal;
    private WindowStyle _normalStyle = WindowStyle.SingleBorderWindow;
    private ResizeMode _normalResizeMode = ResizeMode.CanResize;

    public bool IsLoggingOut { get; private set; } = false;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new ViewModels.MainViewModel();

        // 默认登录后全屏
        this.Loaded += (s, e) =>
        {
            _normalWidth = double.IsNaN(this.Width) ? 1280 : this.Width;
            _normalHeight = double.IsNaN(this.Height) ? 920 : this.Height;
            _normalStyle = this.WindowStyle;
            _normalResizeMode = this.ResizeMode;
            _normalState = this.WindowState;

            ToggleFullscreen();
        };

        // 双击鼠标左键退出全屏
        this.PreviewMouseDoubleClick += MainWindow_PreviewMouseDoubleClick;

        // 监听全局点击以关闭用户悬浮卡片
        this.PreviewMouseDown += MainWindow_PreviewMouseDown;

        // 失去焦点、位置或大小改变时关闭悬浮卡片
        this.Deactivated += (s, e) => { UserCardPopup.IsOpen = false; };
        this.LocationChanged += (s, e) => { UserCardPopup.IsOpen = false; };
        this.SizeChanged += (s, e) => { UserCardPopup.IsOpen = false; };
    }

    private void MainWindow_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && _isFullscreen)
        {
            DependencyObject? clickedElement = e.OriginalSource as DependencyObject;
            if (clickedElement != null)
            {
                bool isAvatar = IsVisualDescendant(clickedElement, UserAvatarBorder);
                bool isPopup = UserCardPopup.Child != null && IsVisualDescendant(clickedElement, UserCardPopup.Child);
                if (isAvatar || isPopup)
                {
                    return; // 双击头像或用户信息卡片时，不退出全屏
                }
            }
            ToggleFullscreen(); // 退出全屏
            e.Handled = true;
        }
    }

    private void ToggleFullscreen()
    {
        if (!_isFullscreen)
        {
            // 进入全屏前，如果当前窗口处于非全屏模式，记录当前窗口的位置大小
            if (this.WindowStyle != WindowStyle.None)
            {
                _normalWidth = this.ActualWidth > 0 ? this.ActualWidth : (double.IsNaN(this.Width) ? 1280 : this.Width);
                _normalHeight = this.ActualHeight > 0 ? this.ActualHeight : (double.IsNaN(this.Height) ? 920 : this.Height);
                _normalLeft = double.IsNaN(this.Left) ? 0 : this.Left;
                _normalTop = double.IsNaN(this.Top) ? 0 : this.Top;
                _normalState = this.WindowState;
                _normalStyle = this.WindowStyle;
                _normalResizeMode = this.ResizeMode;
            }

            this.WindowStyle = WindowStyle.None;
            this.ResizeMode = ResizeMode.NoResize;

            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
            }
            this.WindowState = WindowState.Maximized;
            _isFullscreen = true;
        }
        else
        {
            // 退出全屏
            this.WindowStyle = _normalStyle;
            this.ResizeMode = _normalResizeMode;
            this.WindowState = _normalState;

            if (this.WindowState == WindowState.Normal)
            {
                this.Width = _normalWidth > 0 ? _normalWidth : 1280;
                this.Height = _normalHeight > 0 ? _normalHeight : 920;
                
                // 居中恢复窗口位置
                if (_normalLeft <= 0 || double.IsNaN(_normalLeft))
                {
                    this.Left = (SystemParameters.PrimaryScreenWidth - this.Width) / 2;
                    this.Top = (SystemParameters.PrimaryScreenHeight - this.Height) / 2;
                }
                else
                {
                    this.Left = _normalLeft;
                    this.Top = _normalTop;
                }
            }
            _isFullscreen = false;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.F11)
        {
            ToggleFullscreen();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _isFullscreen)
        {
            ToggleFullscreen(); // 退出全屏
            e.Handled = true;
        }
    }

    private void UserAvatarBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        UserCardPopup.IsOpen = !UserCardPopup.IsOpen;
        e.Handled = true;
    }

    private void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        UserCardPopup.IsOpen = false;
        IsLoggingOut = true;
        this.Close();
    }

    private void LanguageToggleButton_Click(object sender, RoutedEventArgs e)
    {
        LanguageManager.Instance.ToggleLanguage();
    }

    private void MainWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (UserCardPopup.IsOpen)
        {
            DependencyObject? clickedElement = e.OriginalSource as DependencyObject;
            if (clickedElement != null)
            {
                bool isAvatar = IsVisualDescendant(clickedElement, UserAvatarBorder);
                bool isPopup = UserCardPopup.Child != null && IsVisualDescendant(clickedElement, UserCardPopup.Child);
                if (!isAvatar && !isPopup)
                {
                    UserCardPopup.IsOpen = false;
                }
            }
        }
    }

    /// <summary>
    /// 窗口关闭时显示保存进度提示（9.5）。
    /// </summary>
    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        // 如果是登出操作，只做简单清理
        if (IsLoggingOut) return;

        e.Cancel = true; // 先取消关闭，等保存完成后再关闭

        // 创建进度对话框
        var progressDialog = new Window
        {
            Title = "正在保存数据...",
            Width = 360,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            WindowStyle = WindowStyle.ToolWindow,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Background = new SolidColorBrush(Color.FromRgb(33, 36, 49)),
            Foreground = Brushes.White,
            FontFamily = new FontFamily("Microsoft YaHei UI"),
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new TextBlock
                    {
                        Text = "正在保存数据，请稍候...",
                        FontSize = 16,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 20)
                    },
                    new ProgressBar
                    {
                        IsIndeterminate = true,
                        Height = 6,
                        Margin = new Thickness(0, 0, 0, 10)
                    },
                    new TextBlock
                    {
                        Text = "正在刷新缓冲区并等待数据写入完成...",
                        FontSize = 12,
                        Foreground = new SolidColorBrush(Color.FromRgb(138, 141, 153)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        TextAlignment = TextAlignment.Center
                    }
                }
            }
        };

        progressDialog.Show();

        try
        {
            // 等待 BatchWriteService 刷新完成（带超时）
            var runtime = Services.BackendRuntime.GetSharedAsync().GetAwaiter().GetResult();
            if (runtime != null)
            {
                var batchWriter = runtime.BatchWriter;
                if (batchWriter != null)
                {
                    // 给 BatchWriteService 一个超时时间来刷新剩余数据
                    await Task.Delay(2000);
                    await batchWriter.StopAsync();
                }

                // 停止 PLC 轮询
                try { await runtime.PollingService.StopAsync(); }
                catch { /* suppress */ }

                // 释放运行时
                runtime.Dispose();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"Error during shutdown: {ex.Message}");
        }
        finally
        {
            progressDialog.Close();
        }

        // 真正关闭窗口
        e.Cancel = false;
        this.Close();
    }

    private bool IsVisualDescendant(DependencyObject child, DependencyObject parent)
    {
        if (child == null || parent == null)
            return false;

        try
        {
            while (child != null)
            {
                if (child == parent)
                    return true;

                if (child is FrameworkContentElement fce)
                {
                    child = fce.Parent ?? fce.TemplatedParent ?? LogicalTreeHelper.GetParent(fce);
                }
                else if (child is ContentElement ce)
                {
                    child = ContentOperations.GetParent(ce) ?? LogicalTreeHelper.GetParent(ce);
                }
                else if (child is Visual || child is System.Windows.Media.Media3D.Visual3D)
                {
                    child = VisualTreeHelper.GetParent(child);
                }
                else
                {
                    child = LogicalTreeHelper.GetParent(child);
                }
            }
        }
        catch
        {
            // Fail-safe to prevent application crash on tree traversal
        }
        return false;
    }
}