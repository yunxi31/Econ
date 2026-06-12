using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MotorTestSystem.ViewModels
{
    public partial class UserEditDialogViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _title = "新增用户";

        [ObservableProperty]
        private string _account = string.Empty;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _selectedRole = "操作员";

        [ObservableProperty]
        private bool _isEnabled = true;

        [ObservableProperty]
        private bool _isDisabled = false;

        public ObservableCollection<string> Roles { get; } = new() { "管理员", "操作员", "维护员" };

        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;

        partial void OnIsEnabledChanged(bool value)
        {
            if (value)
            {
                _isDisabled = false;
                OnPropertyChanged(nameof(IsDisabled));
            }
        }

        partial void OnIsDisabledChanged(bool value)
        {
            if (value)
            {
                _isEnabled = false;
                OnPropertyChanged(nameof(IsEnabled));
            }
        }
    }
}
