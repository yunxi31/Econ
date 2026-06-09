using System.Windows;
using System.Windows.Controls;
using MotorTestSystem.ViewModels;

namespace MotorTestSystem.Views
{
    public partial class UserEditDialogView : UserControl
    {
        public UserEditDialogView()
        {
            InitializeComponent();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                Window.GetWindow(this)?.DragMove();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            var win = Window.GetWindow(this);
            if (win != null)
            {
                win.DialogResult = false;
                win.Close();
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var win = Window.GetWindow(this);
            if (win == null) return;

            var vm = DataContext as UserEditDialogViewModel;
            if (vm == null) return;

            // Fetch password fields securely from PasswordBoxes
            vm.Password = PbPassword.Password;
            vm.ConfirmPassword = PbConfirmPassword.Password;

            // Validation
            if (string.IsNullOrWhiteSpace(vm.Account))
            {
                MessageBox.Show("账号不能为空！", "输入验证", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(vm.Name))
            {
                MessageBox.Show("姓名不能为空！", "输入验证", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check passwords match if it's a new user or password is provided
            if (vm.Title.Contains("新增"))
            {
                if (string.IsNullOrEmpty(vm.Password))
                {
                    MessageBox.Show("请输入登录密码！", "输入验证", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (vm.Password != vm.ConfirmPassword)
                {
                    MessageBox.Show("两次输入的密码不一致！", "输入验证", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            else
            {
                // If editing and password is not empty, check match
                if (!string.IsNullOrEmpty(vm.Password) && vm.Password != vm.ConfirmPassword)
                {
                    MessageBox.Show("两次输入的密码不一致！", "输入验证", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            win.DialogResult = true;
            win.Close();
        }
    }
}
