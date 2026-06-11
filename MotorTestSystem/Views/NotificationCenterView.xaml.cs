using System.Windows.Controls;
using System.Windows.Input;

namespace MotorTestSystem.Views
{
    /// <summary>
    /// Interaction logic for NotificationCenterView.xaml
    /// </summary>
    public partial class NotificationCenterView : UserControl
    {
        public NotificationCenterView()
        {
            InitializeComponent();

            // 选完日期后移走焦点，消除选中高亮残留
            StartDatePicker.SelectedDateChanged += (s, e) =>
            {
                Keyboard.ClearFocus();
                FocusManager.SetFocusedElement(FocusManager.GetFocusScope(this), null);
            };
            EndDatePicker.SelectedDateChanged += (s, e) =>
            {
                Keyboard.ClearFocus();
                FocusManager.SetFocusedElement(FocusManager.GetFocusScope(this), null);
            };
        }
    }
}

