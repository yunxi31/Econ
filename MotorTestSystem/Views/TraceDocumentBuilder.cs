using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using MotorTestSystem.ViewModels;

namespace MotorTestSystem.Views
{
    /// <summary>
    /// 电机测试追溯单 FlowDocument 构建器
    /// 生成专业的工业追溯单格式，用于打印输出
    /// </summary>
    public static class TraceDocumentBuilder
    {
        // 标准颜色
        private static readonly Color DarkText = Colors.Black;
        private static readonly Color HeaderBg = Color.FromRgb(41, 128, 185);   // 蓝色标题栏
        private static readonly Color StageHeaderBg = Color.FromRgb(52, 73, 94); // 深灰阶段标题
        private static readonly Color OkColor = Color.FromRgb(39, 174, 96);     // 绿色 OK
        private static readonly Color NgColor = Color.FromRgb(231, 76, 60);     // 红色 NG
        private static readonly Color LightBorder = Color.FromRgb(189, 195, 199);
        private static readonly Color LightBg = Color.FromRgb(245, 247, 249);
        private static readonly Color AlternateBg = Color.FromRgb(236, 240, 241);

        /// <summary>
        /// 构建追溯单 FlowDocument
        /// </summary>
        public static FlowDocument Build(MotorTestRecordModel motor)
        {
            var doc = new FlowDocument
            {
                PageWidth = 780,
                PageHeight = 1100,
                PagePadding = new Thickness(40, 30, 40, 30),
                ColumnGap = 0,
                ColumnWidth = 700,
                FontFamily = new FontFamily("Microsoft YaHei UI, SimSun"),
                FontSize = 11,
                Foreground = new SolidColorBrush(DarkText)
            };

            // ---- 标题 ----
            var title = new Paragraph(new Run("电机电性能测试追溯单"))
            {
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(HeaderBg),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            };
            doc.Blocks.Add(title);

            // 副标题
            var subtitle = new Paragraph(new Run("Motor Electrical Performance Test Traceability Sheet"))
            {
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141)),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16)
            };
            doc.Blocks.Add(subtitle);

            // 分割线
            doc.Blocks.Add(CreateDivider());

            // ---- 产品信息区 ----
            doc.Blocks.Add(CreateSectionHeader("一、产品基本信息"));

            var infoTable = new Table { CellSpacing = 0, BorderBrush = new SolidColorBrush(LightBorder), BorderThickness = new Thickness(0.5) };
            infoTable.Columns.Add(new TableColumn { Width = new GridLength(120) });
            infoTable.Columns.Add(new TableColumn { Width = new GridLength(230) });
            infoTable.Columns.Add(new TableColumn { Width = new GridLength(120) });
            infoTable.Columns.Add(new TableColumn { Width = new GridLength(230) });

            var infoRG = new TableRowGroup();
            infoRG.Rows.Add(CreateInfoRow("产品条码", motor.Barcode, "测试时间", motor.TestTime.ToString("yyyy-MM-dd HH:mm:ss")));
            infoRG.Rows.Add(CreateInfoRow("最终判定", motor.FinalResult, "报告编号", $"RPT-{DateTime.Now:yyyyMMdd}-{motor.Barcode.GetHashCode():X8}"));
            infoTable.RowGroups.Add(infoRG);
            doc.Blocks.Add(infoTable);

            doc.Blocks.Add(CreateSpacer(8));

            // ---- 空载测试区 ----
            doc.Blocks.Add(CreateSectionHeader("二、空载测试阶段 (A1/A2 工位)"));

            var noLoadTable = CreateTable(5);
            var noLoadRG = new TableRowGroup();
            noLoadRG.Rows.Add(CreateTableHeaderRow("检测项目", "实测值", "标准下限", "标准上限", "判定"));
            noLoadRG.Rows.Add(CreateDataRow(
                "空载电流 (A)",
                motor.NoLoadCurrent?.ToString("F3") ?? "-",
                "-",
                "1.500",
                motor.IsNoLoadCurrentAbnormal ? "NG" : "OK",
                motor.IsNoLoadCurrentAbnormal));
            noLoadRG.Rows.Add(CreateDataRow(
                "空载转速 (r/min)",
                motor.NoLoadSpeed?.ToString("F0") ?? "-",
                "2900",
                "3100",
                motor.IsNoLoadSpeedAbnormal ? "NG" : "OK",
                motor.IsNoLoadSpeedAbnormal));
            noLoadTable.RowGroups.Add(noLoadRG);
            doc.Blocks.Add(noLoadTable);

            doc.Blocks.Add(CreateSpacer(8));

            // ---- 噪音测试区 ----
            doc.Blocks.Add(CreateSectionHeader("三、噪音测试阶段 (A3/A4 工位)"));

            var noiseTable = CreateTable(5);
            var noiseRG = new TableRowGroup();
            noiseRG.Rows.Add(CreateTableHeaderRow("检测项目", "实测值", "标准下限", "标准上限", "判定"));
            noiseRG.Rows.Add(CreateDataRow(
                "正转噪音 (dB)",
                motor.FwdNoise?.ToString("F1") ?? "-",
                "-",
                "60.0",
                motor.IsFwdNoiseAbnormal ? "NG" : "OK",
                motor.IsFwdNoiseAbnormal));
            noiseRG.Rows.Add(CreateDataRow(
                "反转噪音 (dB)",
                motor.RevNoise?.ToString("F1") ?? "-",
                "-",
                "60.0",
                motor.IsRevNoiseAbnormal ? "NG" : "OK",
                motor.IsRevNoiseAbnormal));
            noiseTable.RowGroups.Add(noiseRG);
            doc.Blocks.Add(noiseTable);

            doc.Blocks.Add(CreateSpacer(8));

            // ---- 负载测试区 ----
            doc.Blocks.Add(CreateSectionHeader("四、负载测试阶段 (A5/A6 工位)"));

            var loadTable = CreateTable(5);
            var loadRG = new TableRowGroup();
            loadRG.Rows.Add(CreateTableHeaderRow("检测项目", "实测值", "标准下限", "标准上限", "判定"));
            loadRG.Rows.Add(CreateDataRow(
                "负载电流 (A)",
                motor.LoadCurrent?.ToString("F3") ?? "-",
                "-",
                "4.500",
                motor.IsLoadCurrentAbnormal ? "NG" : "OK",
                motor.IsLoadCurrentAbnormal));
            loadRG.Rows.Add(CreateDataRow(
                "负载转速 (r/min)",
                motor.LoadSpeed?.ToString("F0") ?? "-",
                "2900",
                "3100",
                motor.IsLoadSpeedAbnormal ? "NG" : "OK",
                motor.IsLoadSpeedAbnormal));
            loadTable.RowGroups.Add(loadRG);
            doc.Blocks.Add(loadTable);

            doc.Blocks.Add(CreateSpacer(8));

            // ---- 检验结论 ----
            doc.Blocks.Add(CreateSectionHeader("五、检验结论"));

            string conclusionText = motor.FinalResult == "OK"
                ? $"产品条码 {motor.Barcode} 经电性能测试，各项指标均符合标准要求，综合判定：合格。"
                : $"产品条码 {motor.Barcode} 经电性能测试，存在不合格项，综合判定：不合格。需返修或报废处理。";

            var conclusionPara = new Paragraph(new Run(conclusionText))
            {
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(motor.FinalResult == "OK" ? OkColor : NgColor),
                Margin = new Thickness(0, 4, 0, 8)
            };
            doc.Blocks.Add(conclusionPara);

            doc.Blocks.Add(CreateDivider());

            // ---- 签章区 ----
            doc.Blocks.Add(CreateSectionHeader("六、签章确认"));

            var signTable = new Table { CellSpacing = 0 };
            signTable.Columns.Add(new TableColumn { Width = new GridLength(233) });
            signTable.Columns.Add(new TableColumn { Width = new GridLength(233) });
            signTable.Columns.Add(new TableColumn { Width = new GridLength(234) });

            var signRG = new TableRowGroup();
            signRG.Rows.Add(CreateSignRow("检验员", "审核员", "批准人"));
            signRG.Rows.Add(CreateSignValueRow());
            signTable.RowGroups.Add(signRG);
            doc.Blocks.Add(signTable);

            doc.Blocks.Add(CreateSpacer(20));

            // ---- 页脚 ----
            var footer = new Paragraph()
            {
                TextAlignment = TextAlignment.Center,
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166)),
                Margin = new Thickness(0, 8, 0, 0)
            };
            footer.Inlines.Add(new Run($"MotorTestSystem 电机电性能测试系统  |  打印时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}  |  第 1 页"));
            doc.Blocks.Add(footer);

            return doc;
        }

        #region 辅助方法

        private static Block CreateDivider()
        {
            var para = new Paragraph
            {
                BorderBrush = new SolidColorBrush(LightBorder),
                BorderThickness = new Thickness(0, 0.5, 0, 0),
                Margin = new Thickness(0, 4, 0, 8),
                FontSize = 1
            };
            para.Inlines.Add(new Run(" ") { FontSize = 1 });
            return para;
        }

        private static Paragraph CreateSpacer(double height)
        {
            return new Paragraph { Margin = new Thickness(0, height / 2, 0, height / 2), FontSize = 2 };
        }

        private static Paragraph CreateSectionHeader(string text)
        {
            return new Paragraph(new Run(text))
            {
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(HeaderBg),
                Margin = new Thickness(0, 6, 0, 6)
            };
        }

        private static Table CreateTable(int columnCount)
        {
            var table = new Table
            {
                CellSpacing = 0,
                BorderBrush = new SolidColorBrush(LightBorder),
                BorderThickness = new Thickness(0.5)
            };

            // 均分列
            var width = new GridLength(700.0 / columnCount);
            for (int i = 0; i < columnCount; i++)
            {
                table.Columns.Add(new TableColumn { Width = width });
            }

            return table;
        }

        private static TableRow CreateTableHeaderRow(params string[] values)
        {
            var row = new TableRow { Background = new SolidColorBrush(StageHeaderBg) };
            foreach (var val in values)
            {
                row.Cells.Add(new TableCell(new Paragraph(new Run(val))
                {
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(4, 6, 4, 6)
                })
                {
                    BorderBrush = new SolidColorBrush(LightBorder),
                    BorderThickness = new Thickness(0.5)
                });
            }
            return row;
        }

        private static TableRow CreateInfoRow(string label1, string value1, string label2, string value2)
        {
            var row = new TableRow();
            row.Cells.Add(CreateLabelCell(label1));
            row.Cells.Add(CreateValueCell(value1));
            row.Cells.Add(CreateLabelCell(label2));
            row.Cells.Add(CreateValueCell(value2));
            return row;
        }

        private static TableRow CreateDataRow(string item, string actual, string lower, string upper, string judge, bool isNg)
        {
            var row = new TableRow { Background = new SolidColorBrush(LightBg) };
            row.Cells.Add(CreateCenterCell(item, false));
            row.Cells.Add(CreateCenterCell(actual, true));
            row.Cells.Add(CreateCenterCell(lower, false));
            row.Cells.Add(CreateCenterCell(upper, false));
            row.Cells.Add(CreateJudgeCell(judge, isNg));
            return row;
        }

        private static TableRow CreateSignRow(string s1, string s2, string s3)
        {
            var row = new TableRow();
            row.Cells.Add(new TableCell(new Paragraph(new Run(s1))
            {
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141)),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(4, 4, 4, 2)
            }));
            row.Cells.Add(new TableCell(new Paragraph(new Run(s2))
            {
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141)),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(4, 4, 4, 2)
            }));
            row.Cells.Add(new TableCell(new Paragraph(new Run(s3))
            {
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141)),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(4, 4, 4, 2)
            }));
            return row;
        }

        private static TableRow CreateSignValueRow()
        {
            var row = new TableRow();
            for (int i = 0; i < 3; i++)
            {
                row.Cells.Add(new TableCell(new Paragraph(new Run(" "))
                {
                    FontSize = 11,
                    Margin = new Thickness(4, 2, 4, 4)
                })
                {
                    BorderBrush = new SolidColorBrush(LightBorder),
                    BorderThickness = new Thickness(0, 0, 0, 0.5)
                });
            }
            return row;
        }

        private static TableCell CreateLabelCell(string text)
        {
            return new TableCell(new Paragraph(new Run(text))
            {
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 110, 120)),
                Margin = new Thickness(6, 5, 4, 5)
            })
            {
                Background = new SolidColorBrush(AlternateBg),
                BorderBrush = new SolidColorBrush(LightBorder),
                BorderThickness = new Thickness(0.5)
            };
        }

        private static TableCell CreateValueCell(string text)
        {
            return new TableCell(new Paragraph(new Run(text))
            {
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(DarkText),
                Margin = new Thickness(4, 5, 6, 5)
            })
            {
                BorderBrush = new SolidColorBrush(LightBorder),
                BorderThickness = new Thickness(0.5)
            };
        }

        private static TableCell CreateCenterCell(string text, bool bold)
        {
            return new TableCell(new Paragraph(new Run(text))
            {
                FontSize = 11,
                FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = new SolidColorBrush(DarkText),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(4, 5, 4, 5)
            })
            {
                BorderBrush = new SolidColorBrush(LightBorder),
                BorderThickness = new Thickness(0.5)
            };
        }

        private static TableCell CreateJudgeCell(string judge, bool isNg)
        {
            return new TableCell(new Paragraph(new Run(judge))
            {
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(isNg ? NgColor : OkColor),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(4, 5, 4, 5)
            })
            {
                BorderBrush = new SolidColorBrush(LightBorder),
                BorderThickness = new Thickness(0.5)
            };
        }

        #endregion
    }
}
