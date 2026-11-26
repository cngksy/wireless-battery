// GPL-3.0-or-later. Read-only battery protocol references are in README.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

static class IpiPiao {
 const byte BatteryCommand=0x4F,BatterySubcommand=0x81;
 [DllImport("hid.dll")] static extern bool HidD_SetFeature(SafeFileHandle handle,byte[] report,int length);
 [DllImport("hid.dll")] static extern bool HidD_GetFeature(SafeFileHandle handle,[In,Out] byte[] report,int length);
 internal static bool Matches(HidInfo info) {
  return info.Vid==0x372E&&(info.Pid==0x1014||info.Pid==0x1015)
   &&info.Page==0xFF00&&info.Usage==1&&info.Feature==64;
 }
 internal static byte[] Request(byte variant=0) {
  var frame=new byte[64];
  frame[0]=3;frame[2]=0x50;frame[4]=variant;frame[5]=BatteryCommand;frame[6]=BatterySubcommand;
  frame[1]=unchecked((byte)(frame[2]+frame[4]+frame[5]+frame[6]));
  return frame;
 }
 internal static int Decode(byte[] reply,byte variant=0) {
  if(reply==null||reply.Length<8||reply[0]!=3||reply[1]!=0x50||reply[2]!=0||reply[3]==0||reply[4]!=variant)return -1;
  int status;
  if(reply[5]==BatteryCommand&&reply[6]==BatterySubcommand)status=reply[7];
  else if(reply[5]==BatterySubcommand||reply[5]==(BatterySubcommand&0x7F))status=reply[6];
  else return -1;
  return (status&0x7F)<=100?status:-1;
 }
 static int Query(SafeFileHandle handle,byte variant) {
  var request=Request(variant);
  if(!HidD_SetFeature(handle,request,request.Length))return -1;
  for(int i=0;i<4;i++) {
   Thread.Sleep(120);
   var response=new byte[64];response[0]=3;
   if(!HidD_GetFeature(handle,response,response.Length))continue;
   int status=Decode(response,variant);
   if(status>=0)return status;
  }
  return -1;
 }
 public static BatteryState Read(IEnumerable<HidInfo> devices) {
  foreach(var info in devices.Where(Matches).OrderByDescending(x=>x.Pid==0x1015)) {
   using(var handle=Native.CreateFile(info.Path,0,3,IntPtr.Zero,3,0,IntPtr.Zero)) {
    if(handle.IsInvalid)continue;
    byte variant=info.Pid==0x1015?(byte)2:(byte)0;
    int status=Query(handle,variant);
    if(status<0)status=Query(handle,(byte)(2-variant));
    if(status<0)continue;
    return new BatteryState{Key="ipi:piao",Name="IPI PIAO / Float 88",Kind="Mouse",
     Percent=status&0x7F,Charging=(status&0x80)!=0,Connection=DeviceConnection.Connected,
     Sample=DateTime.Now,Status="Canlı • USB HID"};
   }
  }
  return null;
 }
}
