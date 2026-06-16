using System.Windows.Controls;

namespace MotorTestSystem.Views
{
    /// <summary>
    /// DataPersistenceMonitorControl.xaml — 数据持久化状态监控面板。
    /// 显示写入通道占用率、死信队列数量、丢弃计数、数据丢失告警和手动补传按钮。
    /// 本控件完全独立，不侵入现有 DashboardView 布局。
    /// </summary>
    public partial class DataPersistenceMonitorControl : UserControl
    {
        public DataPersistenceMonitorControl()
        {
            InitializeComponent();
        }
    }
}
