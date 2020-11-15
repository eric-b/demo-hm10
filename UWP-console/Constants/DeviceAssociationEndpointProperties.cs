

namespace DemoBluetoothLE.Constants
{
    /// <summary>
    /// See https://docs.microsoft.com/fr-fr/windows/win32/properties/devices-bumper
    /// </summary>
    static class DeviceAssociationEndpointProperties
    {
        /// <summary>
        /// Address based on the protocol of the Device Association Endpoint. 
        /// IP Address for an IP device, Bluetooth address for Bluetooth device, etc.
        /// </summary>
        public const string DeviceAddress = "System.Devices.Aep.DeviceAddress";

        /// <summary>
        /// Whether the device is currently connected to the system or or not
        /// </summary>
        public const string IsConnected = "System.Devices.Aep.IsConnected";

        /// <summary>
        /// Whether the device is currently present or not
        /// </summary>
        public const string IsPresent = "System.Devices.Aep.IsPresent";

        /// <summary>
        /// Categories the device is part of. e.g. Printer, Camera, etc.
        /// </summary>
        public const string Category = "System.Devices.Aep.Category";
    }
}
