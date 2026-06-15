using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MotorTestSystem.Services;
using Xunit;

namespace MotorTestSystem.Tests.Performance
{
    public class StartupBlockingTests
    {
        [Fact]
        public async Task TestStartupBlocking_ShouldBeAsynchronousAndFast()
        {
            // Arrange
            ResetStaticStateAndCleanDatabase();

            // Act
            var sw = Stopwatch.StartNew();
            
            // Access BackendRuntime.GetSharedAsync() to trigger asynchronous initialization
            var runtimeTask = BackendRuntime.GetSharedAsync();
            
            // Getting the task should be immediate (under 100ms)
            var elapsedMs = sw.ElapsedMilliseconds;
            
            var runtime = await runtimeTask;
            sw.Stop();

            // Assert
            Assert.NotNull(runtime);
            Assert.True(elapsedMs < 100, 
                $"Expected task retrieval to be immediate (< 100ms), but took {elapsedMs}ms");
        }

        private static void ResetStaticStateAndCleanDatabase()
        {
            // Reset BackendRuntime.Shared backing field
            var field = typeof(BackendRuntime).GetField("<Shared>k__BackingField", 
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(null, null);
            }

            // Reset SqlSugarDbContext._initialized
            var initField = typeof(SqlSugarDbContext).GetField("_initialized", 
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (initField != null)
            {
                initField.SetValue(null, false);
            }

            // Delete database file to force seeding
            string dbPath = SqlSugarDbContext.DbPath;
            if (File.Exists(dbPath))
            {
                try
                {
                    File.Delete(dbPath);
                }
                catch (IOException)
                {
                    // If file is locked, try GC and wait
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    File.Delete(dbPath);
                }
            }
        }
    }
}
