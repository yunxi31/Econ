using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;

namespace MotorTestSystem
{
    public partial class LoginWindow : Window
    {
        public string AuthenticatedUser { get; private set; } = "操作员";

        public LoginWindow()
        {
            InitializeComponent();
            UpdateUsernameDefault();
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
            DialogResult = false;
            Close();
        }

        private void RoleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                UpdateUsernameDefault();
            }
        }

        private void UpdateUsernameDefault()
        {
            if (UsernameTextBox == null) return;

            switch (RoleComboBox.SelectedIndex)
            {
                case 0:
                    UsernameTextBox.Text = "operator";
                    PasswordBox.Password = "";
                    break;
                case 1:
                    UsernameTextBox.Text = "technician";
                    PasswordBox.Password = "";
                    break;
                case 2:
                    UsernameTextBox.Text = "admin";
                    PasswordBox.Password = "";
                    break;
            }
            ErrorTextBlock.Visibility = Visibility.Collapsed;
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            PerformLogin();
        }

        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                PerformLogin();
            }
        }

        private void PerformLogin()
        {
            string username = UsernameTextBox.Text.Trim();
            string password = PasswordBox.Password;

            if (string.IsNullOrEmpty(username))
            {
                ShowError("请输入用户名！");
                return;
            }

            bool isSuccess = false;
            string userDisplayName = "";

            switch (RoleComboBox.SelectedIndex)
            {
                case 0: // 操作员
                    // 操作员允许空密码或者任意密码，或是 op123
                    if (string.IsNullOrEmpty(password) || password == "op123" || password == "123")
                    {
                        isSuccess = true;
                        userDisplayName = $"操作员 ({username})";
                    }
                    else
                    {
                        ShowError("操作员密码错误！(提示：可为空或输入 123)");
                    }
                    break;

                case 1: // 技术员
                    if (password == "tech123" || password == "456")
                    {
                        isSuccess = true;
                        userDisplayName = $"技术员 ({username})";
                    }
                    else
                    {
                        ShowError("技术员密码错误！(提示：tech123 或 456)");
                    }
                    break;

                case 2: // 管理员
                    if (password == "admin123" || password == "789")
                    {
                        isSuccess = true;
                        userDisplayName = $"管理员 ({username})";
                    }
                    else
                    {
                        ShowError("管理员密码错误！(提示：admin123 或 789)");
                    }
                    break;
            }

            if (isSuccess)
            {
                AuthenticatedUser = userDisplayName;
                DialogResult = true;
                Close();
            }
        }

        private void ShowError(string message)
        {
            ErrorTextBlock.Text = message;
            ErrorTextBlock.Visibility = Visibility.Visible;
        }
    }
}
