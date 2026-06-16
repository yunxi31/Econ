using System;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using MotorTestSystem.Models;
using MotorTestSystem.Services;
using Xunit;
using Random = System.Random;

namespace MotorTestSystem.Tests.PropertyTests
{
    /// <summary>
    /// FsCheck 自定义生成器注册类。
    /// </summary>
    public class MetaDataGenerators
    {
        /// <summary>
        /// 注册 DeadLetterMetadata 的自定义生成器。
        /// </summary>
        public static Arbitrary<DeadLetterMetadata> DeadLetterMetadata =>
            Arb.From(Gen.Fresh(() =>
            {
                var rng = new Random();
                int dataCount = rng.Next(1, 20);
                var dataList = Enumerable.Range(0, dataCount)
                    .Select(_ => Gen.Fresh(() =>
                    {
                        var drng = new Random();
                        return new StageTestData
                        {
                            Barcode = $"SN-{drng.Next(100000, 999999)}",
                            StationId = $"A{drng.Next(1, 7)}",
                            Stage = (TestStage)drng.Next(0, 3),
                            CollectedAt = DateTime.UtcNow.AddSeconds(-drng.Next(0, 86400)),
                            Result = drng.Next(0, 2) == 0 ? "OK" : "NG",
                            NoLoadCurrent = drng.Next(0, 2) == 0 ? null : Math.Round(drng.NextDouble() * 5, 3),
                            NoLoadSpeed = drng.Next(0, 2) == 0 ? null : drng.Next(2500, 3200),
                            ShaftLength = drng.Next(0, 2) == 0 ? null : Math.Round(drng.NextDouble() * 50, 3),
                            KnurlDiameter = drng.Next(0, 2) == 0 ? null : Math.Round(drng.NextDouble() * 20, 3),
                            FwdNoise = drng.Next(0, 2) == 0 ? null : Math.Round(drng.NextDouble() * 100, 2),
                            RevNoise = drng.Next(0, 2) == 0 ? null : Math.Round(drng.NextDouble() * 100, 2),
                            NoiseDiff = drng.Next(0, 2) == 0 ? null : Math.Round(drng.NextDouble() * 30, 2),
                            LoadCurrent = drng.Next(0, 2) == 0 ? null : Math.Round(drng.NextDouble() * 6, 3),
                            LoadSpeed = drng.Next(0, 2) == 0 ? null : drng.Next(1000, 3200),
                            Progress = drng.Next(0, 2) == 0 ? null : Math.Round(drng.NextDouble() * 100, 1),
                            Voltage = drng.Next(0, 2) == 0 ? null : Math.Round(drng.NextDouble() * 400, 1),
                            Current = drng.Next(0, 2) == 0 ? null : Math.Round(drng.NextDouble() * 10, 2),
                            RPM = drng.Next(0, 2) == 0 ? null : drng.Next(500, 5000)
                        };
                    }).Sample(1, 1).Single())
                    .ToList();

                return new DeadLetterMetadata
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Timestamp = DateTime.UtcNow.AddSeconds(-rng.Next(0, 3600)),
                    Stage = "BulkUpsert",
                    ErrorMessage = rng.Next(0, 2) == 0 ? "Timeout" : "SQLite locked",
                    ExceptionType = rng.Next(0, 2) == 0 ? null : "System.InvalidOperationException",
                    FailedData = dataList,
                    StationIds = dataList.Select(d => d.StationId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList(),
                    RetryCount = rng.Next(0, 10),
                    LastRetryAt = rng.Next(0, 2) == 0 ? null : DateTime.UtcNow.AddMinutes(-rng.Next(1, 60)),
                    IsFailed = rng.Next(0, 2) == 0
                };
            }));
    }

    /// <summary>
    /// Property 5: 死信队列序列化的往返属性
    /// Validates: Requirements 14.4, 14.5
    /// 验证 Parse(Serialize(data)) ≡ data
    /// </summary>
    public class DeadLetterSerializationRoundtripTests
    {
        private readonly DeadLetterSerializer _serializer = new();

        public DeadLetterSerializationRoundtripTests()
        {
            // 注册自定义 Arbitrary 生成器，使 [Property] 能够正确生成 DeadLetterMetadata
            Arb.Register<MetaDataGenerators>();
        }

        /// <summary>
        /// 验证死信元数据序列化往返：Parse(Serialize(data)) ≡ data
        /// 使用 FsCheck 随机生成测试数据，自动覆盖 double?、DateTime、null 等边缘情况。
        /// </summary>
        [Property(MaxTest = 100)]
        public void SerializationRoundtrip_ShouldPreserveAllFields(DeadLetterMetadata original)
        {
            // Act
            string json = _serializer.Serialize(original);
            var deserialized = _serializer.Deserialize(json);

            // Assert: 基本字段相等
            Assert.Equal(original.Id, deserialized.Id);
            Assert.Equal(original.Stage, deserialized.Stage);
            Assert.Equal(original.ErrorMessage, deserialized.ErrorMessage);
            Assert.Equal(original.ExceptionType, deserialized.ExceptionType);
            Assert.Equal(original.RetryCount, deserialized.RetryCount);
            Assert.Equal(original.IsFailed, deserialized.IsFailed);
            Assert.Equal(original.StationIds.Count, deserialized.StationIds.Count);
            Assert.Equal(original.FailedData.Count, deserialized.FailedData.Count);

            // 时间戳比较（JSON 序列化可能有精度损失）
            var tsDiff = Math.Abs((original.Timestamp - deserialized.Timestamp).TotalMilliseconds);
            Assert.True(tsDiff < 10, $"Timestamp roundtrip error: {tsDiff}ms");

            if (original.LastRetryAt.HasValue)
            {
                Assert.True(deserialized.LastRetryAt.HasValue);
                var retryDiff = Math.Abs((original.LastRetryAt.Value - deserialized.LastRetryAt.Value).TotalMilliseconds);
                Assert.True(retryDiff < 10, $"LastRetryAt roundtrip error: {retryDiff}ms");
            }

            // 验证 FailedData 列表中每条记录的每个字段
            for (int i = 0; i < original.FailedData.Count; i++)
            {
                var orig = original.FailedData[i];
                var des = deserialized.FailedData[i];
                Assert.Equal(orig.Barcode, des.Barcode);
                Assert.Equal(orig.StationId, des.StationId);
                Assert.Equal(orig.Stage, des.Stage);
                Assert.Equal(orig.Result, des.Result);
                Assert.Equal(orig.NoLoadCurrent, des.NoLoadCurrent);
                Assert.Equal(orig.NoLoadSpeed, des.NoLoadSpeed);
                Assert.Equal(orig.ShaftLength, des.ShaftLength);
                Assert.Equal(orig.KnurlDiameter, des.KnurlDiameter);
                Assert.Equal(orig.FwdNoise, des.FwdNoise);
                Assert.Equal(orig.RevNoise, des.RevNoise);
                Assert.Equal(orig.NoiseDiff, des.NoiseDiff);
                Assert.Equal(orig.LoadCurrent, des.LoadCurrent);
                Assert.Equal(orig.LoadSpeed, des.LoadSpeed);
                Assert.Equal(orig.Progress, des.Progress);
                Assert.Equal(orig.Voltage, des.Voltage);
                Assert.Equal(orig.Current, des.Current);
                Assert.Equal(orig.RPM, des.RPM);
            }
        }

        /// <summary>
        /// 验证序列化 null 值时的正确处理。
        /// </summary>
        [Fact]
        public void Serialization_WithAllNullValues_ShouldSucceed()
        {
            // Arrange
            var original = new DeadLetterMetadata
            {
                Id = "test-id",
                Timestamp = DateTime.UtcNow,
                Stage = "BulkUpsert",
                ErrorMessage = "Test error",
                FailedData = new()
                {
                    new StageTestData
                    {
                        Barcode = "SN-TEST-NULL",
                        StationId = "A1",
                        Stage = TestStage.NoLoad,
                        CollectedAt = DateTime.UtcNow,
                        Result = "NG",
                        // 所有可空字段均为 null
                    }
                }
            };

            // Act
            string json = _serializer.Serialize(original);
            var deserialized = _serializer.Deserialize(json);

            // Assert
            Assert.Equal(original.Id, deserialized.Id);
            Assert.Single(deserialized.FailedData);
            Assert.Null(deserialized.FailedData[0].NoLoadCurrent);
            Assert.Null(deserialized.FailedData[0].NoLoadSpeed);
        }
    }
}
