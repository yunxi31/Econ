using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using MotorTestSystem.Models;
using MotorTestSystem.Services;
using Xunit;

namespace MotorTestSystem.Tests.Performance
{
    public class DictionaryThreadSafetyTests
    {
        [Fact]
        public void TestDictionaryThreadSafety_ShouldExposeConcurrentWriteRisk()
        {
            // Arrange
            var mockRepo = new JitterMockRepository();
            var mockFactory = new JitterMockPlcClientFactory();
            
            // Just need a dummy polling service to access its private _consecutiveFailures dictionary
            var service = new PlcPollingService(
                Array.Empty<StationConfig>(),
                mockRepo,
                mockFactory
            );

            var incrementMethod = typeof(PlcPollingService).GetMethod("IncrementFailure", 
                BindingFlags.Instance | BindingFlags.NonPublic);
            var resetMethod = typeof(PlcPollingService).GetMethod("ResetFailure", 
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(incrementMethod);
            Assert.NotNull(resetMethod);

            // Act & Assert
            Exception exception = null;
            int totalIncrements = 1000;

            try
            {
                Parallel.For(0, totalIncrements, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
                {
                    incrementMethod.Invoke(service, new object[] { "GW-M0" });
                });
            }
            catch (AggregateException ae)
            {
                exception = ae.Flatten().InnerException;
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            // Verify no exception occurred during concurrent writes
            Assert.Null(exception);

            // Verify count consistency
            var field = typeof(PlcPollingService).GetField("_consecutiveFailures", 
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            var failuresDict = field.GetValue(service) as IDictionary<string, int>;
            Assert.NotNull(failuresDict);

            Assert.True(failuresDict.TryGetValue("GW-M0", out int finalCount));
            Assert.Equal(totalIncrements, finalCount);
        }
    }
}
