// GPL-3.0-or-later. ASUS battery and receiver protocol references are in README.md.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

static class AsusRog {
 const ushort Vendor=0x0B05,HarpeWired=0x1A92,HarpePaired=0x1A94,OmniReceiver=0x1ACE;
 const byte PairListReport=0x01,PairListCommand=0xA0,OmniMouseReport=0x03;
 const byte BatteryCommand=0x12,BatterySubcommand=0x07;

 internal static bool IsHarpeWiredChannel(HidInfo info) {
  return info.Vid==Vendor&&info.Pid==HarpeWired&&info.Page==0xFF01&&info.Usage==1
   &&info.Input==65&&info.Output==65;
 }

 static string OmniInstance(HidInfo info,ushort page,string collection) {
  if(info.Vid!=Vendor||info.Pid!=OmniReceiver||info.Page!=page||info.Input!=64||info.Output!=64||string.IsNullOrEmpty(info.Path))return null;
  var parts=info.Path.Split('#');
  if(parts.Length<3||!parts[1].EndsWith("&mi_02&"+collection,StringComparison.OrdinalIgnoreCase))return null;
  // The HID collection adds a suffix to the shared USB interface instance.
  int separator=parts[2].LastIndexOf('&');
  return separator>0?parts[2].Substring(0,separator):null;
 }

 internal static bool SameOmniReceiver(HidInfo pairList,HidInfo mouse) {
  string instance=OmniInstance(pairList,0xFF02,"col01");
  return instance!=null&&string.Equals(instance,OmniInstance(mouse,0xFF01,"col03"),StringComparison.OrdinalIgnoreCase);
 }

 internal static bool OmniHasHarpe(byte[] report) {
  if(report==null||report.Length<9||report[0]!=PairListReport||report[1]!=PairListCommand||report[2]!=0)return false;
  int slotCount=report[3];
  for(int slot=0,offset=5;slot<slotCount&&offset+3<report.Length;slot++,offset+=4) {
   int pid=report[offset]|(report[offset+1]<<8);
   if(pid==0)break;
   if(pid==HarpePaired&&report[offset+2]==OmniMouseReport)return true;
  }
  return false;
 }

 internal static int HarpeBatteryPercent(byte[] report,byte reportId,out bool charging) {
  charging=false;
  if(report==null||report.Length<11||report[0]!=reportId||report[1]!=BatteryCommand||report[2]!=BatterySubcommand||report[5]>100)return -1;
  charging=report[10]!=0;
  return report[5]>0||charging?report[5]:-1;
 }

 static byte[] Query(HidInfo info,byte[] request,Func<byte[],bool> validReply) {
  using(var channel=new HidChannel(info)) {
   if(!channel.Valid)return null;
   Native.HidD_FlushQueue(channel.Handle);
   if(!channel.Send(request,350))return null;
   var watch=Stopwatch.StartNew();
   int remaining;
   while((remaining=650-(int)watch.ElapsedMilliseconds)>0) {
    var report=channel.Read(remaining);
    if(report==null)break;
    if(validReply(report))return report;
   }
  }
  return null;
 }

 static BatteryState ReadHarpe(HidInfo info,byte reportId) {
  bool charging;
  var report=Query(info,new[]{reportId,BatteryCommand,BatterySubcommand},reply=>{
   bool replyCharging;
   return HarpeBatteryPercent(reply,reportId,out replyCharging)>=0;
  });
  int percent=HarpeBatteryPercent(report,reportId,out charging);
  if(percent<0)return null;
  return new BatteryState{Key="asus:harpe-ace-aim-lab",Name="ASUS ROG Harpe Ace Aim Lab Edition",Kind="Mouse",
   Percent=percent,Charging=charging,Connection=DeviceConnection.Connected,Sample=DateTime.Now,Status="Canlı • USB HID"};
 }

 public static BatteryState ReadHarpe(IEnumerable<HidInfo> devices) {
  foreach(var info in devices.Where(IsHarpeWiredChannel)) {
   var reading=ReadHarpe(info,0);
   if(reading!=null)return reading;
  }
  foreach(var pairList in devices.Where(x=>OmniInstance(x,0xFF02,"col01")!=null)) {
   var mouse=devices.FirstOrDefault(x=>SameOmniReceiver(pairList,x));
   if(mouse==null||Query(pairList,new byte[]{PairListReport,PairListCommand,0,0},OmniHasHarpe)==null)continue;
   var reading=ReadHarpe(mouse,OmniMouseReport);
   if(reading!=null)return reading;
  }
  return null;
 }
}
