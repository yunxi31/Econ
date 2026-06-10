using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MotorTestSystem.Models;

namespace MotorTestSystem.Services
{
    /// <summary>
    /// 内存用户服务实现（开发/演示用）
    /// </summary>
    public class InMemoryUserService : IUserService
    {
        private readonly List<AppUser> _users = new();
        private int _nextId = 1;

        public InMemoryUserService()
        {
            SeedDefaultUsers();
        }

        // ===== 查询 =====

        public IReadOnlyList<AppUser> GetAll() => _users.AsReadOnly();

        public AppUser? GetById(string id) => _users.FirstOrDefault(u => u.Id == id);

        public AppUser? GetByAccount(string account)
            => _users.FirstOrDefault(u => string.Equals(u.Account, account, StringComparison.OrdinalIgnoreCase));

        // ===== 增删改 =====

        public string? Create(string account, string name, string password, AppRole role, UserStatus status = UserStatus.Active)
        {
            if (string.IsNullOrWhiteSpace(account))
                return "账号不能为空";

            if (string.IsNullOrWhiteSpace(name))
                return "姓名不能为空";

            if (string.IsNullOrWhiteSpace(password))
                return "密码不能为空";

            if (_users.Any(u => string.Equals(u.Account, account, StringComparison.OrdinalIgnoreCase)))
                return $"账号 {account} 已存在";

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
            return null; // 成功
        }

        public string? Update(string userId, string name, AppRole role, UserStatus status)
        {
            var user = GetById(userId);
            if (user == null)
                return "用户不存在";

            if (string.IsNullOrWhiteSpace(name))
                return "姓名不能为空";

            user.Name = name;
            user.Role = role;
            user.Status = status;
            user.UpdatedAt = DateTime.Now;
            return null;
        }

        public string? Delete(string userId)
        {
            var user = GetById(userId);
            if (user == null)
                return "用户不存在";

            _users.Remove(user);
            return null;
        }

        // ===== 密码管理 =====

        public string? ResetPassword(string userId, string newPassword)
        {
            var user = GetById(userId);
            if (user == null)
                return "用户不存在";

            if (string.IsNullOrWhiteSpace(newPassword))
                return "新密码不能为空";

            user.PasswordHash = HashPassword(newPassword);
            user.UpdatedAt = DateTime.Now;
            return null;
        }

        public string? ChangePassword(string userId, string oldPassword, string newPassword)
        {
            var user = GetById(userId);
            if (user == null)
                return "用户不存在";

            if (user.PasswordHash != HashPassword(oldPassword))
                return "旧密码不正确";

            if (string.IsNullOrWhiteSpace(newPassword))
                return "新密码不能为空";

            user.PasswordHash = HashPassword(newPassword);
            user.UpdatedAt = DateTime.Now;
            return null;
        }

        public bool ValidatePassword(string account, string password)
        {
            var user = GetByAccount(account);
            if (user == null) return false;
            if (user.Status == UserStatus.Disabled) return false;
            return user.PasswordHash == HashPassword(password);
        }

        public void UpdateLastLoginTime(string userId)
        {
            var user = GetById(userId);
            if (user != null)
            {
                user.LastLoginTime = DateTime.Now;
            }
        }

        // ===== 密码哈希 =====

        internal static string HashPassword(string password)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(bytes);
        }

        // ===== 种子数据 =====

        private void SeedDefaultUsers()
        {
            var now = DateTime.Now;

            // ──────────── 管理员 (3人) ────────────
            Create("admin", "系统管理员", "admin123", AppRole.Admin);
            SetLoginAndCreated("admin", now.AddHours(-1), now.AddMonths(-6));

            Create("ad_liwei", "李威", "admin123", AppRole.Admin);
            SetLoginAndCreated("ad_liwei", now.AddDays(-3), now.AddMonths(-3));

            Create("ad_sunyan", "孙燕", "admin123", AppRole.Admin, UserStatus.Disabled);
            SetLoginAndCreated("ad_sunyan", null, now.AddMonths(-8));

            // ──────────── 操作员 (8人) ────────────
            Create("operator", "默认操作员", "123", AppRole.Operator);
            SetLoginAndCreated("operator", now.AddMinutes(-10), now.AddMonths(-5));

            Create("op_zhangwei", "张伟", "123", AppRole.Operator);
            SetLoginAndCreated("op_zhangwei", now.AddHours(-3), now.AddMonths(-4));

            Create("op_lina", "李娜", "123", AppRole.Operator);
            SetLoginAndCreated("op_lina", now.AddDays(-1), now.AddMonths(-3));

            Create("op_zhaolei", "赵雷", "123", AppRole.Operator, UserStatus.Disabled);
            SetLoginAndCreated("op_zhaolei", now.AddDays(-30), now.AddMonths(-2));

            Create("op_chenjing", "陈静", "123", AppRole.Operator);
            SetLoginAndCreated("op_chenjing", now.AddMinutes(-45), now.AddMonths(-1));

            Create("op_zhoumei", "周梅", "123", AppRole.Operator);
            SetLoginAndCreated("op_zhoumei", now.AddHours(-8), now.AddMonths(-2));

            Create("op_wugang", "吴刚", "123", AppRole.Operator);
            SetLoginAndCreated("op_wugang", null, now.AddDays(-14));  // 从未登录

            Create("op_huangli", "黄丽", "123", AppRole.Operator, UserStatus.Disabled);
            SetLoginAndCreated("op_huangli", now.AddDays(-7), now.AddDays(-14));

            // ──────────── 维护员 (5人) ────────────
            Create("maintainer", "默认维护员", "maint123", AppRole.Maintainer);
            SetLoginAndCreated("maintainer", now.AddHours(-2), now.AddMonths(-6));

            Create("mt_wangqiang", "王强", "maint123", AppRole.Maintainer);
            SetLoginAndCreated("mt_wangqiang", now.AddHours(-5), now.AddMonths(-4));

            Create("mt_liuyang", "刘洋", "maint123", AppRole.Maintainer);
            SetLoginAndCreated("mt_liuyang", now.AddDays(-2), now.AddMonths(-3));

            Create("mt_zhaomin", "赵敏", "maint123", AppRole.Maintainer);
            SetLoginAndCreated("mt_zhaomin", now.AddMinutes(-20), now.AddMonths(-1));

            Create("mt_chenhao", "陈昊", "maint123", AppRole.Maintainer, UserStatus.Disabled);
            SetLoginAndCreated("mt_chenhao", now.AddDays(-15), now.AddMonths(-2));
        }

        /// <summary>
        /// 辅助方法：设置种子用户的最后登录时间和创建时间
        /// </summary>
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
