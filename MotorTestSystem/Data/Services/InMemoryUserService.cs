using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MotorTestSystem.Models;

namespace MotorTestSystem.Services
{
    /// <summary>
    /// 内存用户服务实现（开发/演示用，全异步包装）
    /// </summary>
    public class InMemoryUserService : IUserService
    {
        private readonly List<AppUser> _users = new();
        private int _nextId = 1;

        public InMemoryUserService()
        {
            SeedDefaultUsers();
        }

        public Task<IReadOnlyList<AppUser>> GetAllAsync()
            => Task.FromResult<IReadOnlyList<AppUser>>(_users.AsReadOnly());

        public Task<AppUser?> GetByIdAsync(string id)
            => Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

        public Task<AppUser?> GetByAccountAsync(string account)
            => Task.FromResult(_users.FirstOrDefault(u =>
                string.Equals(u.Account, account, StringComparison.OrdinalIgnoreCase)));

        public Task<string?> CreateAsync(string account, string name, string password, AppRole role, UserStatus status = UserStatus.Active)
        {
            if (string.IsNullOrWhiteSpace(account))
                return Task.FromResult<string?>("账号不能为空");

            if (string.IsNullOrWhiteSpace(name))
                return Task.FromResult<string?>("姓名不能为空");

            if (string.IsNullOrWhiteSpace(password))
                return Task.FromResult<string?>("密码不能为空");

            if (_users.Any(u => string.Equals(u.Account, account, StringComparison.OrdinalIgnoreCase)))
                return Task.FromResult<string?>($"账号 {account} 已存在");

            var user = new AppUser
            {
                Id = $"U{_nextId++:D5}",
                Account = account,
                Name = name,
                PasswordHash = HashPassword(password),
                Role = role,
                Status = status,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
            };

            _users.Add(user);
            return Task.FromResult<string?>(null);
        }

        public Task<string?> UpdateAsync(string userId, string name, AppRole role, UserStatus status)
        {
            var user = _users.FirstOrDefault(u => u.Id == userId);
            if (user == null)
                return Task.FromResult<string?>("用户不存在");

            if (string.IsNullOrWhiteSpace(name))
                return Task.FromResult<string?>("姓名不能为空");

            user.Name = name;
            user.Role = role;
            user.Status = status;
            user.UpdatedAt = DateTime.Now;
            return Task.FromResult<string?>(null);
        }

        public Task<string?> DeleteAsync(string userId)
        {
            var user = _users.FirstOrDefault(u => u.Id == userId);
            if (user == null)
                return Task.FromResult<string?>("用户不存在");

            _users.Remove(user);
            return Task.FromResult<string?>(null);
        }

        public Task<string?> ResetPasswordAsync(string userId, string newPassword)
        {
            var user = _users.FirstOrDefault(u => u.Id == userId);
            if (user == null)
                return Task.FromResult<string?>("用户不存在");

            if (string.IsNullOrWhiteSpace(newPassword))
                return Task.FromResult<string?>("新密码不能为空");

            user.PasswordHash = HashPassword(newPassword);
            user.UpdatedAt = DateTime.Now;
            return Task.FromResult<string?>(null);
        }

        public Task<string?> ChangePasswordAsync(string userId, string oldPassword, string newPassword)
        {
            var user = _users.FirstOrDefault(u => u.Id == userId);
            if (user == null)
                return Task.FromResult<string?>("用户不存在");

            if (user.PasswordHash != HashPassword(oldPassword))
                return Task.FromResult<string?>("旧密码不正确");

            if (string.IsNullOrWhiteSpace(newPassword))
                return Task.FromResult<string?>("新密码不能为空");

            user.PasswordHash = HashPassword(newPassword);
            user.UpdatedAt = DateTime.Now;
            return Task.FromResult<string?>(null);
        }

        public Task<bool> ValidatePasswordAsync(string account, string password)
        {
            var user = _users.FirstOrDefault(u =>
                string.Equals(u.Account, account, StringComparison.OrdinalIgnoreCase));
            if (user == null) return Task.FromResult(false);
            if (user.Status == UserStatus.Disabled) return Task.FromResult(false);
            return Task.FromResult(user.PasswordHash == HashPassword(password));
        }

        public Task UpdateLastLoginTimeAsync(string userId)
        {
            var user = _users.FirstOrDefault(u => u.Id == userId);
            if (user != null)
            {
                user.LastLoginTime = DateTime.Now;
            }
            return Task.CompletedTask;
        }

        internal static string HashPassword(string password)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(bytes);
        }

        private void SeedDefaultUsers()
        {
            var now = DateTime.Now;

            CreateSync("admin", "系统管理员", "admin123", AppRole.Admin);
            SetLoginAndCreated("admin", now.AddHours(-1), now.AddMonths(-6));

            CreateSync("ad_liwei", "李威", "admin123", AppRole.Admin);
            SetLoginAndCreated("ad_liwei", now.AddDays(-3), now.AddMonths(-3));

            CreateSync("ad_sunyan", "孙燕", "admin123", AppRole.Admin, UserStatus.Disabled);
            SetLoginAndCreated("ad_sunyan", null, now.AddMonths(-8));

            CreateSync("operator", "默认操作员", "123", AppRole.Operator);
            SetLoginAndCreated("operator", now.AddMinutes(-10), now.AddMonths(-5));

            CreateSync("op_zhangwei", "张伟", "123", AppRole.Operator);
            SetLoginAndCreated("op_zhangwei", now.AddHours(-3), now.AddMonths(-4));

            CreateSync("op_lina", "李娜", "123", AppRole.Operator);
            SetLoginAndCreated("op_lina", now.AddDays(-1), now.AddMonths(-3));

            CreateSync("op_zhaolei", "赵雷", "123", AppRole.Operator, UserStatus.Disabled);
            SetLoginAndCreated("op_zhaolei", now.AddDays(-30), now.AddMonths(-2));

            CreateSync("op_chenjing", "陈静", "123", AppRole.Operator);
            SetLoginAndCreated("op_chenjing", now.AddMinutes(-45), now.AddMonths(-1));

            CreateSync("op_zhoumei", "周梅", "123", AppRole.Operator);
            SetLoginAndCreated("op_zhoumei", now.AddHours(-8), now.AddMonths(-2));

            CreateSync("op_wugang", "吴刚", "123", AppRole.Operator);
            SetLoginAndCreated("op_wugang", null, now.AddDays(-14));

            CreateSync("op_huangli", "黄丽", "123", AppRole.Operator, UserStatus.Disabled);
            SetLoginAndCreated("op_huangli", now.AddDays(-7), now.AddDays(-14));

            CreateSync("maintainer", "默认维护员", "maint123", AppRole.Maintainer);
            SetLoginAndCreated("maintainer", now.AddHours(-2), now.AddMonths(-6));

            CreateSync("mt_wangqiang", "王强", "maint123", AppRole.Maintainer);
            SetLoginAndCreated("mt_wangqiang", now.AddHours(-5), now.AddMonths(-4));

            CreateSync("mt_liuyang", "刘洋", "maint123", AppRole.Maintainer);
            SetLoginAndCreated("mt_liuyang", now.AddDays(-2), now.AddMonths(-3));

            CreateSync("mt_zhaomin", "赵敏", "maint123", AppRole.Maintainer);
            SetLoginAndCreated("mt_zhaomin", now.AddMinutes(-20), now.AddMonths(-1));

            CreateSync("mt_chenhao", "陈昊", "maint123", AppRole.Maintainer, UserStatus.Disabled);
            SetLoginAndCreated("mt_chenhao", now.AddDays(-15), now.AddMonths(-2));
        }

        private void CreateSync(string account, string name, string password, AppRole role, UserStatus status = UserStatus.Active)
        {
            var user = new AppUser
            {
                Id = $"U{_nextId++:D5}",
                Account = account,
                Name = name,
                PasswordHash = HashPassword(password),
                Role = role,
                Status = status,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
            };
            _users.Add(user);
        }

        private void SetLoginAndCreated(string account, DateTime? lastLogin, DateTime createdAt)
        {
            var user = _users.FirstOrDefault(u =>
                string.Equals(u.Account, account, StringComparison.OrdinalIgnoreCase));
            if (user == null) return;

            user.LastLoginTime = lastLogin;
            user.CreatedAt = createdAt;
            user.UpdatedAt = createdAt;
        }
    }
}
