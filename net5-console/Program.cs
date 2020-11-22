using System;
using System.Threading.Tasks;
using Windows.Storage;
using DemoBluetoothLE.Constants;

namespace DemoBluetoothLE
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                string deviceId;
                //deviceId = GetLastDeviceIdFromLocalSettings();
                //if (!string.IsNullOrEmpty(deviceId) && await DemoConsole.IsDeviceCompatible(deviceId))
                //    await DemoConsole.StartWithDevice(deviceId);
                //else
                    deviceId = await DemoConsole.StartWithDeviceSelection();

                //SaveLastDeviceIdInLocalSettings(deviceId);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            Console.WriteLine("Program terminated");
            Console.ReadKey(intercept: true);
        }

        static string GetLastDeviceIdFromLocalSettings()
        {
            string deviceId = ApplicationData.Current.LocalSettings.Values[ProgramSettings.LastDeviceId] as string;
            return deviceId;
        }

        static void SaveLastDeviceIdInLocalSettings(string deviceId)
        {
            ApplicationData.Current.LocalSettings.Values[ProgramSettings.LastDeviceId] = deviceId;
        }
    }
}
