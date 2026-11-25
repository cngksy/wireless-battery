// GPL-3.0-or-later. Device IDs and input report references: README.md.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

static class EightBitDo {
 const string DeviceKey="8bitdo:ultimate-2c";

 public static BatteryState ReadUltimate2C(IEnumerable<HidInfo> devices) {
  var gamepads=devices.Where(IsUltimate2CGamepad).ToList();
  if(gamepads.Count==0)return null;
  foreach(var info in gamepads) {
   using(var channel=new HidChannel(info,true)) {
    if(!channel.Valid)continue;
    var stopwatch=Stopwatch.StartNew();
    int remaining;
    while((remaining=600-(int)stopwatch.ElapsedMilliseconds)>0) {
     var report=channel.Read(Math.Min(100,remaining));
     if(report!=null&&report.Length>=15&&(report[0]==1||report[0]==4)&&(report[14]&0x7F)<=100)
      return Reading(report[14]);
    }
   }
  }
  return Reading(-1);
 }

 static BatteryState Reading(int status) {
  int percentage=status<0?-1:status&0x7F;
  return new BatteryState {
   Key=DeviceKey,Name="8BitDo Ultimate 2C Wireless",Kind="Gamepad",
   Percent=percentage,Charging=status>=0&&(status&0x80)!=0&&percentage<100,
   Connection=DeviceConnection.Connected,Sample=status<0?default(DateTime):DateTime.Now,
   Status=status<0?"USB HID • Pil seviyesi bildirilmedi":"Canlı • USB HID"
  };
 }

 static bool IsUltimate2CGamepad(HidInfo info) {
  return info.Vid==0x2DC8&&(info.Pid==0x310A||info.Pid==0x301A||info.Pid==0x301B)
   &&info.Page==1&&info.Usage==5&&info.Input>=15;
 }
}
