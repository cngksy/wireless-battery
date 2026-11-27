// GPL-3.0-or-later. The F75 Max receiver battery protocol is referenced in README.md.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

static class AulaDevices {
 const int ReportLength=33;
 const byte BatteryCommand=0x20,BatterySubcommand=0x01;

 internal static bool IsF75MaxBatteryChannel(HidInfo info) {
  return info.Vid==0x05AC&&info.Pid==0x024F&&info.Page==0xFF60&&info.Usage==0x0061
   &&info.Input==ReportLength&&info.Output==ReportLength;
 }

 internal static byte[] F75MaxBatteryRequest() {
  // Windows includes the report ID (zero) before the 32-byte payload.
  var report=new byte[ReportLength];
  report[1]=BatteryCommand;
  report[2]=BatterySubcommand;
  report[ReportLength-1]=(byte)(BatteryCommand+BatterySubcommand);
  return report;
 }

 internal static int F75MaxBatteryPercent(byte[] report) {
  if(report==null||report.Length<5||report[0]!=0||report[1]!=BatteryCommand||report[2]!=BatterySubcommand)return -1;
  return report[4]>0&&report[4]<=100?report[4]:-1;
 }

 public static IEnumerable<BatteryState> Poll(IEnumerable<HidInfo> devices) {
  foreach(var info in devices.Where(IsF75MaxBatteryChannel)) {
   using(var channel=new HidChannel(info)) {
    if(!channel.Valid)continue;
    Native.HidD_FlushQueue(channel.Handle);
    if(!channel.Send(F75MaxBatteryRequest(),350))continue;
    var watch=Stopwatch.StartNew();
    int remaining;
    while((remaining=500-(int)watch.ElapsedMilliseconds)>0) {
     var report=channel.Read(remaining);
     if(report==null)break;
     int percent=F75MaxBatteryPercent(report);
     if(percent<0)continue;
     yield return new BatteryState {
      Key=Native.HidKey("aula:f75-max",info.Path),Name="AULA F75 Max",Kind="Klavye",
      Percent=percent,Connection=DeviceConnection.Connected,Sample=DateTime.Now,Status="Canlı • USB HID"
     };
     break;
    }
   }
  }
 }
}
