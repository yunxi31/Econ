using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MotorTestSystem;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private bool _isFullscreen = false;
    private double _normalWidth;
    private double _normalHeight;
    private double _normalLeft;
    private double _normalTop;
    private WindowState _normalState;
    private WindowStyle _normalStyle;
    private ResizeMode _normalResizeMode;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new ViewModels.MainViewModel();
        this.StateChanged += MainWindow_StateChanged;
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (MaximizeIcon == null || BtnMaximize == null) return;

        if (this.WindowState == WindowState.Maximized)
        {
            MaximizeIcon.Data = Geometry.Parse("M3,5 L3,3 L13,3 L13,13 L11,13 M1,5 L11,5 L11,15 L1,15 Z");
            BtnMaximize.ToolTip = "向下还原";
        }
        else if (this.WindowState == WindowState.Normal)
        {
            MaximizeIcon.Data = Geometry.Parse("M1,1 L15,1 L15,15 L1,15 Z");
            BtnMaximize.ToolTip = "最大化";
        }
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
    {
        this.WindowState = WindowState.Minimized;
    }

    private void BtnMaximize_Click(object sender, RoutedEventArgs e)
    {
        if (this.WindowState == WindowState.Maximized)
        {
            this.WindowState = WindowState.Normal;
        }
        else
        {
            this.WindowState = WindowState.Maximized;
        }
    }

    private void BtnFullscreen_Click(object sender, RoutedEventArgs e)
    {
        ToggleFullscreen();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private void ToggleFullscreen()
    {
        if (!_isFullscreen)
        {
            // 进入全屏
            _normalWidth = this.Width;
            _normalHeight = this.Height;
            _normalLeft = this.Left;
            _normalTop = this.Top;
            _normalState = this.WindowState;
            _normalStyle = this.WindowStyle;
            _normalResizeMode = this.ResizeMode;

            this.WindowStyle = WindowStyle.None;
            this.ResizeMode = ResizeMode.NoResize;

            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
            }
            this.WindowState = WindowState.Maximized;
            _isFullscreen = true;

            // 改变全屏图标为“退出全屏”
            FullscreenIcon.Data = Geometry.Parse("M5,1 L5,5 L1,5 M11,5 L11,1 L15,5 M15,11 L11,11 L11,15 M1,11 L5,11 L5,15");
            BtnFullscreen.ToolTip = "退出全屏 (F11)";
        }
        else
        {
            // 退出全屏
            this.WindowStyle = _normalStyle;
            this.ResizeMode = _normalResizeMode;
            this.WindowState = _normalState;

            if (this.WindowState == WindowState.Normal)
            {
                this.Width = _normalWidth;
                this.Height = _normalHeight;
                this.Left = _normalLeft;
                this.Top = _normalTop;
            }
            _isFullscreen = false;

            // 恢复全屏图标
            FullscreenIcon.Data = Geometry.Parse("M1,5 L1,1 L5,1 M11,1 L15,1 L15,5 M15,11 L15,15 L11,15 M5,15 L1,15 L1,11");
            BtnFullscreen.ToolTip = "全屏 (F11)";
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
}