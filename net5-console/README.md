**WARNING: this compiles but does not work!**

Issue https://github.com/microsoft/CsWinRT/issues/535
Should be resolved in future release 1.1.0 of https://github.com/microsoft/CsWinRT

Error:

	System.InvalidCastException: Invalid cast from 'WinRT.IInspectable' to 'ABI.System.Collections.Generic.IEnumerable`1[System.Collections.Generic.KeyValuePair`2[System.String,System.Object]]'.
	   at ABI.System.Collections.Generic.IReadOnlyDictionary`2.FromAbiHelper..ctor(IMapView`2 mapView, ObjectReference`1 objRef)
	   at ABI.System.Collections.Generic.IReadOnlyDictionary`2.<>c__DisplayClass11_0.<_FromMapView>b__0()
	   at WinRT.IWinRTObject.<>c__DisplayClass13_0.<GetOrCreateTypeHelperData>b__0(RuntimeTypeHandle type)
	   at System.Collections.Concurrent.ConcurrentDictionary`2.GetOrAdd(TKey key, Func`2 valueFactory)
	   at WinRT.IWinRTObject.GetOrCreateTypeHelperData(RuntimeTypeHandle type, Func`1 helperDataFactory)
	   at ABI.System.Collections.Generic.IReadOnlyDictionary`2._FromMapView(IWinRTObject _this)
	   at ABI.System.Collections.Generic.IReadOnlyDictionary`2.global::System.Collections.Generic.IReadOnlyDictionary<K,V>.get_Item(K key)
	   at DemoBluetoothLE.Model.BLEDeviceInfo.FromRawDeviceInformation(DeviceInformation rawDeviceInformation) in C:\Users\ericb\source\repos\DemoBluetoothLE-hm1x\net5-console\DemoBluetoothLE-Hm1x-net5\Model\BLEDeviceInfo.cs:line 20

