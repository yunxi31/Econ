using System;
using System.Linq;
using MotorTestSystem.Models;

namespace MotorTestSystem.Services
{
    /// <summary>
    /// 认证与权限服务实现
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly IUserService _userService;
        private AppUser? _currentUser;

        public AuthService(IUserService userService)
        {
            _userService = userService;
        }

        public AppUser? CurrentUser => _currentUser;

        public bool IsAuthenticated => _currentUser != null && _currentUser.Status == UserStatus.Active;

        public AppRole CurrentRole => _currentUser?.Role ?? AppRole.Operator;

        public bool Login(string account, string password, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(account))
            {
                errorMessage = "请输入用户名！";
                return false;
            }

            var user = _userService.GetByAccount(account);
            if (user == null)
            {
                errorMessage = "用户不存在！";
                return false;
            }

            if (user.Status == UserStatus.Disabled)
            {
                errorMessage = "该账号已被禁用，请联系管理员！";
                return false;
            }

            if (!_userService.ValidatePassword(account, password))
            {
                errorMessage = "密码错误！";
                return false;
            }

            // 登录成功
            _currentUser = user;
            _userService.UpdateLastLoginTime(user.Id);
            return true;
        }

        public void Logout()
        {
            _currentUser = null;
        }

        public bool HasPermission(AppPermission permission)
        {
            if (_currentUser == null) return false;
            return RolePermissions.HasPermission(_currentUser.Role, permission);
        }

        public bool HasAnyPermission(params AppPermission[] permissions)
        {
            if (_currentUser == null) return false;
            var rolePerms = RolePermissions.GetPermissions(_currentUser.Role);
            return permissions.Any(p => rolePerms.Contains(p));
        }

        public bool HasAllPermissions(params AppPermission[] permissions)
        {
            if (_currentUser == null) return false;
            var rolePerms = RolePermissions.GetPermissions(_currentUser.Role);
            return permissions.All(p => rolePerms.Contains(p));
        }
    }
}
