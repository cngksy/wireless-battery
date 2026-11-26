using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Gaming.Input;
using Windows.Foundation;
using Windows.System.Power;

static class WindowsRuntimeBattery {
 const string BatteryLife="System.Devices.BatteryLife";
 const string BluetoothBattery="{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";
 const string ContainerId="System.Devices.ContainerId";
 const string ClassOfDevice="System.Devices.Aep.Bluetooth.Cod.Major";
 static int controllerListPrimed;

 public static void AddBluetooth(List<BatteryState> readings) {
  try {
   {
    string classic=BluetoothDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected);
    string lowEnergy=BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected);
    var operation=DeviceInformation.FindAllAsync("("+classic+") OR ("+lowEnergy+")",
     new[]{BatteryLife,BluetoothBattery,ContainerId,ClassOfDevice},DeviceInformationKind.AssociationEndpoint);
    var completed=new TaskCompletionSource<AsyncStatus>();
    operation.Completed=(op,status)=>completed.TrySetResult(status);
    if(!completed.Task.Wait(1500)){operation.Cancel();return;}
    if(completed.Task.Result!=AsyncStatus.Completed)return;
    var devices=operation.GetResults();
    foreach(var device in devices) {
     object value;
     int percent=device.Properties.TryGetValue(BatteryLife,out value)?ReadPercent(value):-1;
     if(percent<0&&device.Properties.TryGetValue(BluetoothBattery,out value))percent=ReadPercent(value);
     if(percent<0||string.IsNullOrWhiteSpace(device.Name))continue;
     string key=device.Properties.TryGetValue(ContainerId,out value)&&value is Guid&&(Guid)value!=Guid.Empty
      ?"windows:"+((Guid)value).ToString():Native.HidKey("windows:bluetooth",device.Id);
     string name=WindowsDevices.BluetoothName(device.Name);
     uint cod=device.Properties.TryGetValue(ClassOfDevice,out value)&&value is uint?(uint)value<<8:0;
     var existing=readings.FirstOrDefault(x=>x.Key==key)??readings.SingleOrDefaultSafe(x=>
      string.Equals(WindowsDevices.BluetoothName(x.Name),name,StringComparison.OrdinalIgnoreCase));
     if(existing!=null&&!existing.Stale)continue;
     if(existing!=null){key=existing.Key;readings.Remove(existing);}
     readings.Add(new BatteryState{Key=key,Name=name,Kind=WindowsDevices.KindFromClass(cod,name),
      Percent=percent,Connection=DeviceConnection.Connected,Sample=DateTime.Now,Status="Windows Bluetooth pil bilgisi"});
    }
   }
  } catch(OperationCanceledException){} catch(COMException){} catch(ArgumentException){} catch(TypeLoadException){} catch(MissingMethodException){}
 }
 internal static int ReadPercent(object value) {
  if(value is byte)return (byte)value<=100?(byte)value:-1;
  if(value is uint)return (uint)value<=100?(int)(uint)value:-1;
  if(value is int)return (int)value>=0&&(int)value<=100?(int)value:-1;
  return -1;
 }
 internal static int CapacityPercent(int? remaining,int? full) {
  if(!remaining.HasValue||remaining.Value<0||!full.HasValue||full.Value<=0)return -1;
  return (int)Math.Min(100,Math.Round(remaining.Value*100.0/full.Value));
 }
 public static void AddGameControllers(List<BatteryState> readings) {
  try {
   var controllers=RawGameController.RawGameControllers;
   if(Interlocked.Exchange(ref controllerListPrimed,1)==0&&controllers.Count==0) {
    Thread.Sleep(300);
    controllers=RawGameController.RawGameControllers;
   }
   foreach(var controller in controllers) {
    if(controller.HardwareVendorId!=0x045E)continue;
    var battery=controller.TryGetBatteryReport();
    if(battery==null||battery.Status==BatteryStatus.NotPresent)continue;
    int percent=CapacityPercent(battery.RemainingCapacityInMilliwattHours,battery.FullChargeCapacityInMilliwattHours);
    if(percent<0)continue;
    string id=controller.NonRoamableId;
    if(string.IsNullOrEmpty(id))continue;
    string name=string.IsNullOrWhiteSpace(controller.DisplayName)?"Xbox Wireless Controller":controller.DisplayName;
    readings.Add(new BatteryState{Key=Native.HidKey("windows:wgi",id),Name=name,Kind="Gamepad",
     Percent=percent,Charging=battery.Status==BatteryStatus.Charging,Sample=DateTime.Now,
     Connection=DeviceConnection.Connected,Status="Windows Gaming Input"});
   }
  } catch(COMException){} catch(TypeLoadException){} catch(MissingMethodException){}
 }

 static T SingleOrDefaultSafe<T>(this IEnumerable<T> items,Func<T,bool> matches) where T:class {
  using(var found=items.Where(matches).GetEnumerator()) {
   if(!found.MoveNext())return null;
   T first=found.Current;
   return found.MoveNext()?null:first;
  }
 }
}
