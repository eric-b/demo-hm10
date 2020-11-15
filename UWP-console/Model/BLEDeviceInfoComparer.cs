using System.Linq;
using System.Collections.Generic;

namespace DemoBluetoothLE.Model
{
    /// <summary>
    /// Arbitrary comparer to priviledge a typical unpaired arduino Bluetooth module
    /// in first position.
    /// </summary>
    sealed class BLEDeviceInfoComparer : IComparer<BLEDeviceInfo>
    {
        public static readonly BLEDeviceInfoComparer Instance = new BLEDeviceInfoComparer();

        private BLEDeviceInfoComparer()
        {
        }

        public int Compare(BLEDeviceInfo x, BLEDeviceInfo y)
        {
            if (x is null)
                return y is null ? 0 : -1;
            if (y is null)
                return 1;

            int scoreX = 0, scoreY = 0;

            bool xIsHid = x.Category.Contains("Input.Mouse") == true || x.Category.Contains("Input.Keyboard") == true;
            bool yIsHid = y.Category.Contains("Input.Mouse") == true || y.Category.Contains("Input.Keyboard") == true;
         
            if (x.IsPresent && !x.IsConnected)
                scoreX++;
            if (!x.IsPaired)
                scoreX++;
            if (!string.IsNullOrEmpty(x.Name))
                scoreX++;
            if (!xIsHid)
                scoreX++;

            if (y.IsPresent && !y.IsConnected)
                scoreY++;
            if (!y.IsPaired)
                scoreY++;
            if (!string.IsNullOrEmpty(y.Name))
                scoreY++;
            if (!yIsHid)
                scoreY++;

            int result = scoreX.CompareTo(scoreY) * -1;

            // Because original order is typically based on distance of device,
            // it's a meaningful sort order to keep.
            if (result == 0 && x.OriginalOrder != null && y.OriginalOrder != null)
                result = x.OriginalOrder.Value.CompareTo(y.OriginalOrder.Value);

            return result;
        }
    }
}
