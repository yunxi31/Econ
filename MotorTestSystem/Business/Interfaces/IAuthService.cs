using MotorTestSystem.Models;

namespace MotorTestSystem.Services
{
    /// <summary>
    /// 认证与权限服务接口
    /// </summary>
    public interface IAuthService
    {
        /// <summary>当前登录用户（未登录时为 null）</summary>
        AppUser? CurrentUser { get; }

        /// <summary>当前用户是否已认证</summary>
        bool IsAuthenticated { get; }

        /// <summary>
        /// 登录认证
        /// </summary>
        /// <param name="account">账号</param>
        /// <param name="password">密码</param>
        /// <param name="errorMessage">错误消息</param>
        /// <returns>是否登录成功</returns>
        bool Login(string account, string password, out string errorMessage);

        /// <summary>登出</summary>
        void Logout();

        /// <summary>判断当前用户是否拥有指定权限</summary>
        bool HasPermission(AppPermission permission);

        /// <summary>判断当前用户是否拥有任一指定权限</summary>
        bool HasAnyPermission(params AppPermission[] permissions);

        /// <summary>判断当前用户是否拥有全部指定权限</summary>
        bool HasAllPermissions(params AppPermission[] permissions);

        /// <summary>当前用户角色</summary>
        AppRole CurrentRole { get; }
    }
}
