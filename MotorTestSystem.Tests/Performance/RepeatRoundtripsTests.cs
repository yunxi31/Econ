using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MotorTestSystem.Models;
using MotorTestSystem.Services;
using S7.Net;
using S7.Net.Types;
using Xunit;

namespace MotorTestSystem.Tests.Performance
{
    public class RepeatRoundtripsTests
    {
        [Fact]
        public async Task TestRepeatRoundtrips_ShouldShowRedundantPlcReads()
        {
            // Arrange
            var config = new StationConfig
            {
                Id = "A1",
                PlcModel = "S7-1200",
                IpAddress = "127.0.0.1",
                Port = 102,
                StationId = 1
            };

            var mockPlc = new MockPlc();
            var client = new S7PlcClient(config, (cpu, ip, port, rack, slot) => mockPlc);

            // Act
            var snapshot = await client.ReadSnapshotAsync();

            // Assert
            Assert.True(snapshot.IsOnline);
            Assert.True(snapshot.CompletionSignal);
            Assert.NotNull(snapshot.CompletedData);
            Assert.Equal("TEST-BARCODE", snapshot.CompletedData.Barcode);

            // After optimization, reading a snapshot with completion signal true takes exactly 1 batch read:
            Assert.Equal(1, mockPlc.ReadMultipleCallCount);
            Assert.Equal(0, mockPlc.ReadCallCount);
        }
    }

    public class MockPlc : IS7Plc
    {
        public int ReadCallCount { get; private set; }
        public int ReadMultipleCallCount { get; private set; }

        public bool IsConnected => true;

        public Task OpenAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void Close()
        {
        }

        public Task<object?> ReadAsync(DataType dataType, int db, int startByteAdr, VarType varType, int varCount, byte bitAdr = 0, CancellationToken cancellationToken = default)
        {
            ReadCallCount++;

            if (dataType == DataType.Memory && varType == VarType.Bit)
            {
                // Completion signal (M100.0)
                return Task.FromResult<object?>(true);
            }
            if (dataType == DataType.DataBlock && varType == VarType.Word)
            {
                // Numeric data (DB1.DBW100 8 bytes)
                return Task.FromResult<object?>(new ushort[] { 1500, 2000, 12000, 25000 });
            }
            if (dataType == DataType.DataBlock && varType == VarType.String)
            {
                // Barcode (DB1.DBD200 String)
                return Task.FromResult<object?>("TEST-BARCODE");
            }

            return Task.FromResult<object?>(null);
        }

        public Task WriteAsync(DataType dataType, int db, int startByteAdr, object value, int bitAdr = -1, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<List<DataItem>> ReadMultipleVarsAsync(List<DataItem> dataItems, CancellationToken cancellationToken = default)
        {
            ReadMultipleCallCount++;
            foreach (var item in dataItems)
            {
                if (item.DataType == DataType.Memory && item.VarType == VarType.Bit)
                {
                    item.Value = true;
                }
                else if (item.DataType == DataType.DataBlock && item.VarType == VarType.Word)
                {
                    item.Value = new ushort[] { (ushort)1500, (ushort)2000, (ushort)12000, (ushort)25000 };
                }
                else if (item.DataType == DataType.DataBlock && item.VarType == VarType.String)
                {
                    item.Value = "TEST-BARCODE";
                }
            }
            return Task.FromResult(dataItems);
        }

        public void Dispose()
        {
        }
    }
}
