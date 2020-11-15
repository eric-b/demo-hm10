using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DemoBluetoothLE.Components
{
    /// <summary>
    /// Simulates a Central role serial terminal 
    /// with an HM-1x Bluetooth LE module
    /// acting as a Peripheral role.
    /// </summary>
    sealed class Hm1xConsoleTerminal : IAsyncDisposable
    {
        /// <summary>
        /// Assumed to be greater than max length of characteristic buffer to read.
        /// </summary>
        private const int ReadBufferLength = 1024;
        private const int ReadDisplayTimeoutMs = 250;

        private static readonly Encoding Utf8 = Encoding.UTF8;

        private readonly Timer _readDisplayTimer;
        private readonly byte[] _readBuffer;
        private int _readBufferOffset;
        private DateTimeOffset _lastReadTimestamp;

        private readonly Hm1xBLEClient _client;
        private int _disposeCount;

        public Hm1xConsoleTerminal(string deviceId)
        {
            _readBuffer = new byte[ReadBufferLength];
            _client = new Hm1xBLEClient(deviceId ?? throw new ArgumentNullException(nameof(deviceId)));
            _client.Connected += OnClientConnected;
            _client.Disconnected += OnClientDisconnected;

            _readDisplayTimer = new Timer(OnReadDisplayTimerCallback, null, Timeout.Infinite, Timeout.Infinite);
        }

        private void OnReadDisplayTimerCallback(object state)
        {
            FlushReadBuffer();
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Increment(ref _disposeCount) != 1)
                return default;

            _readDisplayTimer.Dispose();
            _client.Connected -= OnClientConnected;
            _client.Disconnected -= OnClientDisconnected;

            FlushReadBuffer();

            return _client.DisposeAsync();
        }


        public async Task Run()
        {
            if (!await _client.IsHm1xCompatibleDevice())
                return;

            Console.Clear();
            Console.WriteLine($"{_client.DeviceName} {_client.DeviceId}");
            await _client.SubscribeToNotifications(OnValueRead);
            Console.WriteLine($"Connected and ready.\r\nType 'exit' or 'quit' to stop.");

            while (_disposeCount == 0)
            {
                string input = Console.ReadLine();
                if (input == "exit" || input == "quit")
                    break;

                if (input != string.Empty)
                    await _client.Send(input);
            }
        }

        private void FlushReadBuffer()
        {
            DateTimeOffset timestamp = _lastReadTimestamp;
            int bufferLength = _readBufferOffset;
            if (bufferLength == 0)
                return;
            
            string msg = Utf8.GetString(_readBuffer, 0, bufferLength);
            _readBufferOffset = 0;

            var formattedMsg = string.Empty;
            bool lastCharNonAscii = false;
            for (int i = 0; i < msg.Length; i++)
            {
                char c = msg[i];
                if (c < 0x20 || c > 0x7e)
                {
                    if (lastCharNonAscii)
                    {
                        formattedMsg += $"{((byte)c):x2}";
                    }
                    else
                    {
                        if (i > 0)
                            formattedMsg += ' ';

                        formattedMsg += $"0x{((byte)c):x2}";
                        lastCharNonAscii = true;
                    }
                    
                }
                else
                {
                    if (lastCharNonAscii)
                    {
                        formattedMsg += " " + c;
                        lastCharNonAscii = false;
                    }
                    else
                    {
                        formattedMsg += c;
                    }
                }
            }
            Console.WriteLine($"{timestamp:HH:mm:ss}: {formattedMsg}");
        }

        private void FlushReadBufferIfTimeIntervalThreshold(DateTimeOffset currentTime)
        {
            if (_readBufferOffset != 0 && currentTime - _lastReadTimestamp >= TimeSpan.FromMilliseconds(ReadDisplayTimeoutMs))
                FlushReadBuffer();
        }

        private void OnValueRead(DateTimeOffset timestamp, byte[] buffer)
        {
            if (_readBufferOffset + buffer.Length > ReadBufferLength)
            {
                FlushReadBuffer();
            }
            else
            {
                FlushReadBufferIfTimeIntervalThreshold(timestamp);
            }

            Array.Copy(buffer, 0, _readBuffer, _readBufferOffset, buffer.Length);
            _readBufferOffset += buffer.Length;
            _lastReadTimestamp = timestamp;

            try
            {
                if (_disposeCount != 0)
                    return;
                _readDisplayTimer.Change(ReadDisplayTimeoutMs, Timeout.Infinite);
            }
            catch(ObjectDisposedException)
            {

            }
        }

        private void OnClientDisconnected(object sender, EventArgs e)
        {
            FlushReadBuffer();
            Console.WriteLine($"-- Device disconnected --");
        }

        private void OnClientConnected(object sender, EventArgs e)
        {
            Console.WriteLine($"-- Device connected --");
        }

    }
}
