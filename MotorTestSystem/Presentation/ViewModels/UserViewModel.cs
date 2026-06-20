using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MotorTestSystem.Models;
using MotorTestSystem.Services;
using MotorTestSystem.Views;

namespace MotorTestSystem.ViewModels
{
    public partial class UserViewModel : ViewModelBase
    {
        private readonly IUserService _userService;
        private readonly IAuthService _authService;
        private readonly IDialogService _dialogService;

        /// <summary>
        /// 用户列表项（UI 展示用，映射自 AppUser）
        /// </summary>
        public class UserItem : ObservableObject
        {
            private string _id = string.Empty;
            public string Id
            {
                get => _id;
                set => SetProperty(ref _id, value);
            }

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

            private AppRole _roleEnum = AppRole.Operator;
            public AppRole RoleEnum
            {
                get => _roleEnum;
                set => SetProperty(ref _roleEnum, value);
            }

            private string _status = string.Empty;
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

        private List<UserItem> _allUsers = new();

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

        // ===== 右侧角色权限面板 =====

        /// <summary>角色权限展示列表</summary>
        public ObservableCollection<RolePermissionDisplay> RolePermissionDisplays { get; } = new();

        // ===== 权限检查 =====

        /// <summary>当前用户是否可以新增用户</summary>
        [ObservableProperty]
        private bool _canAddUser;

        /// <summary>当前用户是否可以编辑用户</summary>
        [ObservableProperty]
        private bool _canEditUser;

        /// <summary>当前用户是否可以重置密码</summary>
        [ObservableProperty]
        private bool _canResetPassword;

        private static BackendRuntime GetRuntime() => BackendRuntime.GetSharedAsync().GetAwaiter().GetResult();

        public UserViewModel() : this(GetRuntime())
        {
        }

        private UserViewModel(BackendRuntime runtime)
            : this(
                  runtime.UserService,
                  runtime.AuthService,
                  new MotorTestSystem.Presentation.Services.WpfDialogService())
        {
        }

        public UserViewModel(IUserService userService, IAuthService authService)
            : this(
                  userService,
                  authService,
                  new MotorTestSystem.Presentation.Services.WpfDialogService())
        {
        }

        public UserViewModel(
            IUserService userService,
            IAuthService authService,
            IDialogService dialogService)
        {
            _userService = userService;
            _authService = authService;
            _dialogService = dialogService;

            _ = LoadUsersAsync();
            LoadRolePermissions();
            RefreshPermissions();
        }

        // ===== 数据加载 =====

        private async Task LoadUsersAsync()
        {
            var users = await _userService.GetAllAsync();
            _allUsers = users.Select(MapToItem).ToList();
            FilterUsers();
        }

        private static UserItem MapToItem(AppUser user) => new()
        {
            Id = user.Id,
            Account = user.Account,
            Name = user.Name,
            Role = user.RoleDisplayName,
            RoleEnum = user.Role,
            Status = user.Status == UserStatus.Disabled ? "禁用" : (user.LastLoginTime.HasValue ? "在线" : "离线"),
            LastLoginTime = user.LastLoginTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-",
        };

        private void LoadRolePermissions()
        {
            RolePermissionDisplays.Clear();

            foreach (AppRole role in Enum.GetValues<AppRole>())
            {
                var permissions = Models.RolePermissions.GetPermissions(role);
                var display = new RolePermissionDisplay
                {
                    RoleName = role switch
                    {
                        AppRole.Admin => "管理员",
                        AppRole.Operator => "操作员",
                        AppRole.Maintainer => "维护员",
                        _ => role.ToString()
                    },
                    RoleEnum = role,
                };

                foreach (var perm in permissions)
                {
                    display.Permissions.Add(new PermissionTag
                    {
                        Name = Models.RolePermissions.GetPermissionDisplayName(perm),
                        IsHighlighted = Models.RolePermissions.IsHighlightPermission(role, perm),
                    });
                }

                RolePermissionDisplays.Add(display);
            }
        }

        private void RefreshPermissions()
        {
            CanAddUser = _authService.HasPermission(AppPermission.UserManagement);
            CanEditUser = _authService.HasPermission(AppPermission.UserManagement);
            CanResetPassword = _authService.HasPermission(AppPermission.UserManagement);
        }

        // ===== 搜索与过滤 =====

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

        // ===== 命令 =====

        [RelayCommand(CanExecute = nameof(CanAddUser))]
        private async Task AddUser()
        {
            var result = _dialogService.ShowUserEditDialog("新增用户", "", "", "", true);
            if (result != null)
            {
                var role = ParseRole(result.Role);
                var status = result.IsEnabled ? UserStatus.Active : UserStatus.Disabled;

                var error = await _userService.CreateAsync(
                    result.Account,
                    result.Name,
                    result.Password,
                    role,
                    status);

                if (error != null)
                {
                    await _dialogService.ShowMessageAsync(error, "创建失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                await LoadUsersAsync(); // 重新加载
            }
        }

        [RelayCommand(CanExecute = nameof(CanEditUser))]
        private async Task EditUser(UserItem user)
        {
            if (user == null) return;

            var result = _dialogService.ShowUserEditDialog(
                "编辑用户信息",
                user.Account,
                user.Name,
                user.Role,
                user.Status != "禁用");

            if (result != null)
            {
                var role = ParseRole(result.Role);
                var status = result.IsEnabled ? UserStatus.Active : UserStatus.Disabled;

                var error = await _userService.UpdateAsync(user.Id, result.Name, role, status);

                if (error != null)
                {
                    await _dialogService.ShowMessageAsync(error, "更新失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                await LoadUsersAsync(); // 重新加载
            }
        }

        [RelayCommand(CanExecute = nameof(CanResetPassword))]
        private async Task ResetPassword(UserItem user)
        {
            if (user == null) return;

            var result = await _dialogService.ShowMessageAsync(
                $"确定要重置用户 {user.Name} ({user.Account}) 的密码为初始密码吗？",
                "密码重置确认",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result != System.Windows.MessageBoxResult.Yes) return;

            // 重置为默认密码 123456
            var error = await _userService.ResetPasswordAsync(user.Id, "123456");
            if (error != null)
            {
                await _dialogService.ShowMessageAsync(error, "重置失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            await _dialogService.ShowMessageAsync(
                $"用户 {user.Name} 的密码已重置为：123456",
                "密码重置成功",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        private static AppRole ParseRole(string roleName) => roleName switch
        {
            "管理员" => AppRole.Admin,
            "操作员" => AppRole.Operator,
            "维护员" => AppRole.Maintainer,
            _ => AppRole.Operator
        };
    }

    // ===== 右侧角色权限面板的数据模型 =====

    /// <summary>
    /// 角色权限展示项
    /// </summary>
    public class RolePermissionDisplay : ObservableObject
    {
        private string _roleName = string.Empty;
        public string RoleName
        {
            get => _roleName;
            set => SetProperty(ref _roleName, value);
        }

        private AppRole _roleEnum = AppRole.Operator;
        public AppRole RoleEnum
        {
            get => _roleEnum;
            set => SetProperty(ref _roleEnum, value);
        }

        public ObservableCollection<PermissionTag> Permissions { get; } = new();
    }

    /// <summary>
    /// 权限标签项
    /// </summary>
    public class PermissionTag : ObservableObject
    {
        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private bool _isHighlighted;
        public bool IsHighlighted
        {
            get => _isHighlighted;
            set => SetProperty(ref _isHighlighted, value);
        }
    }
}
