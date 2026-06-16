using System.Collections.Generic;
using System.Threading.Tasks;
using MotorTestSystem.Models;

namespace MotorTestSystem.Services
{
    /// <summary>
    /// 用户管理服务接口（全异步）
    /// </summary>
    public interface IUserService
    {
        /// <summary>获取所有用户</summary>
        Task<IReadOnlyList<AppUser>> GetAllAsync();

        /// <summary>根据 ID 获取用户</summary>
        Task<AppUser?> GetByIdAsync(string id);

        /// <summary>根据账号获取用户</summary>
        Task<AppUser?> GetByAccountAsync(string account);

        /// <summary>创建新用户（返回 null 表示成功，否则返回错误消息）</summary>
        Task<string?> CreateAsync(string account, string name, string password, AppRole role, UserStatus status = UserStatus.Active);

        /// <summary>更新用户信息（返回 null 表示成功，否则返回错误消息）</summary>
        Task<string?> UpdateAsync(string userId, string name, AppRole role, UserStatus status);

        /// <summary>删除用户（返回 null 表示成功，否则返回错误消息）</summary>
        Task<string?> DeleteAsync(string userId);

        /// <summary>重置用户密码（返回 null 表示成功，否则返回错误消息）</summary>
        Task<string?> ResetPasswordAsync(string userId, string newPassword);

        /// <summary>修改密码（返回 null 表示成功，否则返回错误消息）</summary>
        Task<string?> ChangePasswordAsync(string userId, string oldPassword, string newPassword);

        /// <summary>验证账号密码是否正确</summary>
        Task<bool> ValidatePasswordAsync(string account, string password);

        /// <summary>更新最后登录时间</summary>
        Task UpdateLastLoginTimeAsync(string userId);
    }
}
