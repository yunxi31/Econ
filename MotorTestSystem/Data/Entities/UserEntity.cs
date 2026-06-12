using SqlSugar;
using System;

namespace MotorTestSystem.Models.Entities
{
    /// <summary>
    /// 用户实体 — 对应数据库表
    /// </summary>
    [SugarTable("Users")]
    public class UserEntity
    {
        /// <summary>用户ID（如 U00001）</summary>
        [SugarColumn(IsPrimaryKey = true, Length = 20)]
        public string Id { get; set; } = string.Empty;

        /// <summary>登录账号</summary>
        [SugarColumn(Length = 50, IsNullable = false)]
        public string Account { get; set; } = string.Empty;

        /// <summary>用户姓名</summary>
        [SugarColumn(Length = 50, IsNullable = false)]
        public string Name { get; set; } = string.Empty;

        /// <summary>密码哈希（SHA256）</summary>
        [SugarColumn(Length = 128, IsNullable = false)]
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>角色 0=Admin 1=Operator 2=Maintainer</summary>
        [SugarColumn(IsNullable = false)]
        public int Role { get; set; } = 1;

        /// <summary>状态 0=Active 1=Disabled</summary>
        [SugarColumn(IsNullable = false)]
        public int Status { get; set; } = 0;

        /// <summary>最后登录时间</summary>
        [SugarColumn(IsNullable = true)]
        public DateTime? LastLoginTime { get; set; }

        /// <summary>创建时间</summary>
        [SugarColumn(IsNullable = false)]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>更新时间</summary>
        [SugarColumn(IsNullable = false)]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
