using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MotorTestSystem.Models;
using MotorTestSystem.Models.Entities;

namespace MotorTestSystem.Services
{
    /// <summary>
    /// 基于 SqlSugar + SQLite 的用户服务实现（全异步）
    /// </summary>
    public class SqlSugarUserService : IUserService
    {
        private readonly SqlSugarDbContext _ctx;
        private int _nextIdSeq;

        public SqlSugarUserService(SqlSugarDbContext ctx)
        {
            _ctx = ctx;
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

        public async Task<IReadOnlyList<AppUser>> GetAllAsync()
        {
            var entities = await _ctx.Db.Queryable<UserEntity>()
                .OrderBy(u => u.Id)
                .ToListAsync();

            return entities.Select(ToModel).ToList();
        }

        public async Task<AppUser?> GetByIdAsync(string id)
        {
            var entity = await _ctx.Db.Queryable<UserEntity>()
                .FirstAsync(u => u.Id == id);
            return entity != null ? ToModel(entity) : null;
        }

        public async Task<AppUser?> GetByAccountAsync(string account)
        {
            var entity = await _ctx.Db.Queryable<UserEntity>()
                .FirstAsync(u => u.Account == account);
            return entity != null ? ToModel(entity) : null;
        }

        // ===== 增删改 =====

        public async Task<string?> CreateAsync(string account, string name, string password, AppRole role, UserStatus status = UserStatus.Active)
        {
            if (string.IsNullOrWhiteSpace(account))
                return "账号不能为空";

            if (string.IsNullOrWhiteSpace(name))
                return "姓名不能为空";

            if (string.IsNullOrWhiteSpace(password))
                return "密码不能为空";

            if (await _ctx.Db.Queryable<UserEntity>().AnyAsync(u => u.Account == account))
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

            await _ctx.Db.Insertable(entity).ExecuteCommandAsync();
            return null;
        }

        public async Task<string?> UpdateAsync(string userId, string name, AppRole role, UserStatus status)
        {
            var entity = await _ctx.Db.Queryable<UserEntity>().FirstAsync(u => u.Id == userId);
            if (entity == null)
                return "用户不存在";

            if (string.IsNullOrWhiteSpace(name))
                return "姓名不能为空";

            entity.Name = name;
            entity.Role = (int)role;
            entity.Status = (int)status;
            entity.UpdatedAt = DateTime.Now;

            await _ctx.Db.Updateable(entity).ExecuteCommandAsync();
            return null;
        }

        public async Task<string?> DeleteAsync(string userId)
        {
            var entity = await _ctx.Db.Queryable<UserEntity>().FirstAsync(u => u.Id == userId);
            if (entity == null)
                return "用户不存在";

            await _ctx.Db.Deleteable(entity).ExecuteCommandAsync();
            return null;
        }

        // ===== 密码管理 =====

        public async Task<string?> ResetPasswordAsync(string userId, string newPassword)
        {
            var entity = await _ctx.Db.Queryable<UserEntity>().FirstAsync(u => u.Id == userId);
            if (entity == null)
                return "用户不存在";

            if (string.IsNullOrWhiteSpace(newPassword))
                return "新密码不能为空";

            entity.PasswordHash = HashPassword(newPassword);
            entity.UpdatedAt = DateTime.Now;
            await _ctx.Db.Updateable(entity).ExecuteCommandAsync();
            return null;
        }

        public async Task<string?> ChangePasswordAsync(string userId, string oldPassword, string newPassword)
        {
            var entity = await _ctx.Db.Queryable<UserEntity>().FirstAsync(u => u.Id == userId);
            if (entity == null)
                return "用户不存在";

            if (entity.PasswordHash != HashPassword(oldPassword))
                return "旧密码不正确";

            if (string.IsNullOrWhiteSpace(newPassword))
                return "新密码不能为空";

            entity.PasswordHash = HashPassword(newPassword);
            entity.UpdatedAt = DateTime.Now;
            await _ctx.Db.Updateable(entity).ExecuteCommandAsync();
            return null;
        }

        public async Task<bool> ValidatePasswordAsync(string account, string password)
        {
            var entity = await _ctx.Db.Queryable<UserEntity>()
                .FirstAsync(u => u.Account == account);

            if (entity == null) return false;
            if ((UserStatus)entity.Status == UserStatus.Disabled) return false;
            return entity.PasswordHash == HashPassword(password);
        }

        public async Task UpdateLastLoginTimeAsync(string userId)
        {
            var entity = await _ctx.Db.Queryable<UserEntity>().FirstAsync(u => u.Id == userId);
            if (entity != null)
            {
                entity.LastLoginTime = DateTime.Now;
                await _ctx.Db.Updateable(entity)
                    .UpdateColumns(u => new { u.LastLoginTime })
                    .ExecuteCommandAsync();
            }
        }

        internal static string HashPassword(string password)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(bytes);
        }

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
