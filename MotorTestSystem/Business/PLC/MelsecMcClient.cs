using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MotorTestSystem.Models;

namespace MotorTestSystem.Services
{
    public sealed class MelsecMcClient : IPlcClient
    {
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private bool _isDisposed;

        public MelsecMcClient(StationConfig config)
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
                // 1. Read Completion Signal (M100)
                // Device code: 0x90 (M), Start address: 100, Count: 1
                bool completionSignal = await ReadBitAsync(0x90, 100, cancellationToken);

                StageTestData? completedData = null;
                if (completionSignal)
                {
                    // 2. Read Test Data (D100 to D209)
                    // Device code: 0xA8 (D), Start address: 100, Count: 110 words
                    byte[] rawData = await ReadWordsAsync(0xA8, 100, 110, cancellationToken);

                    // Parse variables
                    // D100 (Current): 16-bit signed integer, scale factor /1000
                    short rawCurrent = BitConverter.ToInt16(rawData, 0);
                    double current = Math.Round(rawCurrent / 1000.0, 3);

                    // D102 (Speed): 16-bit signed integer
                    short speed = BitConverter.ToInt16(rawData, 4);

                    // D104 (ShaftLength): 16-bit signed integer, scale factor /1000
                    short rawLength = BitConverter.ToInt16(rawData, 8);
                    double shaftLength = Math.Round(rawLength / 1000.0, 3);

                    // D106 (KnurlDiameter): 16-bit signed integer, scale factor /1000
                    short rawDiameter = BitConverter.ToInt16(rawData, 12);
                    double knurlDiameter = Math.Round(rawDiameter / 1000.0, 3);

                    // D200 to D209 (Barcode): ASCII string (20 bytes)
                    string barcode = DecodeAsciiString(rawData, 200, 20);

                    // Resolve stage and test result
                    TestStage stage = ResolveStage(Config.Id);
                    string result = stage switch
                    {
                        TestStage.NoLoad => (current > 2.3 || knurlDiameter > 4.65) ? "NG" : "OK",
                        TestStage.Noise => (current > 70.0) ? "NG" : "OK", // Dummy check if read noise as current
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
                        // In noise stage, map read parameters to noise fields
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
            catch
            {
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
                // Write M100 = False (0x00)
                await WriteBitAsync(0x90, 100, false, cancellationToken);
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

        private async Task<bool> ReadBitAsync(byte deviceCode, int address, CancellationToken cancellationToken)
        {
            byte[] request = BuildReadFrame(deviceCode, address, 1, isBitUnit: true);
            byte[] response = await SendAndReceiveAsync(request, cancellationToken);
            
            // Check End Code
            ushort endCode = BitConverter.ToUInt16(response, 9);
            if (endCode != 0)
            {
                throw new InvalidOperationException($"MC ReadBit failed with end code: 0x{endCode:X4}");
            }

            // In bit read, response data starts at byte 11.
            // High nibble of the first byte holds the first bit's value.
            return (response[11] & 0xF0) == 0x10;
        }

        private async Task<byte[]> ReadWordsAsync(byte deviceCode, int address, ushort count, CancellationToken cancellationToken)
        {
            byte[] request = BuildReadFrame(deviceCode, address, count, isBitUnit: false);
            byte[] response = await SendAndReceiveAsync(request, cancellationToken);

            ushort endCode = BitConverter.ToUInt16(response, 9);
            if (endCode != 0)
            {
                throw new InvalidOperationException($"MC ReadWords failed with end code: 0x{endCode:X4}");
            }

            // Response data starts at byte 11.
            int dataLen = response.Length - 11;
            byte[] data = new byte[dataLen];
            Buffer.BlockCopy(response, 11, data, 0, dataLen);
            return data;
        }

        private async Task WriteBitAsync(byte deviceCode, int address, bool value, CancellationToken cancellationToken)
        {
            byte[] request = BuildWriteBitFrame(deviceCode, address, value);
            byte[] response = await SendAndReceiveAsync(request, cancellationToken);

            ushort endCode = BitConverter.ToUInt16(response, 9);
            if (endCode != 0)
            {
                throw new InvalidOperationException($"MC WriteBit failed with end code: 0x{endCode:X4}");
            }
        }

        private byte[] BuildReadFrame(byte deviceCode, int address, ushort count, bool isBitUnit)
        {
            byte[] frame = new byte[21];

            // 1. Subheader: 50 00
            frame[0] = 0x50;
            frame[1] = 0x00;

            // 2. Network No: 00
            frame[2] = 0x00;

            // 3. PC No: FF
            frame[3] = 0xFF;

            // 4. Dest Module I/O: FF 03
            frame[4] = 0xFF;
            frame[5] = 0x03;

            // 5. Dest Station No: 00
            frame[6] = 0x00;

            // 6. Request Data Length: CPU timer (2) + Command (2) + Subcommand (2) + Address (3) + Device (1) + Count (2) = 12 bytes
            frame[7] = 0x0C;
            frame[8] = 0x00;

            // 7. CPU Timer: 0A 00
            frame[9] = 0x0A;
            frame[10] = 0x00;

            // 8. Command: Batch Read (0401 -> 01 04)
            frame[11] = 0x01;
            frame[12] = 0x04;

            // 9. Subcommand: 0000 for Word, 0100 for Bit (0100 -> 01 00 in bit mode)
            frame[13] = (byte)(isBitUnit ? 0x01 : 0x00);
            frame[14] = 0x00;

            // 10. Start Address: 3 bytes (little endian)
            frame[15] = (byte)(address & 0xFF);
            frame[16] = (byte)((address >> 8) & 0xFF);
            frame[17] = (byte)((address >> 16) & 0xFF);

            // 11. Device Code: 1 byte
            frame[18] = deviceCode;

            // 12. Count: 2 bytes (little-endian)
            frame[19] = (byte)(count & 0xFF);
            frame[20] = (byte)((count >> 8) & 0xFF);

            return frame;
        }

        private byte[] BuildWriteBitFrame(byte deviceCode, int address, bool value)
        {
            byte[] frame = new byte[22];

            // Subheader
            frame[0] = 0x50;
            frame[1] = 0x00;
            frame[2] = 0x00; // Network No
            frame[3] = 0xFF; // PC No
            frame[4] = 0xFF; // I/O
            frame[5] = 0x03;
            frame[6] = 0x00; // Station No

            // Length: 12 + 1 data byte = 13 bytes
            frame[7] = 0x0D;
            frame[8] = 0x00;

            // CPU Timer
            frame[9] = 0x0A;
            frame[10] = 0x00;

            // Command: Batch Write (1401 -> 01 14)
            frame[11] = 0x01;
            frame[12] = 0x14;

            // Subcommand: 01 00 (Bit unit)
            frame[13] = 0x01;
            frame[14] = 0x00;

            // Address
            frame[15] = (byte)(address & 0xFF);
            frame[16] = (byte)((address >> 8) & 0xFF);
            frame[17] = (byte)((address >> 16) & 0xFF);

            // Device code
            frame[18] = deviceCode;

            // Count: 1 point
            frame[19] = 0x01;
            frame[20] = 0x00;

            // Data: 1 byte (0x10 for True, 0x00 for False)
            frame[21] = (byte)(value ? 0x10 : 0x00);

            return frame;
        }

        private async Task<byte[]> SendAndReceiveAsync(byte[] request, CancellationToken cancellationToken)
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

            // Response header is 11 bytes
            byte[] header = new byte[11];
            int read = 0;
            while (read < 11)
            {
                int n = await _stream.ReadAsync(header, read, 11 - read, cancellationToken);
                if (n <= 0)
                {
                    throw new SocketException((int)SocketError.ConnectionReset);
                }
                read += n;
            }

            // Check Subheader (should be D0 00)
            if (header[0] != 0xD0 || header[1] != 0x00)
            {
                throw new InvalidOperationException($"Invalid MC response subheader: {header[0]:X2} {header[1]:X2}");
            }

            // Remaining length is at offset 7-8
            ushort remainingLength = BitConverter.ToUInt16(header, 7);
            
            // The remaining bytes include the End Code (2 bytes) and the actual data payload.
            // Since End Code is already read into the header (offset 9, 10), we need to read remainingLength - 2 bytes.
            int payloadLength = remainingLength - 2;
            byte[] payload = new byte[payloadLength];
            read = 0;
            while (read < payloadLength)
            {
                int n = await _stream.ReadAsync(payload, read, payloadLength - read, cancellationToken);
                if (n <= 0)
                {
                    throw new SocketException((int)SocketError.ConnectionReset);
                }
                read += n;
            }

            // Combine header and payload into a single response buffer
            byte[] response = new byte[11 + payloadLength];
            Buffer.BlockCopy(header, 0, response, 0, 11);
            Buffer.BlockCopy(payload, 0, response, 11, payloadLength);

            return response;
        }

        private static string DecodeAsciiString(byte[] data, int byteOffset, int count)
        {
            if (byteOffset + count > data.Length)
            {
                return "ERR_LEN";
            }

            // MC stores characters in words, often low byte first, then high byte.
            // Let's decode them and swap bytes.
            byte[] chars = new byte[count];
            for (int i = 0; i < count; i += 2)
            {
                if (i + 1 < count)
                {
                    chars[i] = data[byteOffset + i + 1]; // high byte (first character in word)
                    chars[i + 1] = data[byteOffset + i]; // low byte (second character in word)
                }
            }

            string result = Encoding.ASCII.GetString(chars).Trim('\0', ' ', '\r', '\n');
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
