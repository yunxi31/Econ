namespace MotorTestSystem.Models
{
    /// <summary>
    /// 登录认证结果 — 替代 AuthService.Login 的 out string 参数模式。
    /// 注意：名称使用 AuthLoginResult 而非 LoginResult，以避免和
    /// HikvisionSdkService.LoginResult 产生命名冲突。
    /// </summary>
    public sealed class AuthLoginResult
    {
        /// <summary>是否登录成功</summary>
        public bool Success { get; init; }

        /// <summary>错误消息（登录失败时）</summary>
        public string? ErrorMessage { get; init; }

        /// <summary>登录成功的用户</summary>
        public AppUser? User { get; init; }

        public static AuthLoginResult Ok(AppUser user) => new() { Success = true, User = user };
        public static AuthLoginResult Fail(string error) => new() { Success = false, ErrorMessage = error };
    }
}
