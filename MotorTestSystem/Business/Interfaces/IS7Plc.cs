using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using S7.Net;
using S7.Net.Types;

namespace MotorTestSystem.Services
{
    public interface IS7Plc : IDisposable
    {
        bool IsConnected { get; }
        Task OpenAsync(CancellationToken cancellationToken = default);
        void Close();
        Task<object?> ReadAsync(DataType dataType, int db, int startByteAdr, VarType varType, int varCount, byte bitAdr = 0, CancellationToken cancellationToken = default);
        Task WriteAsync(DataType dataType, int db, int startByteAdr, object value, int bitAdr = -1, CancellationToken cancellationToken = default);
        Task<List<DataItem>> ReadMultipleVarsAsync(List<DataItem> dataItems, CancellationToken cancellationToken = default);
    }
}
