// GPL-3.0-or-later. BlackShark V3 Pro protocol: openrazer/openrazer#2838.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

public static class RazerDevices {
 public const ushort Vid=0x1532,Pid=0x0577;

 public static byte[] Frame(byte route,byte command) {
  var b=new byte[64];b[0]=0x02;b[2]=0x60;b[6]=0x04;b[9]=route;b[10]=command;
  byte checksum=0;for(int i=0;i<62;i++)checksum^=b[i];b[62]=checksum;return b;
 }
 public static int Parse(byte[] r,byte command) {
  if(r==null||r.Length<63||r[0]!=0x02||r[10]!=command||r[12]!=0x01)return -1;
  byte checksum=0;for(int i=0;i<62;i++)checksum^=r[i];if(checksum!=r[62])return -1;
  return r[13];
 }
 static int Ask(HidChannel channel,byte command) {
  Native.HidD_FlushQueue(channel.Handle);if(!channel.Send(Frame(0x80,command)))return -1;
  var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<700) {
   var r=channel.Read((int)(700-watch.ElapsedMilliseconds));if(r==null)break;
   int value=Parse(r,command);if(value>=0)return value;
  }return -1;
 }
 public static List<BatteryState> Poll(List<HidInfo> all) {
  var result=new List<BatteryState>();
  var controls=all.Where(x=>x.Vid==Vid&&x.Pid==Pid&&x.Page==0xFF14&&x.Usage==1&&x.Input>=64&&x.Output>=64).ToList();
  var wakes=all.Where(x=>x.Vid==Vid&&x.Pid==Pid&&x.Page==0x000B&&x.Usage==5&&x.Output>=2).ToList();
  if(controls.Count==0||wakes.Count==0)return result;
  foreach(var controlInfo in controls)using(var control=new HidChannel(controlInfo)) {
   if(!control.Valid)continue;
   using(var wake=new HidChannel(wakes[0])) {
    if(!wake.Valid)continue;
    // Read-only setup used by the receiver: select the control channel, select
    // the headset target, then wake its radio before asking two status values.
    Native.HidD_FlushQueue(control.Handle);
    if(!control.Send(Frame(0x00,0x02)))continue;Thread.Sleep(50);
    if(!control.Send(Frame(0x00,0x2A)))continue;Thread.Sleep(50);
    for(int i=0;i<3;i++){wake.Send(new byte[]{0x05,0x00});Thread.Sleep(5);}
    int charging=Ask(control,0x2A),percent=Ask(control,0x21);
    if(percent>=0&&percent<=100)result.Add(new BatteryState{Key="razer:blackshark-v3-pro",Name="Razer BlackShark V3 Pro",Kind="Kulaklık",Percent=percent,Charging=charging==1,Sample=DateTime.Now,Status="Canlı • Razer USB HID"});
   }
   if(result.Count>0)break;
  }
  return result;
 }
}
