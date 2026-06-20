using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MiniExcelLibs;
using MiniExcelLibs.Attributes;
using MiniExcelLibs.OpenXml;
using MotorTestSystem.Models;

namespace MotorTestSystem.Services
{
    /// <summary>
    /// 使用 MiniExcel 将 MotorTestResult 列表导出为格式化 xlsx 文件。
    /// </summary>
    public static class ExcelExportService
    {
        // ── 列定义（用于动态字典行，控制列顺序与表头文字）──────────────────────
        private static readonly MiniExcelLibs.OpenXml.OpenXmlConfiguration _config = new()
        {
            TableStyles = TableStyles.Default,   // 使用默认的表格样式
            AutoFilter   = true,
            FreezeRowCount = 1,                  // 冻结首行表头
            MinWidth     = 12,
        };

        /// <summary>
        /// 异步将 <paramref name="results"/> 导出到 <paramref name="filePath"/>。
        /// 返回写入的行数。
        /// </summary>
        public static async Task<int> ExportAsync(
            IEnumerable<MotorTestResult> results,
            string filePath)
        {
            var rows = results
                .Select(r => new ExcelRow(r))
                .ToList();

            await MiniExcel.SaveAsAsync(filePath, rows, excelType: ExcelType.XLSX,
                configuration: _config, overwriteFile: true);

            return rows.Count;
        }

        // ── 强类型行 DTO（ExcelColumn 特性控制列名与格式）─────────────────────
        private sealed class ExcelRow
        {
            public ExcelRow(MotorTestResult r)
            {
                Barcode        = r.Barcode;
                TestTime       = r.TestTime;
                FinalResult    = r.FinalResult;

                NoLoadCurrent  = r.NoLoadCurrent;
                NoLoadSpeed    = r.NoLoadSpeed;
                ShaftLength    = r.ShaftLength;
                KnurlDiameter  = r.KnurlDiameter;
                NoLoadResult   = r.NoLoadResult ?? string.Empty;

                FwdNoise       = r.FwdNoise;
                RevNoise       = r.RevNoise;
                NoiseDiff      = r.NoiseDiff;
                NoiseResult    = r.NoiseResult ?? string.Empty;

                LoadCurrent    = r.LoadCurrent;
                LoadSpeed      = r.LoadSpeed;
                LoadResult     = r.LoadResult ?? string.Empty;
            }

            [ExcelColumn(Name = "条形码", Index = 0, Width = 20)]
            public string Barcode { get; }

            [ExcelColumn(Name = "测试时间", Index = 1, Width = 20,
                Format = "yyyy-MM-dd HH:mm:ss")]
            public DateTime TestTime { get; }

            [ExcelColumn(Name = "综合判定", Index = 2, Width = 10)]
            public string FinalResult { get; }

            // ── 阶段 1：空载 ─────────────────────────────────────────────────
            [ExcelColumn(Name = "空载电流 (A)", Index = 3, Width = 14,
                Format = "0.00")]
            public double? NoLoadCurrent { get; }

            [ExcelColumn(Name = "空载转速 (r/min)", Index = 4, Width = 16)]
            public int? NoLoadSpeed { get; }

            [ExcelColumn(Name = "轴伸长度 (mm)", Index = 5, Width = 14,
                Format = "0.00")]
            public double? ShaftLength { get; }

            [ExcelColumn(Name = "滚花直径 (mm)", Index = 6, Width = 14,
                Format = "0.00")]
            public double? KnurlDiameter { get; }

            [ExcelColumn(Name = "空载判定", Index = 7, Width = 10)]
            public string NoLoadResult { get; }

            // ── 阶段 2：噪音 ─────────────────────────────────────────────────
            [ExcelColumn(Name = "正转噪音 (dB)", Index = 8, Width = 14,
                Format = "0.0")]
            public double? FwdNoise { get; }

            [ExcelColumn(Name = "反转噪音 (dB)", Index = 9, Width = 14,
                Format = "0.0")]
            public double? RevNoise { get; }

            [ExcelColumn(Name = "噪音差值 (dB)", Index = 10, Width = 14,
                Format = "0.0")]
            public double? NoiseDiff { get; }

            [ExcelColumn(Name = "噪音判定", Index = 11, Width = 10)]
            public string NoiseResult { get; }

            // ── 阶段 3：负载 ─────────────────────────────────────────────────
            [ExcelColumn(Name = "负载电流 (A)", Index = 12, Width = 14,
                Format = "0.00")]
            public double? LoadCurrent { get; }

            [ExcelColumn(Name = "负载转速 (r/min)", Index = 13, Width = 16)]
            public int? LoadSpeed { get; }

            [ExcelColumn(Name = "负载判定", Index = 14, Width = 10)]
            public string LoadResult { get; }
        }
    }
}
