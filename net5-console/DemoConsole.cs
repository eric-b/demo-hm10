using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using DemoBluetoothLE.Components;
using DemoBluetoothLE.Model;

namespace DemoBluetoothLE
{
    static class DemoConsole
    {
        public static async Task<bool> IsDeviceCompatible(string deviceId)
        {
            await using (var client = new Hm1xBLEClient(deviceId))
            {
                return await client.IsHm1xCompatibleDevice();
            }
        }

        public static async Task StartWithDevice(string deviceId)
        {
            await using (var terminal = new Hm1xConsoleTerminal(deviceId))
            {
                await terminal.Run();
            }
        }

        public static async Task<string> StartWithDeviceSelection()
        {
            var scanner = new Hm1xDeviceScanner();
            Console.WriteLine("Scanning Bluetooth Low Energy devices of kind HM-1x, please wait...");
            BLEDeviceInfo[] devices = await scanner.ScanBluetoothAdvertisers();
            BLEDeviceInfo selectedDeviceInfo = SelectDevice(devices);
            if (selectedDeviceInfo != null)
            {
                string deviceId = selectedDeviceInfo.Id;
                await StartWithDevice(deviceId);
                return deviceId;
            }
            return null;
        }

        private static BLEDeviceInfo SelectDevice(BLEDeviceInfo[] devices)
        {
            if (devices.Length == 0)
            {
                Console.WriteLine("No compatible Bluetooth LE device found.\r\nEnsure that:\r\n- your device is powered and it is advertising.\r\n- device is compatible with HM-10 module.\r\n- device is not paired with Windows (see Bluetooth Devices in Control Panel).");
                return null;
            }

            if (devices.Length == 1)
            {
                BLEDeviceInfo selectedDevice = devices[0];
                Console.WriteLine($"A single device matches expected GATT service:\r\n{JsonConvert.SerializeObject(selectedDevice, Formatting.Indented)}\r\nTo continue, press any key");
                Console.ReadKey(intercept: true);
                return selectedDevice;
            }

            string statusMsg = string.Empty;
            while (true)
            {
                Console.Clear();
                Console.WriteLine(statusMsg);
                Console.WriteLine("Select Bluetooth LE device:");
                for (int i = 0; i < devices.Length; i++)
                {
                    var item = devices[i];
                    Console.WriteLine($"[{i + 1}] {item.Address}\t{item.Name}");
                }

                string input = Console.ReadLine();
                if (!int.TryParse(input, out int index))
                {
                    Console.WriteLine("No selection");
                    return null;
                }
                if (index < 1 || index > devices.Length)
                {
                    statusMsg = $"Selection {index} is out of range";
                    continue;
                }

                BLEDeviceInfo selectedDevice = devices[index - 1];
                Console.WriteLine($"Selected device:\r\n{JsonConvert.SerializeObject(selectedDevice, Formatting.Indented)}\r\nTo confirm, press Enter");
                if (Console.ReadKey(intercept: true).Key == ConsoleKey.Enter)
                    return selectedDevice;
            }
        }  
    }
}
