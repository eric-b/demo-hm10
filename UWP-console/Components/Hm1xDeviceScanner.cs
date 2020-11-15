using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using DemoBluetoothLE.Constants;
using DemoBluetoothLE.Model;

namespace DemoBluetoothLE.Components
{
    /// <summary>
    /// Looks for Bluetooth devices with expected custom service and characteristic
    /// exposed by HM-1x modules.
    /// </summary>
    sealed class Hm1xDeviceScanner
    {
        /// <summary>
        /// See https://docs.microsoft.com/en-us/windows/uwp/devices-sensors/aep-service-class-ids#bluetooth-and-bluetooth-le-services
        /// </summary>
        private const string BLEProtocolId = "bb7bb05e-5972-42b5-94fc-76eaa7084d49";

        /// <summary>
        /// See https://msdn.microsoft.com/en-us/library/windows/desktop/ff521659(v=vs.85).aspx
        /// </summary>
        private static readonly string[] RequestedProperties =
            {
                DeviceAssociationEndpointProperties.DeviceAddress,
                DeviceAssociationEndpointProperties.IsConnected,
                DeviceAssociationEndpointProperties.IsPresent,
                DeviceAssociationEndpointProperties.Category
            };

        public async Task<BLEDeviceInfo[]> ScanBluetoothAdvertisers()
        {
            // For a more responsive user experience, 
            //  see BluetoothLEAdvertisementWatcher or 
            //  DeviceInformation.CreateWatcher() 
            //  instead of DeviceInformation.FindAllAsync().
            const string bluetoothLeDevicesFilter = "(System.Devices.Aep.ProtocolId:=\"{" + BLEProtocolId + "}\")";
            DeviceInformationCollection bleDevices = await DeviceInformation.FindAllAsync(bluetoothLeDevicesFilter, RequestedProperties, DeviceInformationKind.AssociationEndpoint);
            
            var result = new List<BLEDeviceInfo>(bleDevices.Count);
            for (int i = 0; i < bleDevices.Count; i++)
            {
                var model = BLEDeviceInfo.FromRawDeviceInformation(bleDevices[i]) with { OriginalOrder = i };
                if (await CheckForCustomCharacteristic(model))
                    result.Add(model);
            }
            result.Sort(BLEDeviceInfoComparer.Instance);
            return result.ToArray();
        }

        private static async Task<bool> CheckForCustomCharacteristic(BLEDeviceInfo info)
        {
            try
            {
                await using (var client = new Hm1xBLEClient(info.Id))
                {
                    return await client.IsHm1xCompatibleDevice();
                }
            }
            catch 
            { }

            return false;
        }
    }
}
