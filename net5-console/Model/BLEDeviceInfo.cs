using System;
using Windows.Devices.Enumeration;
using DemoBluetoothLE.Constants;

namespace DemoBluetoothLE.Model
{
    public sealed record BLEDeviceInfo
    {
        public string Id { get; init; }
        public string Name { get; init; }
        public bool IsPaired { get; init; }
        public bool IsConnected { get; init; }
        public bool IsPresent { get; init; }
        public string Address { get; init; }
        public string[] Category { get; init; }
        public int? OriginalOrder { get; init; }

        public static BLEDeviceInfo FromRawDeviceInformation(DeviceInformation rawDeviceInformation) 
        {
            return new BLEDeviceInfo()
            {
                Id = rawDeviceInformation.Id ,
                Name = rawDeviceInformation.Name,
                IsPaired = rawDeviceInformation.Pairing.IsPaired,
                IsConnected = (bool?)rawDeviceInformation.Properties[DeviceAssociationEndpointProperties.IsConnected] == true,
                IsPresent = (bool?)rawDeviceInformation.Properties[DeviceAssociationEndpointProperties.IsPresent] == true,
                Address = (string)rawDeviceInformation.Properties[DeviceAssociationEndpointProperties.DeviceAddress],
                Category = (string[])rawDeviceInformation.Properties[DeviceAssociationEndpointProperties.Category] ?? Array.Empty<string>()
            };

        }
    }
}
