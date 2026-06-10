using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using MotorTestSystem.Models;
using MotorTestSystem.Services;

namespace MotorTestSystem
{
    public partial class LoginWindow : Window
    {
        private readonly IAuthService _authService;

        /// <summary>认证成功后的用户信息</summary>
        public AppUser? AuthenticatedUser { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();
            _authService = BackendRuntime.Shared.AuthService;
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
                    PasswordInput.Password = "";
                    break;
                case 1:
                    UsernameTextBox.Text = "maintainer";
                    PasswordInput.Password = "";
                    break;
                case 2:
                    UsernameTextBox.Text = "admin";
                    PasswordInput.Password = "";
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
            string password = PasswordInput.Password;

            if (string.IsNullOrEmpty(username))
            {
                ShowError("请输入用户名！");
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                // 操作员允许空密码（等同于 "123"）
                if (RoleComboBox.SelectedIndex == 0)
                {
                    password = "123";
                }
                else
                {
                    ShowError("请输入密码！");
                    return;
                }
            }

            // 使用 AuthService 认证
            if (_authService.Login(username, password, out string errorMessage))
            {
                AuthenticatedUser = _authService.CurrentUser;
                DialogResult = true;
                Close();
            }
            else
            {
                ShowError(errorMessage);
            }
        }

        private void ShowError(string message)
        {
            ErrorTextBlock.Text = message;
            ErrorTextBlock.Visibility = Visibility.Visible;
        }
    }
}
