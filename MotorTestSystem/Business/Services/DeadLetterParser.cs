using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace MotorTestSystem.Services
{
    /// <summary>
    /// 死信队列文件名解析工具。
    /// 文件名格式：{yyyyMMdd-HHmmss-fffffff}_{guid}.json
    /// </summary>
    public static class DeadLetterParser
    {
        private static readonly Regex FileNamePattern = new(
            @"^(\d{4}\d{2}\d{2}-\d{2}\d{2}\d{2}-\d{7})_([0-9a-fA-F]{32})\.json$",
            RegexOptions.Compiled | RegexOptions.Singleline);

        /// <summary>
        /// 从文件名解析时间戳。
        /// 返回 null 如果文件名格式不匹配。
        /// </summary>
        public static DateTime? ParseTimestamp(string fileName)
        {
            ArgumentNullException.ThrowIfNull(fileName);

            var match = FileNamePattern.Match(fileName);
            if (!match.Success)
                return null;

            string timestampPart = match.Groups[1].Value;
            // 格式: yyyyMMdd-HHmmss-fffffff
            // 需要转换为 DateTime
            string normalized = timestampPart.Replace("-", "");
            // 变为: yyyyMMddHHmmssfffffff (21 位)
            if (DateTime.TryParseExact(
                    normalized,
                    "yyyyMMddHHmmssfffffff",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var result))
            {
                return result;
            }

            return null;
        }

        /// <summary>
        /// 生成符合命名规范的文件名。
        /// </summary>
        public static string GenerateFileName(DateTime timestamp, string id)
        {
            string ts = timestamp.ToString("yyyyMMdd-HHmmss-fffffff", CultureInfo.InvariantCulture);
            return $"{ts}_{id}.json";
        }

        /// <summary>
        /// 检查文件名是否匹配死信队列命名规范。
        /// </summary>
        public static bool IsValidFileName(string fileName)
        {
            ArgumentNullException.ThrowIfNull(fileName);
            return FileNamePattern.IsMatch(fileName);
        }

        /// <summary>
        /// 检查文件是否已被标记为失败（.failed 后缀）。
        /// </summary>
        public static bool IsFailedFile(string fileName)
        {
            ArgumentNullException.ThrowIfNull(fileName);
            return fileName.EndsWith(".failed", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 将文件名标记为失败（追加 .failed 后缀）。
        /// </summary>
        public static string MarkAsFailedFileName(string fileName)
        {
            ArgumentNullException.ThrowIfNull(fileName);
            return fileName + ".failed";
        }
    }
}
