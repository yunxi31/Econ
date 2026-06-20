using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MotorTestSystem.Models;

namespace MotorTestSystem.Services
{
    /// <summary>
    /// 死信队列 JSON 序列化/反序列化组件。
    /// 使用 System.Text.Json 处理 double?、DateTime、null 等边缘情况。
    /// </summary>
    public sealed class DeadLetterSerializer
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };

        /// <summary>
        /// 将 DeadLetterMetadata 序列化为 JSON 字符串。
        /// </summary>
        public string Serialize(DeadLetterMetadata metadata)
        {
            ArgumentNullException.ThrowIfNull(metadata);
            return JsonSerializer.Serialize(metadata, Options);
        }

        /// <summary>
        /// 将 JSON 字符串反序列化为 DeadLetterMetadata。
        /// </summary>
        public DeadLetterMetadata Deserialize(string json)
        {
            ArgumentNullException.ThrowIfNull(json);
            var result = JsonSerializer.Deserialize<DeadLetterMetadata>(json, Options);
            return result ?? throw new InvalidOperationException("Deserialization returned null.");
        }

        /// <summary>
        /// 从文件读取并反序列化。
        /// </summary>
        public async Task<DeadLetterMetadata> DeserializeFromFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filePath);

            if (!File.Exists(filePath))
                throw new FileNotFoundException("Dead letter file not found.", filePath);

            string json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            return Deserialize(json);
        }

        /// <summary>
        /// 序列化并写入文件。
        /// </summary>
        public async Task SerializeToFileAsync(string filePath, DeadLetterMetadata metadata, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filePath);
            ArgumentNullException.ThrowIfNull(metadata);

            string json = Serialize(metadata);
            await File.WriteAllTextAsync(filePath, json, cancellationToken).ConfigureAwait(false);
        }
    }
}
