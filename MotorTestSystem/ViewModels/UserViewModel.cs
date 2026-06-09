using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MotorTestSystem.ViewModels
{
    public partial class UserViewModel : ViewModelBase
    {
        public class UserItem : ObservableObject
        {
            private string _account = string.Empty;
            public string Account
            {
                get => _account;
                set => SetProperty(ref _account, value);
            }

            private string _name = string.Empty;
            public string Name
            {
                get => _name;
                set => SetProperty(ref _name, value);
            }

            private string _role = string.Empty;
            public string Role
            {
                get => _role;
                set => SetProperty(ref _role, value);
            }

            private string _status = string.Empty; // "在线", "离线", "禁用"
            public string Status
            {
                get => _status;
                set => SetProperty(ref _status, value);
            }

            private string _lastLoginTime = string.Empty;
            public string LastLoginTime
            {
                get => _lastLoginTime;
                set => SetProperty(ref _lastLoginTime, value);
            }
        }

        private ObservableCollection<UserItem> _allUsers = new();
        
        [ObservableProperty]
        private ObservableCollection<UserItem> _users = new();

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _selectedRoleFilter = "所有角色";

        public ObservableCollection<string> RoleFilters { get; } = new()
        {
            "所有角色",
            "管理员",
            "操作员",
            "维护员"
        };

        public UserViewModel()
        {
            LoadMockUsers();
            FilterUsers();
        }

        private void LoadMockUsers()
        {
            _allUsers = new ObservableCollection<UserItem>
            {
                new UserItem { Account = "OP-10024", Name = "张伟 (Zhang Wei)", Role = "管理员", Status = "在线", LastLoginTime = "2023-10-27 08:15:32" },
                new UserItem { Account = "OP-10088", Name = "李娜 (Li Na)", Role = "操作员", Status = "在线", LastLoginTime = "2023-10-27 14:22:10" },
                new UserItem { Account = "MT-20011", Name = "王强 (Wang Qiang)", Role = "维护员", Status = "离线", LastLoginTime = "2023-10-26 18:45:00" },
                new UserItem { Account = "OP-10092", Name = "赵雷 (Zhao Lei)", Role = "操作员", Status = "禁用", LastLoginTime = "2023-10-15 09:12:44" },
                new UserItem { Account = "OP-10095", Name = "陈静 (Chen Jing)", Role = "操作员", Status = "在线", LastLoginTime = "2023-10-27 15:10:22" },
                new UserItem { Account = "MT-20015", Name = "刘洋 (Liu Yang)", Role = "维护员", Status = "离线", LastLoginTime = "2023-10-26 09:30:15" },
                new UserItem { Account = "OP-10102", Name = "周梅 (Zhou Mei)", Role = "操作员", Status = "在线", LastLoginTime = "2023-10-27 16:45:00" }
            };
        }

        partial void OnSearchTextChanged(string value) => FilterUsers();
        partial void OnSelectedRoleFilterChanged(string value) => FilterUsers();

        private void FilterUsers()
        {
            var filtered = _allUsers.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                filtered = filtered.Where(u => 
                    u.Account.Contains(SearchText, StringComparison.OrdinalIgnoreCase) || 
                    u.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            if (SelectedRoleFilter != "所有角色")
            {
                filtered = filtered.Where(u => u.Role == SelectedRoleFilter);
            }

            Users = new ObservableCollection<UserItem>(filtered);
        }

        [RelayCommand]
        private void AddUser()
        {
            var dialogViewModel = new UserEditDialogViewModel
            {
                Title = "新增用户",
                IsEnabled = true
            };

            var win = new UserEditWindow
            {
                DataContext = dialogViewModel,
                Owner = System.Windows.Application.Current.MainWindow
            };

            if (win.ShowDialog() == true)
            {
                var newUser = new UserItem
                {
                    Account = dialogViewModel.Account,
                    Name = dialogViewModel.Name,
                    Role = dialogViewModel.SelectedRole,
                    Status = dialogViewModel.IsEnabled ? "在线" : "禁用",
                    LastLoginTime = "-"
                };
                _allUsers.Insert(0, newUser);
                FilterUsers();
            }
        }

        [RelayCommand]
        private void EditUser(UserItem user)
        {
            if (user == null) return;

            var dialogViewModel = new UserEditDialogViewModel
            {
                Title = "编辑用户信息",
                Account = user.Account,
                Name = user.Name,
                SelectedRole = user.Role,
                IsEnabled = user.Status != "禁用"
            };

            var win = new UserEditWindow
            {
                DataContext = dialogViewModel,
                Owner = System.Windows.Application.Current.MainWindow
            };

            if (win.ShowDialog() == true)
            {
                user.Account = dialogViewModel.Account;
                user.Name = dialogViewModel.Name;
                user.Role = dialogViewModel.SelectedRole;
                user.Status = dialogViewModel.IsEnabled ? "在线" : "禁用";
                FilterUsers();
            }
        }

        [RelayCommand]
        private void ResetPassword(UserItem user)
        {
            if (user == null) return;
            System.Windows.MessageBox.Show($"已重置用户 {user.Name} ({user.Account}) 的密码为初始密码。", "密码重置", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
    }
}
