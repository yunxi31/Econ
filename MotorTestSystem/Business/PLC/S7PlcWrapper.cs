using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using S7.Net;
using S7.Net.Types;

namespace MotorTestSystem.Services
{
    public class S7PlcWrapper : IS7Plc
    {
        private readonly Plc _plc;

        public S7PlcWrapper(CpuType cpu, string ip, int port, short rack, short slot)
        {
            _plc = new Plc(cpu, ip, port, rack, slot);
        }

        public bool IsConnected => _plc.IsConnected;

        public Task OpenAsync(CancellationToken cancellationToken = default)
        {
            return _plc.OpenAsync(cancellationToken);
        }

        public void Close()
        {
            _plc.Close();
        }

        public Task<object?> ReadAsync(DataType dataType, int db, int startByteAdr, VarType varType, int varCount, byte bitAdr = 0, CancellationToken cancellationToken = default)
        {
            return _plc.ReadAsync(dataType, db, startByteAdr, varType, varCount, bitAdr, cancellationToken);
        }

        public Task WriteAsync(DataType dataType, int db, int startByteAdr, object value, int bitAdr = -1, CancellationToken cancellationToken = default)
        {
            return _plc.WriteAsync(dataType, db, startByteAdr, value, bitAdr, cancellationToken);
        }

        public Task<List<DataItem>> ReadMultipleVarsAsync(List<DataItem> dataItems, CancellationToken cancellationToken = default)
        {
            return _plc.ReadMultipleVarsAsync(dataItems, cancellationToken);
        }

        public void Dispose()
        {
            ((IDisposable)_plc).Dispose();
        }
    }
}
