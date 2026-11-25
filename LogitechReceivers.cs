// GPL-3.0-or-later. HID++ transport and features: Solaar (see README).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

static class LogitechReceivers {
 const byte SoftwareId=0x0E;
 static readonly int[] BatteryFeatures={0x1004,0x1000};

 internal static int SlotCount(HidInfo info) {
  if(info.Vid!=0x046D||info.Page!=0xFF00||info.Usage!=2||info.Input!=20||info.Output!=20)return 0;
  switch(info.Pid) {
   case 0xC52B:case 0xC532:case 0xC548:return 6; // Unifying and Bolt
   case 0xC52E:case 0xC52F:case 0xC531:case 0xC537:case 0xC539:
   case 0xC53A:case 0xC53D:case 0xC541:case 0xC545:case 0xC547:return 1;
   default:return 0; // Existing dedicated G305 and headset routes stay separate.
  }
 }

 public static IEnumerable<BatteryState> Poll(IEnumerable<HidInfo> devices) {
  foreach(var info in devices.Where(d=>SlotCount(d)>0))using(var channel=new HidChannel(info)) {
   if(!channel.Valid)continue;
   var clock=Stopwatch.StartNew();
   int slots=SlotCount(info);
   for(byte slot=1;slot<=slots&&clock.ElapsedMilliseconds<2400;slot++) {
    var state=ReadSlot(channel,slot,clock);
    if(state!=null)yield return state;
   }
  }
 }

 static BatteryState ReadSlot(HidChannel channel,byte slot,Stopwatch clock) {
  BatteryState state=null;
  foreach(int feature in BatteryFeatures) {
   byte index=FindFeature(channel,slot,feature,clock);
   if(index==0||index==0xFF)continue;
   state=ParseBattery(Call(channel,slot,index,feature==0x1004?0x10:0x00,clock),feature);
   if(state!=null)break;
  }
  if(state==null)return null;

  byte nameFeature=FindFeature(channel,slot,0x0005,clock);
  string name=null;
  string kind=null;
  if(nameFeature>0&&nameFeature<0xFF) {
   name=ReadName(channel,slot,nameFeature,clock);
   var type=Call(channel,slot,nameFeature,0x20,clock);
   if(type!=null) {
    if(type[0]==0||type[0]==2)kind="Klavye";
    else if(type[0]>=3&&type[0]<=5)kind="Mouse";
   }
  }
  name=string.IsNullOrWhiteSpace(name)?"Logitech "+channel.Info.Pid.ToString("X4")+" #"+slot:name;
  if(!name.StartsWith("Logitech ",StringComparison.OrdinalIgnoreCase))name="Logitech "+name;
  state.Name=name;
  state.Kind=kind??WindowsDevices.KindFromClass(0,name);
  state.Key=Native.HidKey("logitech:hidpp",channel.Info.Path)+":"+slot;
  state.Status="Canlı • HID++";
  state.Sample=DateTime.Now;
  state.Connection=DeviceConnection.Connected;
  return state;
 }

 static string ReadName(HidChannel channel,byte slot,byte feature,Stopwatch clock) {
  var length=Call(channel,slot,feature,0x00,clock);
  if(length==null||length[0]==0||length[0]>64)return null;
  var name=new List<byte>(length[0]);
  while(name.Count<length[0]) {
   var part=Call(channel,slot,feature,0x10,clock,(byte)name.Count);
   if(part==null||part.Length==0)return null;
   name.AddRange(part.Take(length[0]-name.Count));
  }
  string result=Encoding.UTF8.GetString(name.ToArray()).Trim();
  return result.Any(char.IsControl)?null:result;
 }

 static byte FindFeature(HidChannel channel,byte slot,int id,Stopwatch clock) {
  var response=Call(channel,slot,0,0,clock,(byte)(id>>8),(byte)id);
  return response==null?(byte)0:response[0];
 }
 static byte[] Call(HidChannel channel,byte slot,byte feature,int function,Stopwatch clock,params byte[] args) {
  int remaining=2400-(int)clock.ElapsedMilliseconds;
  return remaining<=0?null:Batteries.LogiRequest(channel,Math.Min(remaining,250),slot,feature,(byte)(function|SoftwareId),args);
 }

 internal static BatteryState ParseBattery(byte[] response,int feature) {
  if(response==null||response.Length<(feature==0x1004?4:3)||response[0]>100)return null;
  int status=response[2];
  if(status>4)return null;
  bool charging=status==1||status==2||status==4;
  if(response[0]>0)return new BatteryState{Percent=response[0],Charging=charging};
  if(status==3)return new BatteryState{Percent=100};
  if(feature!=0x1004)return null;
  switch(response[1]) {
   case 1:return new BatteryState{Percent=5,LevelText=L.BatteryLevel(0),Approximate=true,Charging=charging};
   case 2:return new BatteryState{Percent=15,LevelText=L.BatteryLevel(1),Approximate=true,Charging=charging};
   case 4:return new BatteryState{Percent=60,LevelText=L.BatteryLevel(2),Approximate=true,Charging=charging};
   case 8:return new BatteryState{Percent=100,LevelText=L.BatteryLevel(3),Approximate=true,Charging=charging};
   default:return null;
  }
 }
}
