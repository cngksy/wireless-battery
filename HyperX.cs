// GPL-3.0-or-later. HyperX protocol references: README.md.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public static class HyperXDevices {
 public const ushort Vid=0x03F0,Pid=0x098D;
 const ushort CloudIIIReceiver=0x05B7,CloudIIICable=0x06B7;
 const byte CloudIIIBattery=0x89,CloudIIICharging=0x8A;
 public static int Parse(byte[] response,byte command) {
  return response!=null&&response.Length>3&&response[0]==0x21&&response[1]==0xBB&&response[2]==command?response[3]:-1;
 }
 static int Ask(HidChannel channel,byte command) {
  Native.HidD_FlushQueue(channel.Handle);if(!channel.Send(new byte[]{0x21,0xBB,command}))return -1;
  var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<650) {
   var r=channel.Read((int)(650-watch.ElapsedMilliseconds));if(r==null)break;
   int value=Parse(r,command);if(value>=0)return value;
  }return -1;
 }
 public static List<BatteryState> Poll(List<HidInfo> all) {
  var result=new List<BatteryState>();
  var devices=all.Where(x=>x.Vid==Vid&&x.Pid==Pid&&x.Page==0xFF43&&x.Usage==0x0202&&x.Input>=31&&x.Output>=31).ToList();
  foreach(var info in devices)using(var channel=new HidChannel(info)) {
   if(!channel.Valid)continue;
   int link=Ask(channel,0x03);if(link==0)continue;
   int charging=Ask(channel,0x0C),percent=Ask(channel,0x0B);
   if(percent>=0&&percent<=100)result.Add(new BatteryState{Key="hyperx:cloud-alpha-wireless",Name="HyperX Cloud Alpha Wireless",Kind="Kulaklık",Percent=percent,Charging=charging==1,Sample=DateTime.Now,Status="Canlı • HyperX USB HID"});
   if(result.Count>0)break;
  }
  var cloudIII=ReadCloudIII(all);
  if(cloudIII!=null)result.Add(cloudIII);
  return result;
 }

 internal static bool IsCloudIIIChannel(HidInfo info) {
  return info.Vid==Vid&&(info.Pid==CloudIIIReceiver||info.Pid==CloudIIICable)
   &&info.Page==0xFF13&&info.Usage==1&&info.Input==62&&info.Output==62;
 }

 static BatteryState ReadCloudIII(IEnumerable<HidInfo> devices) {
  BatteryState reading=null;
  foreach(var info in devices.Where(IsCloudIIIChannel).OrderByDescending(x=>x.Pid==CloudIIICable)) {
   using(var channel=new HidChannel(info)) {
    if(!channel.Valid)continue;
    int percent=AskCloudIII(channel,CloudIIIBattery);
    if(percent<0)continue;
    int charging=AskCloudIII(channel,CloudIIICharging);
    if(reading==null)reading=new BatteryState {
     Key="hyperx:cloud-iii-wireless",Name="HyperX Cloud III Wireless",Kind="Kulaklık",
     Percent=percent,Connection=DeviceConnection.Connected,Sample=DateTime.Now,Status="Canlı • HyperX USB HID"
    };
    if(charging>=0) {
     reading.Charging=charging==1;
     break;
    }
   }
  }
  return reading;
 }

 static int AskCloudIII(HidChannel channel,byte command) {
  Native.HidD_FlushQueue(channel.Handle);
  if(!channel.Send(new byte[]{0x66,command},350))return -1;
  var watch=Stopwatch.StartNew();
  int remaining;
  while((remaining=450-(int)watch.ElapsedMilliseconds)>0) {
   var reply=channel.Read(remaining);
   if(reply==null)break;
   int value=ParseCloudIII(reply,command);
   if(value>=0)return value;
  }
  return -1;
 }

 internal static int ParseCloudIII(byte[] report,byte command) {
  if(report==null||report.Length<3||report[0]!=0x66)return -1;
  if(command==CloudIIIBattery&&(report[1]==CloudIIIBattery||report[1]==0x0D)) {
   if(report.Length>=5&&(report[2]!=0||report[3]!=0)&&report[4]<=100)return report[4];
  }
  if(command==CloudIIICharging&&(report[1]==CloudIIICharging||report[1]==0x0C)&&report[2]<=2)
   return report[2];
  return -1;
 }
}
