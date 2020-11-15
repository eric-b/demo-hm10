using DemoBluetoothLE.Constants;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;

namespace DemoBluetoothLE.Components
{
    /// <summary>
    /// Wrapper for <see cref="BluetoothLEDevice"/> and
    /// custom GATT service exposed by HM-1x module.
    /// </summary>
    sealed class Hm1xBLEClient : IAsyncDisposable
    {
        private readonly Timer _connectionTimer;

        /// <summary>
        /// Semaphore to use when <see cref="_device"/> needs
        /// to be reinitialized (in case of connection lost).
        /// </summary>
        private readonly SemaphoreSlim _deviceSemaphore;
        
        private BluetoothLEDevice _device;
        private GattCharacteristic _characteristic;
        
        private Action<DateTimeOffset, byte[]> _externalReadCallback;

        private int _disposeCount;

        public EventHandler Connected, Disconnected;

        public string DeviceId { get; }

        public string DeviceName => _device?.Name;

        public Hm1xBLEClient(string deviceId)
        {
            DeviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
            _connectionTimer = new Timer(OnConnectionTimerCallback, null, Timeout.Infinite, Timeout.Infinite);
            _deviceSemaphore = new SemaphoreSlim(initialCount: 1, maxCount: 1);
        }


        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Increment(ref _disposeCount) != 1)
                return;

            _connectionTimer.Change(Timeout.Infinite, Timeout.Infinite);
            await _deviceSemaphore.WaitAsync();
            try
            {
                try
                {
                    if (_characteristic != null)
                    {
                        await UnsubscribeFromDevice();
                    }

                    _device?.Dispose();
                    _connectionTimer.Dispose();
                }
                catch
                {
                }
            }
            finally
            {
                _deviceSemaphore.Release();
            }
            _deviceSemaphore.Dispose();
        }

        /// <summary>
        /// Looks for custom service and characteristic exposed by HM-1x modules.
        /// </summary>
        /// <returns></returns>
        public Task<bool> IsHm1xCompatibleDevice()
            => IsHm1xCompatibleDeviceInternal(sync: true);

        private async Task<bool> IsHm1xCompatibleDeviceInternal(bool sync)
        {
            await EnsureDeviceInitialized(sync);
            return _characteristic != null &&
                _characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify) &&
                _characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Read) &&
                _characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Write) &&
                !_device.DeviceInformation.Pairing.IsPaired; // There is a bug in Windows 10: subscription to characteristic will fail if device is paired!
        }

        public Task SubscribeToNotifications(Action<DateTimeOffset, byte[]> readBufferCallback)
        {
            return SubscribeToNotificationsInternal(readBufferCallback, sync: true);
        }

        public async Task<bool> Send(string input)
        {
            await _deviceSemaphore.WaitAsync();
            try
            {
                if (_device.ConnectionStatus != BluetoothConnectionStatus.Connected)
                {
                    return false;
                }

                try
                {
                    IBuffer buffer = CryptographicBuffer.ConvertStringToBinary(input, BinaryStringEncoding.Utf8);
                    GattCommunicationStatus status = await _characteristic.WriteValueAsync(buffer, GattWriteOption.WriteWithResponse);
                    if (status == GattCommunicationStatus.Success)
                        return true;
                    else
                    {
                        Debug.WriteLine($"Send status: {status}");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                    return false;
                }
            }
            finally
            {
                _deviceSemaphore.Release();
            }
        }


        private async Task<bool> SubscribeToNotificationsInternal(Action<DateTimeOffset, byte[]> readBufferCallback, bool sync)
        {
            var task = new Func<Task<bool>>(async () =>
            {
                if (!await IsHm1xCompatibleDeviceInternal(sync: false))
                {
                    return false;
                }
                GattCommunicationStatus status = await _characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                if (status != GattCommunicationStatus.Success)
                    throw new Exception($"Failed to subscribe to GATT characteristic {_characteristic.Uuid}: {status}.");

                _externalReadCallback = readBufferCallback ?? throw new ArgumentNullException(nameof(readBufferCallback));
                _characteristic.ValueChanged += OnCharacteristicValueChanged;
                return true;
            });

            if (sync)
            {
                await _deviceSemaphore.WaitAsync();
            }
            try
            {
                return await Task.Run(task);
            }
            finally
            {
                if (sync)
                {
                    _deviceSemaphore.Release();
                }
            }
        }

        private async Task UnsubscribeFromDevice()
        {
            _device.ConnectionStatusChanged -= OnDeviceConnectionStatusChanged;
            if (_device.ConnectionStatus == BluetoothConnectionStatus.Connected)
                await _characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None);
            _characteristic.ValueChanged -= OnCharacteristicValueChanged;
            Debug.WriteLine("Unsubscribed from device");
        }


        private async void OnConnectionTimerCallback(object state)
        {
            if (_disposeCount != 0)
                return;
            try
            {
                BluetoothConnectionStatus status = _device.ConnectionStatus;
                if (status == BluetoothConnectionStatus.Connected)
                    return;

                await _deviceSemaphore.WaitAsync();
                try
                {
                    if (_characteristic != null)
                        await UnsubscribeFromDevice();
                    _device.Dispose();
                    _device = null;
                    if (!await SubscribeToNotificationsInternal(_externalReadCallback, sync: false) && _disposeCount == 0)
                    {
                        // Failed to reconnect
                        try
                        {
                            _connectionTimer.Change(60000, Timeout.Infinite);
                        }
                        catch (ObjectDisposedException)
                        {
                        }
                    }
                }
                finally
                {
                    _deviceSemaphore.Release();
                }
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }


        private void OnCharacteristicValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            if (_disposeCount != 0)
                return;

            byte[] buffer;
            CryptographicBuffer.CopyToByteArray(args.CharacteristicValue, out buffer);
            _externalReadCallback(args.Timestamp, buffer);
        }

        private async Task EnsureDeviceInitialized(bool sync)
        {
            var action = new Func<Task>(async () =>
            {
                if (_device != null)
                    return;

                _device = await BluetoothLEDevice.FromIdAsync(DeviceId);
                if (_device == null)
                {
                    // Happens if Bluetooth capability not checked in app manifest
                    // https://docs.microsoft.com/en-us/uwp/schemas/appxpackage/how-to-specify-device-capabilities-for-bluetooth
                    throw new Exception("Device not found. If Device ID is correct, ensure Bluetooth Capability is enabled in application manifest.");
                }

                Debug.WriteLine($"BluetoothLEDevice initialized: {_device.DeviceId}");
                (_, _characteristic) = await GetCustomHm1xCharacteristic(_device);

                if (_characteristic != null)
                {
                    _device.ConnectionStatusChanged += OnDeviceConnectionStatusChanged;
                    Connected?.Invoke(this, EventArgs.Empty);
                }
            });

            if (sync)
            {
                await _deviceSemaphore.WaitAsync();
            }
            try
            {
                await Task.Run(action);
            }
            finally
            {
                if (sync)
                {
                    _deviceSemaphore.Release();
                }
            }
        }

        private void OnDeviceConnectionStatusChanged(BluetoothLEDevice sender, object args)
        {
            if (_disposeCount != 0)
                return;

            BluetoothConnectionStatus status = _device.ConnectionStatus;
            Debug.WriteLine($"Connection status: {status}");
            if (status == BluetoothConnectionStatus.Connected)
            {
                Connected?.Invoke(this, EventArgs.Empty);
                try
                {
                    _connectionTimer.Change(Timeout.Infinite, Timeout.Infinite);
                }
                catch
                {

                }
            }
            else
            {
                Disconnected?.Invoke(this, EventArgs.Empty);
                try
                {
                    _connectionTimer.Change(30000, Timeout.Infinite);
                }
                catch
                {

                }
            }
        }

        private static async Task<(GattDeviceService, GattCharacteristic)> GetCustomHm1xCharacteristic(BluetoothLEDevice device)
        {
            const string bluetoothBaseGuidSuffix = "0000-1000-8000-00805F9B34FB";
            GattDeviceServicesResult serviceResult = await device.GetGattServicesForUuidAsync(new Guid($"{Hm1xShortUuids.CustomService}-{bluetoothBaseGuidSuffix}"));
            if (serviceResult.Status != GattCommunicationStatus.Success)
            {
                Debug.WriteLine($"No GATT service {Hm1xShortUuids.CustomService} found on device {device.DeviceId} {device.Name}: status {serviceResult.Status} {serviceResult.ProtocolError:x}.");
                return (null, null);
            }

            GattDeviceService service = serviceResult.Services[0];
            GattCharacteristicsResult characteristicResult = await service.GetCharacteristicsForUuidAsync(new Guid($"{Hm1xShortUuids.CustomCharacteristic}-{bluetoothBaseGuidSuffix}"));
            if (characteristicResult.Status != GattCommunicationStatus.Success)
            {
                Debug.WriteLine($"No GATT characteristic {Hm1xShortUuids.CustomCharacteristic} found on device {device.DeviceId} {device.Name}: status {characteristicResult.Status} {characteristicResult.ProtocolError:x}.");
                return (service, null);
            }
            return (service, characteristicResult.Characteristics[0]);
        }

    }
}
