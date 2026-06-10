using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MotorTestSystem.Models;

namespace MotorTestSystem.Services
{
    public sealed class ModbusTcpClient : IPlcClient
    {
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private bool _isDisposed;
        private ushort _transactionId;

        public ModbusTcpClient(StationConfig config)
        {
            Config = config;
        }

        public StationConfig Config { get; }

        public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_tcpClient is { Connected: true })
            {
                return true;
            }

            await _lock.WaitAsync(cancellationToken);
            try
            {
                if (_tcpClient is { Connected: true })
                {
                    return true;
                }

                CloseConnection();

                _tcpClient = new TcpClient();
                // Connect with a 2-second timeout
                var connectTask = _tcpClient.ConnectAsync(Config.IpAddress, Config.Port, cancellationToken).AsTask();
                if (await Task.WhenAny(connectTask, Task.Delay(2000, cancellationToken)) != connectTask)
                {
                    CloseConnection();
                    return false;
                }

                if (!_tcpClient.Connected)
                {
                    CloseConnection();
                    return false;
                }

                _stream = _tcpClient.GetStream();
                return true;
            }
            catch
            {
                CloseConnection();
                return false;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<StationSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default)
        {
            if (!await ConnectAsync(cancellationToken))
            {
                return new StationSnapshot
                {
                    StationId = Config.Id,
                    IsOnline = false,
                    Status = 2, // Fault/Offline
                    CompletionSignal = false
                };
            }

            await _lock.WaitAsync(cancellationToken);
            try
            {
                // 1. Read Completion Signal (M100 -> Coil address 100)
                bool completionSignal = await ReadCoilAsync(100, cancellationToken);

                StageTestData? completedData = null;
                if (completionSignal)
                {
                    // 2. Read Test Data (D100 to D209 -> Holding Register address 100, count 110)
                    byte[] rawData = await ReadHoldingRegistersAsync(100, 110, cancellationToken);

                    // Parse variables in big-endian
                    // D100 (Current): 16-bit signed integer (offset 0), scale factor /1000
                    short rawCurrent = ReadInt16Be(rawData, 0);
                    double current = Math.Round(rawCurrent / 1000.0, 3);

                    // D102 (Speed): 16-bit signed integer (offset 4, i.e., Word offset 2)
                    short speed = ReadInt16Be(rawData, 4);

                    // D104 (ShaftLength): 16-bit signed integer (offset 8, i.e., Word offset 4), scale factor /1000
                    short rawLength = ReadInt16Be(rawData, 8);
                    double shaftLength = Math.Round(rawLength / 1000.0, 3);

                    // D106 (KnurlDiameter): 16-bit signed integer (offset 12, i.e., Word offset 6), scale factor /1000
                    short rawDiameter = ReadInt16Be(rawData, 12);
                    double knurlDiameter = Math.Round(rawDiameter / 1000.0, 3);

                    // D200 to D209 (Barcode): ASCII string (20 bytes, offset 200, i.e., Word offset 100)
                    // Note: No byte swapping in Modbus TCP due to big-endian transmission.
                    string barcode = DecodeAsciiString(rawData, 200, 20);

                    // Resolve stage and test result
                    TestStage stage = ResolveStage(Config.Id);
                    string result = stage switch
                    {
                        TestStage.NoLoad => (current > 2.3 || knurlDiameter > 4.65) ? "NG" : "OK",
                        TestStage.Noise => (current > 70.0) ? "NG" : "OK",
                        TestStage.Load => (current > 3.2) ? "NG" : "OK",
                        _ => "OK"
                    };

                    completedData = new StageTestData
                    {
                        Barcode = barcode,
                        StationId = Config.Id,
                        Stage = stage,
                        CollectedAt = DateTime.Now,
                        Result = result
                    };

                    // Populate specific stage parameters
                    if (stage == TestStage.NoLoad)
                    {
                        completedData.NoLoadCurrent = current;
                        completedData.NoLoadSpeed = speed;
                        completedData.ShaftLength = shaftLength;
                        completedData.KnurlDiameter = knurlDiameter;
                    }
                    else if (stage == TestStage.Noise)
                    {
                        completedData.FwdNoise = current; // map current to noise
                        completedData.RevNoise = speed / 10.0;
                        completedData.NoiseDiff = Math.Round(Math.Abs(completedData.FwdNoise.Value - completedData.RevNoise.Value), 2);
                        completedData.Result = (completedData.NoiseDiff > 10.0 || completedData.FwdNoise > 70.0) ? "NG" : "OK";
                    }
                    else if (stage == TestStage.Load)
                    {
                        completedData.LoadCurrent = current;
                        completedData.LoadSpeed = speed;
                    }
                }

                return new StationSnapshot
                {
                    StationId = Config.Id,
                    IsOnline = true,
                    Status = completionSignal ? 1 : 0, // 1: Running, 0: Idle
                    CompletionSignal = completionSignal,
                    CompletedData = completedData
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ModbusTcpClient Error] ReadSnapshotAsync failed: {ex.Message}\n{ex.StackTrace}");
                CloseConnection();
                return new StationSnapshot
                {
                    StationId = Config.Id,
                    IsOnline = false,
                    Status = 2,
                    CompletionSignal = false
                };
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task ResetCompletionSignalAsync(CancellationToken cancellationToken = default)
        {
            if (!await ConnectAsync(cancellationToken))
            {
                return;
            }

            await _lock.WaitAsync(cancellationToken);
            try
            {
                // Write M100 = False (FC 05, Coil address 100, value 0x0000)
                await WriteCoilAsync(100, false, cancellationToken);
            }
            catch
            {
                CloseConnection();
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<bool> ReadCoilAsync(ushort address, CancellationToken cancellationToken)
        {
            ushort transactionId = ++_transactionId;
            byte[] request = new byte[12];
            
            // Transaction ID
            request[0] = (byte)((transactionId >> 8) & 0xFF);
            request[1] = (byte)(transactionId & 0xFF);
            // Protocol ID (0x0000)
            request[2] = 0x00;
            request[3] = 0x00;
            // Length (6)
            request[4] = 0x00;
            request[5] = 0x06;
            // Unit ID
            request[6] = Config.StationId;
            // Function Code (0x01)
            request[7] = 0x01;
            // Coil Address
            request[8] = (byte)((address >> 8) & 0xFF);
            request[9] = (byte)(address & 0xFF);
            // Quantity of Coils (1)
            request[10] = 0x00;
            request[11] = 0x01;

            byte[] response = await SendAndReceiveAsync(transactionId, request, 9, cancellationToken);
            
            // Check byte count (should be 1)
            byte byteCount = response[8];
            if (byteCount < 1)
            {
                throw new InvalidOperationException("Invalid byte count in Modbus read coil response.");
            }

            // Coil status is in response[9] bit 0
            return (response[9] & 0x01) == 0x01;
        }

        private async Task<byte[]> ReadHoldingRegistersAsync(ushort address, ushort count, CancellationToken cancellationToken)
        {
            ushort transactionId = ++_transactionId;
            byte[] request = new byte[12];

            // Transaction ID
            request[0] = (byte)((transactionId >> 8) & 0xFF);
            request[1] = (byte)(transactionId & 0xFF);
            // Protocol ID (0x0000)
            request[2] = 0x00;
            request[3] = 0x00;
            // Length (6)
            request[4] = 0x00;
            request[5] = 0x06;
            // Unit ID
            request[6] = Config.StationId;
            // Function Code (0x03)
            request[7] = 0x03;
            // Address
            request[8] = (byte)((address >> 8) & 0xFF);
            request[9] = (byte)(address & 0xFF);
            // Quantity of Registers
            request[10] = (byte)((count >> 8) & 0xFF);
            request[11] = (byte)(count & 0xFF);

            int expectedResponseLength = 9 + (count * 2);
            byte[] response = await SendAndReceiveAsync(transactionId, request, expectedResponseLength, cancellationToken);

            byte byteCount = response[8];
            if (byteCount != count * 2)
            {
                throw new InvalidOperationException($"Invalid byte count in Modbus response. Expected: {count * 2}, got: {byteCount}");
            }

            byte[] data = new byte[byteCount];
            Buffer.BlockCopy(response, 9, data, 0, byteCount);
            return data;
        }

        private async Task WriteCoilAsync(ushort address, bool value, CancellationToken cancellationToken)
        {
            ushort transactionId = ++_transactionId;
            byte[] request = new byte[12];

            // Transaction ID
            request[0] = (byte)((transactionId >> 8) & 0xFF);
            request[1] = (byte)(transactionId & 0xFF);
            // Protocol ID (0x0000)
            request[2] = 0x00;
            request[3] = 0x00;
            // Length (6)
            request[4] = 0x00;
            request[5] = 0x06;
            // Unit ID
            request[6] = Config.StationId;
            // Function Code (0x05)
            request[7] = 0x05;
            // Address
            request[8] = (byte)((address >> 8) & 0xFF);
            request[9] = (byte)(address & 0xFF);
            // Value (0xFF00 for true, 0x0000 for false)
            request[10] = (byte)(value ? 0xFF : 0x00);
            request[11] = 0x00;

            // Echo response (12 bytes)
            await SendAndReceiveAsync(transactionId, request, 12, cancellationToken);
        }

        private async Task<byte[]> SendAndReceiveAsync(ushort transactionId, byte[] request, int expectedLength, CancellationToken cancellationToken)
        {
            if (_stream == null)
            {
                throw new InvalidOperationException("Not connected to PLC");
            }

            // Clear any buffered data in stream
            if (_tcpClient?.Available > 0)
            {
                byte[] junk = new byte[_tcpClient.Available];
                await _stream.ReadAsync(junk, 0, junk.Length, cancellationToken);
            }

            await _stream.WriteAsync(request, 0, request.Length, cancellationToken);

            // Read the Modbus TCP response header (7 bytes)
            byte[] mbapHeader = new byte[7];
            int read = 0;
            while (read < 7)
            {
                int n = await _stream.ReadAsync(mbapHeader, read, 7 - read, cancellationToken);
                if (n <= 0)
                {
                    throw new SocketException((int)SocketError.ConnectionReset);
                }
                read += n;
            }

            // Verify Transaction ID
            ushort rxTxId = (ushort)((mbapHeader[0] << 8) | mbapHeader[1]);
            if (rxTxId != transactionId)
            {
                throw new InvalidOperationException($"Modbus transaction ID mismatch. Expected: {transactionId}, got: {rxTxId}");
            }

            // Verify Protocol ID
            ushort rxProtoId = (ushort)((mbapHeader[2] << 8) | mbapHeader[3]);
            if (rxProtoId != 0)
            {
                throw new InvalidOperationException($"Invalid Modbus protocol ID: {rxProtoId}");
            }

            // Get remaining length (length of unit ID + function code + data)
            ushort remainingLength = (ushort)((mbapHeader[4] << 8) | mbapHeader[5]);
            if (remainingLength < 2)
            {
                throw new InvalidOperationException($"Invalid Modbus remaining length: {remainingLength}");
            }

            // Read remaining data
            int bytesToRead = remainingLength - 1;
            byte[] payload = new byte[bytesToRead];
            read = 0;
            while (read < bytesToRead)
            {
                int n = await _stream.ReadAsync(payload, read, bytesToRead - read, cancellationToken);
                if (n <= 0)
                {
                    throw new SocketException((int)SocketError.ConnectionReset);
                }
                read += n;
            }

            // Check if it is a Modbus Exception
            byte functionCode = payload[0];
            if ((functionCode & 0x80) != 0)
            {
                byte exceptionCode = payload[1];
                throw new InvalidOperationException($"Modbus exception received. Function Code: 0x{functionCode:X2}, Exception Code: 0x{exceptionCode:X2}");
            }

            // Combine MBAP and payload into a single buffer
            byte[] response = new byte[7 + bytesToRead];
            Buffer.BlockCopy(mbapHeader, 0, response, 0, 7);
            Buffer.BlockCopy(payload, 0, response, 7, bytesToRead);

            return response;
        }

        private static short ReadInt16Be(byte[] data, int offset)
        {
            if (offset + 1 >= data.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset out of data bounds.");
            }
            return (short)((data[offset] << 8) | data[offset + 1]);
        }

        private static string DecodeAsciiString(byte[] data, int byteOffset, int count)
        {
            if (byteOffset + count > data.Length)
            {
                return "ERR_LEN";
            }

            // Modbus TCP (big-endian) transmits register values high byte first.
            // If the string starts with the high byte of the first register, then the characters
            // are already sequentially in memory: data[0], data[1], data[2], data[3]...
            // So we don't swap bytes! We just read directly.
            string result = Encoding.ASCII.GetString(data, byteOffset, count).Trim('\0', ' ', '\r', '\n');
            return string.IsNullOrEmpty(result) ? "UNKNOWN_BARCODE" : result;
        }

        private static TestStage ResolveStage(string stationId)
        {
            return stationId switch
            {
                "A1" or "A2" => TestStage.NoLoad,
                "A3" or "A4" => TestStage.Noise,
                "A5" or "A6" => TestStage.Load,
                _ => TestStage.NoLoad
            };
        }

        private void CloseConnection()
        {
            _stream?.Dispose();
            _stream = null;
            _tcpClient?.Close();
            _tcpClient = null;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            CloseConnection();
            _lock.Dispose();
        }
    }
}
