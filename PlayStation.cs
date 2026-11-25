// GPL-3.0-or-later. Input report layout: Linux hid-playstation (see README).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

static class PlayStation {
 static string Model(ushort pid) {
  switch(pid) {
   case 0x05C4:case 0x09CC:case 0x0BA0:return "Sony DualShock 4";
   case 0x0CE6:return "Sony DualSense";
   case 0x0DF2:return "Sony DualSense Edge";
   default:return null;
  }
 }
 public static IEnumerable<BatteryState> Poll(IEnumerable<HidInfo> devices) {
  foreach(var info in devices.Where(x=>x.Vid==0x054C&&x.Page==1&&x.Usage==5&&x.Input>=64&&Model(x.Pid)!=null))
   using(var channel=new HidChannel(info,true)) {
    if(!channel.Valid)continue;
    bool bluetooth=(info.Path??"").IndexOf("00001124-",StringComparison.OrdinalIgnoreCase)>=0;
    var timer=Stopwatch.StartNew();
    int remaining;
    while((remaining=250-(int)timer.ElapsedMilliseconds)>0) {
     var report=channel.Read(remaining);
     if(report==null)break;
     var state=Decode(info.Pid,report,bluetooth);
     if(state==null)continue;
     state.Key=Native.HidKey("sony:gamepad",info.Path);
     state.Sample=DateTime.Now;
     yield return state;
     break;
    }
   }
 }
 internal static BatteryState Decode(ushort pid,byte[] report,bool bluetooth) {
  string name=Model(pid);
  if(name==null||report==null||report.Length<64)return null;
  bool dualSense=pid==0x0CE6||pid==0x0DF2;
  bool extended=report[0]==(dualSense?0x31:0x11);
  if(extended?report.Length<78:bluetooth||report[0]!=0x01)return null;
  int offset=dualSense?(extended?54:53):(extended?32:30);
  int value=report[offset],level=value&0x0F;
  if(pid==0x0BA0&&(report[offset+1]&4)!=0)return null;
  bool charging;
  int percent;
  if(dualSense) {
   int status=value>>4;
   if(status>2||level>10&&status!=2)return null;
   charging=status==1;
   percent=status==2?100:Math.Min(level*10+5,100);
  } else {
   bool cable=(value&0x10)!=0;
   if(level>(cable?11:10))return null;
   charging=cable&&level<11;
   percent=level==11?100:cable?level*10:Math.Min(level*10+5,100);
  }
  return new BatteryState{Name=name,Kind="Gamepad",Percent=percent,Charging=charging,Approximate=true,
   Connection=DeviceConnection.Connected,Status="Canlı • USB HID"};
 }
}
