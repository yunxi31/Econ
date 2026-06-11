using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MotorTestSystem.Models;
using MotorTestSystem.Models.Entities;

namespace MotorTestSystem.Services
{
    /// <summary>
    /// 基于 SqlSugar + SQLite 的用户服务实现
    /// </summary>
    public class SqlSugarUserService : IUserService
    {
        private readonly SqlSugarDbContext _ctx;
        private int _nextIdSeq;

        public SqlSugarUserService(SqlSugarDbContext ctx)
        {
            _ctx = ctx;
            // 从数据库中获取最大 ID 序号，确保后续 ID 不重复
            InitializeIdSequence();
        }

        private void InitializeIdSequence()
        {
            var maxId = _ctx.Db.Queryable<UserEntity>()
                .OrderByDescending(u => u.Id)
                .Select(u => u.Id)
                .First();

            if (!string.IsNullOrEmpty(maxId) && maxId.StartsWith("U") && maxId.Length > 1)
            {
                if (int.TryParse(maxId[1..], out int seq))
                {
                    _nextIdSeq = seq;
                }
            }
        }

        // ===== 查询 =====

        public IReadOnlyList<AppUser> GetAll()
        {
            var entities = _ctx.Db.Queryable<UserEntity>()
                .OrderBy(u => u.Id)
                .ToList();

            return entities.Select(ToModel).ToList();
        }

        public AppUser? GetById(string id)
        {
            var entity = _ctx.Db.Queryable<UserEntity>()
                .First(u => u.Id == id);
            return entity != null ? ToModel(entity) : null;
        }

        public AppUser? GetByAccount(string account)
        {
            var entity = _ctx.Db.Queryable<UserEntity>()
                .First(u => u.Account == account);
            return entity != null ? ToModel(entity) : null;
        }

        // ===== 增删改 =====

        public string? Create(string account, string name, string password, AppRole role, UserStatus status = UserStatus.Active)
        {
            if (string.IsNullOrWhiteSpace(account))
                return "账号不能为空";

            if (string.IsNullOrWhiteSpace(name))
                return "姓名不能为空";

            if (string.IsNullOrWhiteSpace(password))
                return "密码不能为空";

            // 检查账号唯一性
            if (_ctx.Db.Queryable<UserEntity>().Any(u => u.Account == account))
                return $"账号 {account} 已存在";

            var entity = new UserEntity
            {
                Id = $"U{++_nextIdSeq:D5}",
                Account = account,
                Name = name,
                PasswordHash = HashPassword(password),
                Role = (int)role,
                Status = (int)status,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
            };

            _ctx.Db.Insertable(entity).ExecuteCommand();
            return null; // 成功
        }

        public string? Update(string userId, string name, AppRole role, UserStatus status)
        {
            var entity = _ctx.Db.Queryable<UserEntity>().First(u => u.Id == userId);
            if (entity == null)
                return "用户不存在";

            if (string.IsNullOrWhiteSpace(name))
                return "姓名不能为空";

            entity.Name = name;
            entity.Role = (int)role;
            entity.Status = (int)status;
            entity.UpdatedAt = DateTime.Now;

            _ctx.Db.Updateable(entity).ExecuteCommand();
            return null;
        }

        public string? Delete(string userId)
        {
            var entity = _ctx.Db.Queryable<UserEntity>().First(u => u.Id == userId);
            if (entity == null)
                return "用户不存在";

            _ctx.Db.Deleteable(entity).ExecuteCommand();
            return null;
        }

        // ===== 密码管理 =====

        public string? ResetPassword(string userId, string newPassword)
        {
            var entity = _ctx.Db.Queryable<UserEntity>().First(u => u.Id == userId);
            if (entity == null)
                return "用户不存在";

            if (string.IsNullOrWhiteSpace(newPassword))
                return "新密码不能为空";

            entity.PasswordHash = HashPassword(newPassword);
            entity.UpdatedAt = DateTime.Now;
            _ctx.Db.Updateable(entity).ExecuteCommand();
            return null;
        }

        public string? ChangePassword(string userId, string oldPassword, string newPassword)
        {
            var entity = _ctx.Db.Queryable<UserEntity>().First(u => u.Id == userId);
            if (entity == null)
                return "用户不存在";

            if (entity.PasswordHash != HashPassword(oldPassword))
                return "旧密码不正确";

            if (string.IsNullOrWhiteSpace(newPassword))
                return "新密码不能为空";

            entity.PasswordHash = HashPassword(newPassword);
            entity.UpdatedAt = DateTime.Now;
            _ctx.Db.Updateable(entity).ExecuteCommand();
            return null;
        }

        public bool ValidatePassword(string account, string password)
        {
            var entity = _ctx.Db.Queryable<UserEntity>()
                .First(u => u.Account == account);

            if (entity == null) return false;
            if ((UserStatus)entity.Status == UserStatus.Disabled) return false;
            return entity.PasswordHash == HashPassword(password);
        }

        public void UpdateLastLoginTime(string userId)
        {
            var entity = _ctx.Db.Queryable<UserEntity>().First(u => u.Id == userId);
            if (entity != null)
            {
                entity.LastLoginTime = DateTime.Now;
                _ctx.Db.Updateable(entity)
                    .UpdateColumns(u => new { u.LastLoginTime })
                    .ExecuteCommand();
            }
        }

        // ===== 密码哈希 =====

        internal static string HashPassword(string password)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(bytes);
        }

        // ===== 实体 → 领域模型转换 =====

        private static AppUser ToModel(UserEntity entity)
        {
            return new AppUser
            {
                Id = entity.Id,
                Account = entity.Account,
                Name = entity.Name,
                PasswordHash = entity.PasswordHash,
                Role = (AppRole)entity.Role,
                Status = (UserStatus)entity.Status,
                LastLoginTime = entity.LastLoginTime,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }
    }
}
