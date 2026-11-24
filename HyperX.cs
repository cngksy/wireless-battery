// GPL-3.0-or-later. Cloud Alpha Wireless protocol references: README.md.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public static class HyperXDevices {
 public const ushort Vid=0x03F0,Pid=0x098D;
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
  return result;
 }
}
