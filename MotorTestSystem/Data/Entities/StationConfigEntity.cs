using SqlSugar;
using System;

namespace MotorTestSystem.Models.Entities
{
    /// <summary>
    /// 工位配置实体 — 对应数据库表
    /// </summary>
    [SugarTable("StationConfigs")]
    public class StationConfigEntity
    {
        /// <summary>工位ID（如 A1）</summary>
        [SugarColumn(IsPrimaryKey = true, Length = 10)]
        public string Id { get; set; } = string.Empty;

        /// <summary>工位名称</summary>
        [SugarColumn(Length = 50, IsNullable = false)]
        public string Name { get; set; } = string.Empty;

        /// <summary>PLC型号</summary>
        [SugarColumn(Length = 50, IsNullable = false)]
        public string PlcModel { get; set; } = string.Empty;

        /// <summary>PLC IP地址</summary>
        [SugarColumn(Length = 50, IsNullable = false)]
        public string IpAddress { get; set; } = "192.168.1.1";

        /// <summary>PLC端口</summary>
        [SugarColumn(IsNullable = false)]
        public int Port { get; set; } = 502;

        /// <summary>通信协议</summary>
        [SugarColumn(Length = 50, IsNullable = false)]
        public string Protocol { get; set; } = "ModbusTCP";

        /// <summary>站号</summary>
        [SugarColumn(IsNullable = false)]
        public int StationId { get; set; } = 1;

        /// <summary>是否在线</summary>
        [SugarColumn(IsNullable = false)]
        public bool IsConnected { get; set; }

        /// <summary>状态文本</summary>
        [SugarColumn(Length = 20, IsNullable = false)]
        public string Status { get; set; } = "离线";
    }
}
