using SqlSugar;
using System;

namespace MotorTestSystem.Models.Entities
{
    /// <summary>
    /// 电机测试记录实体 — 对应数据库表
    /// </summary>
    [SugarTable("MotorTestRecords")]
    public class MotorTestRecordEntity
    {
        /// <summary>自增主键</summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        /// <summary>电机条码（业务唯一键）</summary>
        [SugarColumn(Length = 50, IsNullable = false)]
        public string Barcode { get; set; } = string.Empty;

        /// <summary>测试时间</summary>
        [SugarColumn(IsNullable = false)]
        public DateTime TestTime { get; set; }

        /// <summary>最终判定结果 OK/NG</summary>
        [SugarColumn(Length = 10, IsNullable = false)]
        public string FinalResult { get; set; } = "NG";

        // ---- 空载阶段 ----
        [SugarColumn(IsNullable = true)]
        public double? NoLoadCurrent { get; set; }

        [SugarColumn(IsNullable = true)]
        public int? NoLoadSpeed { get; set; }

        [SugarColumn(IsNullable = true)]
        public double? ShaftLength { get; set; }

        [SugarColumn(IsNullable = true)]
        public double? KnurlDiameter { get; set; }

        [SugarColumn(Length = 10, IsNullable = true)]
        public string? NoLoadResult { get; set; }

        // ---- 噪音阶段 ----
        [SugarColumn(IsNullable = true)]
        public double? FwdNoise { get; set; }

        [SugarColumn(IsNullable = true)]
        public double? RevNoise { get; set; }

        [SugarColumn(IsNullable = true)]
        public double? NoiseDiff { get; set; }

        [SugarColumn(Length = 10, IsNullable = true)]
        public string? NoiseResult { get; set; }

        // ---- 负载阶段 ----
        [SugarColumn(IsNullable = true)]
        public double? LoadCurrent { get; set; }

        [SugarColumn(IsNullable = true)]
        public int? LoadSpeed { get; set; }

        [SugarColumn(Length = 10, IsNullable = true)]
        public string? LoadResult { get; set; }
    }
}
