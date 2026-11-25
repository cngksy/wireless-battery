// GPL-3.0-or-later. Device IDs and input report references: README.md.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

static class EightBitDo {
 const string DeviceKey="8bitdo:ultimate-2c";
 const string Ultimate2Name="8BitDo Ultimate 2 Wireless";

 internal static bool IsUltimate2BatteryChannel(HidInfo info) {
  return info.Vid==0x2DC8&&info.Pid==0x310B&&info.Page==0xFF7A&&info.Usage==1
   &&info.Input==64&&info.Output==64&&(info.Name??"").StartsWith(Ultimate2Name,StringComparison.OrdinalIgnoreCase);
 }

 public static BatteryState ReadUltimate2(IEnumerable<HidInfo> devices) {
  foreach(var info in devices.Where(IsUltimate2BatteryChannel))using(var channel=new HidChannel(info)) {
   if(!channel.Valid)continue;
   Native.HidD_FlushQueue(channel.Handle);
   if(!channel.Send(new byte[]{0x81,0x04,0x21,0x01,0,0,0x06},300))continue;
   var timer=Stopwatch.StartNew();
   int remaining;
   while((remaining=350-(int)timer.ElapsedMilliseconds)>0) {
    var report=channel.Read(remaining);
    if(report==null)break;
    int status=ParseUltimate2Battery(report);
    if(status<0)continue;
    int percent=status&0x7F;
    return new BatteryState {
     Key="8bitdo:ultimate-2",Name=Ultimate2Name,Kind="Gamepad",Percent=percent,
     Charging=(status&0x80)!=0&&percent<100,Connection=DeviceConnection.Connected,
     Sample=DateTime.Now,Status="Canlı • USB HID"
    };
   }
  }
  return null;
 }

 internal static int ParseUltimate2Battery(byte[] report) {
  if(report==null||report.Length<19||report[0]!=0x02||report[1]!=0x04||report[2]!=0x04
   ||report[3]!=0||report[4]!=0x21||report[5]!=0x01)return -1;
  int percent=report[18]&0x7F;
  return percent>0&&percent<=100?report[18]:-1;
 }

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
