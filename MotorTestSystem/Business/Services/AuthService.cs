using System;
using System.Linq;
using System.Threading.Tasks;
using MotorTestSystem.Models;

namespace MotorTestSystem.Services
{
    /// <summary>
    /// 认证与权限服务实现（异步 Login）
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

        public async Task<AuthLoginResult> LoginAsync(string account, string password)
        {
            if (string.IsNullOrWhiteSpace(account))
            {
                return AuthLoginResult.Fail("请输入用户名！");
            }

            var user = await _userService.GetByAccountAsync(account);
            if (user == null)
            {
                return AuthLoginResult.Fail("用户不存在！");
            }

            if (user.Status == UserStatus.Disabled)
            {
                return AuthLoginResult.Fail("该账号已被禁用，请联系管理员！");
            }

            if (!await _userService.ValidatePasswordAsync(account, password))
            {
                return AuthLoginResult.Fail("密码错误！");
            }

            // 登录成功
            _currentUser = user;
            await _userService.UpdateLastLoginTimeAsync(user.Id);
            return AuthLoginResult.Ok(user);
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
