using System;
using System.Collections.Generic;
using System.IO;

static class RegressionChecks {
 public static int Run() {
  var failures=new List<string>();int count=0;
  Action<bool,string> check=(ok,name)=>{count++;if(!ok)failures.Add(name);};
  var now=new DateTime(2026,11,20,23,0,0);
  var last=new BatteryState{Key="mouse",Name="Mouse only",Percent=95,Sample=now.AddHours(-2),Millivolts=4084};
  var waiting=Batteries.PreserveLastReading(new BatteryState{Key="mouse",Name="Mouse only",ReceiverPresent=true},last,now);
  check(waiting.Percent==95&&!waiting.Dimmed,"Idle receiver must retain percentage without dimming");
  check(waiting.Stale&&waiting.Sample==last.Sample,"Cached sample must keep its original timestamp and freshness");
  check(!waiting.Charging,"Cached charge state must not pretend charging is current");
  var fresh=Batteries.PreserveLastReading(new BatteryState{Key="mouse",Percent=90,Sample=now,ReceiverPresent=true},last,now);
  check(fresh.Percent==90&&!fresh.Stale&&fresh.Sample==now,"Fresh battery must replace previous sample");
  var missing=Batteries.PreserveLastReading(new BatteryState{Key="mouse",Name="Mouse only",Status="Alıcı bulunamadı"},new BatteryState{Name="Mouse only",Percent=95,Sample=now.AddMinutes(-1)},now);
  check(missing.Dimmed&&missing.Stale&&missing.Percent==95,"Unplugged receiver must be distinguished from idle radio");
  var absent=Batteries.MergeReadings(new BatteryState[0],new[]{last,new BatteryState{
   Key="headset",Name="Headset",Kind="Kulaklık",Percent=62,Sample=now.AddHours(-2),Charging=true,Status="USB HID"}},now,true);
  check(absent.Count==2&&absent.TrueForAll(x=>x.Dimmed&&x.Stale&&!x.Charging&&x.Connection==DeviceConnection.Disconnected),
   "Disconnected devices keep dimmed readings without an expiry or charging indicator");
  var connected=Batteries.MergeReadings(new[]{new BatteryState{Key="headset",Name="Headset",Percent=64,Connection=DeviceConnection.Connected,Sample=now}},absent,now,true);
  check(connected[0].Percent==64&&!connected[0].Stale&&!connected[0].Dimmed,"A fresh reading replaces the old one on reconnect");
  var offBt=new BatteryState{Key="windows:test",Name="Bluetooth headset",Percent=70,Stale=true,Connection=DeviceConnection.Disconnected,Status="Windows Bluetooth"};
  var cachedBluetooth=Batteries.MergeReadings(new[]{offBt},new[]{new BatteryState{Key=offBt.Key,Percent=68,Sample=now.AddHours(-1),Status="Windows Bluetooth"}},now,true)[0];
  check(cachedBluetooth.Percent==68&&cachedBluetooth.Sample==now.AddHours(-1)&&cachedBluetooth.Dimmed,
   "An offline Bluetooth snapshot does not replace the saved sample or timestamp");
  var hitStates=new List<BatteryState>{new BatteryState{Percent=95},new BatteryState{Percent=95},new BatteryState{Percent=95}};
  check(Widget.HitDevice(0,1,hitStates)==0&&Widget.HitDevice(65,1,hitStates)==0&&Widget.HitDevice(66,1,hitStates)==1&&Widget.HitDevice(132,1,hitStates)==2,"Device boundaries must select separate cells");
  check(Widget.HitDevice(-1,1,hitStates)==-1&&Widget.HitDevice(198,1,hitStates)==-1,"Outside strip must not select a device");
  check(Widget.HitDevice(132,2,hitStates)==1,"Hover hit testing must follow display scaling");
  var chargingHitStates=new List<BatteryState>{new BatteryState{Percent=95},new BatteryState{Percent=100,Charging=true},new BatteryState{Percent=95}};
  check(Widget.StripWidthDip(chargingHitStates)==214&&Widget.HitDevice(147,1,chargingHitStates)==1&&Widget.HitDevice(148,1,chargingHitStates)==2,"Charging indicator must expand its cell instead of colliding with neighbors");
  string tip=Widget.DeviceTooltip(waiting);
  check(tip=="Mouse only\n"+L.BatteryLabel(false)+"%95\n"+L.TimeLabel+"21:00\n"+L.By("USB HID")+"\n"+L.MouseReceiverWaiting,"The cached mouse tooltip reports receiver presence");
  check(Widget.DeviceTooltip(new BatteryState{Key="headset",Name="Headset",Percent=53,Charging=true,Sample=now,Status="G HUB tarafından bildirilen yüzde"})=="Headset\n"+L.BatteryLabel(true)+"%53\n"+L.TimeLabel+"23:00\n"+L.By("G HUB"),"Charging tooltip must replace the localized battery label");
  check(Batteries.VoltagePercent(4102)==100&&Batteries.VoltagePercent(3293)==0&&Batteries.VoltagePercent(3758)==50,"Headset voltage calibration");
  var g735 = new HidInfo { Vid=0x046D, Pid=0x0AD8, Page=0xFF03, Usage=1, Input=271, Output=271 };
  check(LogitechG735.Matches(g735),"G735 recognizes its bidirectional vendor collection");
  g735.Pid=0x0AB5;
  check(!LogitechG735.Matches(g735),"G735 does not send requests to other Logitech models");
  var g735Reply = new byte[]{0xF2,0,0,0x0C,0x1E,0x82,0x40,0,0,0xF2,0,6,0,0,0,0,2,0x0B,0x41,0x3C,0};
  int g735Percent;
  byte g735Charge;
  check(LogitechG735.TryDecodeBattery(LogitechG735.ReplyData(g735Reply,2),out g735Percent,out g735Charge)
        && g735Percent==65 && g735Charge==0,"G735 decodes a captured 65% battery reply");
  g735Reply[17]=0x0D;
  check(LogitechG735.ReplyData(g735Reply,2)==null,"G735 ignores replies addressed to G HUB");
  check(!LogitechG735.TryDecodeBattery(new byte[]{0xFF,0xFF,0},out g735Percent,out g735Charge)
        && g735Percent==-1,"G735 rejects unavailable battery values");
  check(Batteries.IsVxeMouse(new HidInfo{Vid=0x373B,Pid=0x1273,Page=0xFF02,Usage=2,Input=17,Output=17})&&!Batteries.IsVxeMouse(new HidInfo{Vid=0x373B,Pid=0x1273,Page=1,Usage=2}),"VXE R1 Pro Max wired charging interface");
  var settings=new UserSettings();var device=new BatteryState{Key="example",BatterySupported=true};
  check(!absent.Exists(settings.IncludesReading)&&settings.IncludesReading(fresh)&&settings.IncludesReading(waiting),
   "Remember last charge defaults to off and leaves present devices visible");
  settings.RememberLastCharge=true;
  check(absent.TrueForAll(settings.IncludesReading),"Opting in shows cached readings for disconnected devices");
  check(settings.IsShown(device),"Supported devices should be visible by default");
  settings.Devices[device.Key]=false;check(!settings.IsShown(device),"Unchecked device must be hidden");
  settings.Devices[device.Key]=true;check(settings.IsShown(device),"Second toggle must restore device");
  var json=new System.Web.Script.Serialization.JavaScriptSerializer();var restored=json.Deserialize<UserSettings>(json.Serialize(settings));
  check(restored.IsShown(device)&&restored.RememberLastCharge,"Device visibility and memory settings survive serialization");
  check(!json.Deserialize<UserSettings>("{\"ShowWidget\":true}").RememberLastCharge,"Older settings files keep memory off unless explicitly enabled");
  var localNow=DateTime.Now;
  var fromDisk=json.Deserialize<BatteryState>(json.Serialize(new BatteryState{Key="mouse",Name="Mouse only",Percent=95,Sample=localNow.AddMinutes(-1)}));
  var afterRestart=Batteries.MergeReadings(new BatteryState[0],new[]{fromDisk},localNow,false);
  check(afterRestart.Count==1&&afterRestart[0].Percent==95&&afterRestart[0].Dimmed,"A saved sample survives restart and does not appear live before the scan");
  check(WindowsDevices.KindFromClass(0x0504,"Controller")=="Gamepad"&&WindowsDevices.KindFromClass(0x0540,"Keys")=="Klavye"&&WindowsDevices.KindFromClass(0x0580,"Mouse")=="Mouse","Bluetooth device classes");
  check(new BatteryState{Percent=60,Approximate=true}.Level=="60%","Approximate values must display without a tilde marker");
  check(new BatteryState{Percent=60,Approximate=true,LevelText="Orta"}.Level=="Orta","Gamepad's honest coarse level remains allowed");
  check(Autostart.CommandFor(@"C:\Apps\WirelessBattery.exe")=="\"C:\\Apps\\WirelessBattery.exe\"","Autostart command must quote the executable path");
  check(MenuToggle.ShouldOpen(System.Windows.Forms.MouseButtons.Right,false,false)&&!MenuToggle.ShouldOpen(System.Windows.Forms.MouseButtons.Right,true,false)&&!MenuToggle.ShouldOpen(System.Windows.Forms.MouseButtons.Right,false,true),"Second right-click must close rather than reopen the context menu");
  check(!MenuToggle.ShouldOpen(System.Windows.Forms.MouseButtons.Left,false,false),"Left click must never open the menu");
  check(MenuToggle.WasOpen(true,9999)&&MenuToggle.WasOpen(false,50)&&!MenuToggle.WasOpen(false,1000),"A menu auto-closed by the same click still counts as open"); 
  check(Updates.IsNewer("v1.4.1",new Version(1,4,0))&&Updates.IsNewer("1.5",new Version(1,4,9))&&!Updates.IsNewer("v1.4.0",new Version(1,4,0))&&!Updates.IsNewer("v1.3.9",new Version(1,4,0)),"Update version comparison");
  check(!Updates.IsNewer("latest",new Version(1,0,0))&&!Updates.IsNewer(null,new Version(1,0,0))&&!Updates.IsNewer("v2.0.0-beta",new Version(1,0,0)),"Malformed or prerelease tags must not trigger updates");
  check(Updates.IsSafeReleaseUrl("https://github.com/"+Updates.Repo+"/releases/tag/v1.4.0")&&!Updates.IsSafeReleaseUrl("https://evil.example/releases/")&&!Updates.IsSafeReleaseUrl("https://github.com/other/repo/releases/tag/v9"),"Only this repository's release pages may be opened");
  check(Updates.Current.Major>=1&&Updates.Current.Revision<=0,"Current version must come from the assembly");
  check(Widget.TooltipDelayMs>=1000&&Widget.TooltipDelayMs<=1500,"Device details must wait 1-1.5 s so right-click is not blocked");
  check(Batteries.IsVxeMouse(new HidInfo{Vid=0x373B,Pid=0x1290,Page=0xFF02,Usage=2,Input=17,Output=17})&&Batteries.IsVxeMouse(new HidInfo{Vid=0x3554,Pid=0xF505,Page=0xFF02,Usage=2,Input=17,Output=17}),"Other ATK/VXE receivers are tried");
  check(!Batteries.IsVxeMouse(new HidInfo{Vid=0x373B,Pid=0x11CA,Page=0xFF02,Usage=2,Input=17,Output=17})&&!Batteries.IsVxeMouse(new HidInfo{Vid=0x3554,Pid=0xFA09,Page=0xFF02,Usage=2,Input=17,Output=17})&&!Batteries.IsVxeMouse(new HidInfo{Vid=0x373B,Pid=0x1290,Page=0xFF05,Usage=0,Input=8,Output=0}),"Headsets, keyboard receivers and small collections are never sent mouse frames");
  check(Batteries.MouseName(new HidInfo{Vid=0x373B,Pid=0x1273,Name="VXE R1 PRO MAX"})=="VXE R1 / NordicMouse"&&Batteries.MouseName(new HidInfo{Vid=0x373B,Pid=0x1290,Name="ATK X1 SE 2.4G"})=="ATK X1 SE"&&Batteries.MouseName(new HidInfo{Vid=0x373B,Pid=0x1290,Name=""})=="ATK wireless mouse","Mouse names come from the receiver");
  var report=DeviceReport.Build(new List<HidInfo>{new HidInfo{Vid=0x373B,Pid=0x1290,Page=0xFF02,Usage=2,Input=17,Output=17,Name="ATK X1 SE",Path=@"\?\hid#vid_373b&pid_1290#7&secret"}},new List<BatteryState>{new BatteryState{Key="bt",Name="Private Phone",Kind="Kulaklık",Status="Windows Bluetooth",Percent=50}},"1.4.2","Windows");
  check(report.Contains("373B:1290 FF02:0002")&&!report.Contains("secret")&&!report.Contains("Private Phone"),"Device report lists IDs without paths or Bluetooth names");
  check(DeviceReport.Hex(new byte[]{0x01,0xA5,0,0,0})=="01 A5 (+3 x 00)"&&DeviceReport.Hex(new byte[]{0,0})=="00 (+1 x 00)","Captured reports are printed compactly");
  check(DeviceReport.IsListenCandidate(new HidInfo{Vid=0x3634,Pid=0x4155,Page=0xFF02,Usage=2,Input=65,Output=65})&&!DeviceReport.IsListenCandidate(new HidInfo{Vid=0x3634,Pid=0x4155,Page=0x000C,Usage=1,Input=3})&&!DeviceReport.IsListenCandidate(new HidInfo{Vid=0x373B,Pid=0x10C9,Page=0xFF02,Usage=2,Input=17,Output=17}),"Only unsupported vendor channels are listened to");
  var second=new System.Drawing.Rectangle(-1920,78,1920,1080);var menuSize=new System.Drawing.Size(277,254);
  var nearEdge=MenuToggle.MenuOrigin(new System.Drawing.Point(-60,1134),menuSize,second);
  check(nearEdge.X==-277&&nearEdge.Y==1134-254,"Menu near the right edge of a secondary monitor stays on that monitor");
  var leftEdge=MenuToggle.MenuOrigin(new System.Drawing.Point(-1910,1134),menuSize,second);
  check(leftEdge.X==-1910&&leftEdge.X>=second.Left,"Menu never crosses the left edge either");
  var top=MenuToggle.MenuOrigin(new System.Drawing.Point(100,90),menuSize,second);
  check(top.Y==90,"Menu opens below the cursor when there is no room above");
  string testHash=new string('a',64);
  check(Updates.InstallerHash(testHash+"  WirelessBattery-Setup.exe\r\n")==testHash,"Installer hash supports release checksum format");
  bool rejected=false;try{Updates.InstallerHash(testHash+"  Other.exe");}catch(InvalidDataException){rejected=true;}check(rejected,"Wrong checksum filename is rejected");
  rejected=false;try{Updates.InstallerHash(testHash+"  WirelessBattery-Setup.exe\n"+testHash+"  WirelessBattery-Setup.exe");}catch(InvalidDataException){rejected=true;}check(rejected,"Ambiguous checksum entries are rejected");
  check(Updates.IsAssetUrl("https://github.com/"+Updates.Repo+"/releases/download/v1.4.7/WirelessBattery-Setup.exe")&&!Updates.IsAssetUrl("https://github.com.evil.test/"+Updates.Repo+"/releases/download/v1/a.exe")&&!Updates.IsAssetUrl("http://github.com/"+Updates.Repo+"/releases/download/v1/a.exe"),"Installer downloads require the official repository over HTTPS");
  string hashTest=Path.Combine(Program.Root,"hash-test.tmp");try{File.WriteAllBytes(hashTest,new byte[0]);check(Updates.VerifyFile(hashTest,"e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")&&!Updates.VerifyFile(hashTest,testHash),"Corrupted installer bytes fail verification");}finally{File.Delete(hashTest);}
  check(Batteries.MouseName(new HidInfo{Vid=0x373B,Pid=0x10C9,Name="Nearlink Mouse Dongle"})=="ATK X1 SE"&&Batteries.MouseName(new HidInfo{Vid=0x373B,Pid=0x10C9,Name="Other ATK Mouse Dongle"})=="Other ATK Mouse","Confirmed X1 SE receiver alias preserves explicit names");
  check(Batteries.MouseName(new HidInfo{Vid=0x373B,Pid=0x9999,Name="Nearlink Mouse Dongle"})=="Nearlink Mouse"&&Batteries.MouseName(new HidInfo{Vid=0x3554,Pid=0x10C9,Name="Nearlink Mouse Dongle"})=="Nearlink Mouse","Other VID or PID Nearlink receivers must not get the X1 SE name");
  var razer=RazerDevices.Frame(0x80,0x21);check(razer.Length==64&&razer[0]==0x02&&razer[2]==0x60&&razer[6]==0x04&&razer[9]==0x80&&razer[10]==0x21&&razer[62]==0xC7,"Razer BlackShark V3 Pro battery request frame");
  var razerReply=RazerDevices.Frame(0x80,0x21);razerReply[11]=1;razerReply[12]=1;razerReply[13]=73;razerReply[62]=0;for(int i=0;i<62;i++)razerReply[62]^=razerReply[i];
  check(RazerDevices.Parse(razerReply,0x21)==73&&RazerDevices.Parse(razerReply,0x2A)<0,"Razer replies require the matching command and checksum");
  check(HyperXDevices.Parse(new byte[]{0x21,0xBB,0x0B,73},0x0B)==73&&HyperXDevices.Parse(new byte[]{0x21,0xBB,0x0C,1},0x0C)==1&&HyperXDevices.Parse(new byte[]{0x20,0xBB,0x0B,73},0x0B)<0,"HyperX Cloud Alpha Wireless replies");
  var cloudIII=new HidInfo{Vid=0x03F0,Pid=0x05B7,Page=0xFF13,Usage=1,Input=62,Output=62};
  check(HyperXDevices.IsCloudIIIChannel(cloudIII),"Cloud III recognizes the wireless receiver's vendor collection");
  cloudIII.Pid=0x06B7;
  check(HyperXDevices.IsCloudIIIChannel(cloudIII),"Cloud III recognizes the charging cable's vendor collection");
  cloudIII.Page=0x000B;
  check(!HyperXDevices.IsCloudIIIChannel(cloudIII),"Cloud III does not probe the telephony collection");
  check(HyperXDevices.ParseCloudIII(new byte[]{0x66,0x0D,0x0E,0xD7,48},0x89)==48
   &&HyperXDevices.ParseCloudIII(new byte[]{0x66,0x89,0x0E,0xD7,100},0x89)==100
   &&HyperXDevices.ParseCloudIII(new byte[]{0x66,0x89,0,0,0},0x89)<0
   &&HyperXDevices.ParseCloudIII(new byte[]{0x66,0x89,0x0E,0xD7,101},0x89)<0,"Cloud III validates both battery reply IDs and rejects absent or invalid readings");
  check(HyperXDevices.ParseCloudIII(new byte[]{0x66,0x0C,1},0x8A)==1
   &&HyperXDevices.ParseCloudIII(new byte[]{0x66,0x8A,2},0x8A)==2
   &&HyperXDevices.ParseCloudIII(new byte[]{0x66,0x0D,1},0x8A)<0,"Cloud III distinguishes charging, full and unrelated reports");
  var teevo=new HidInfo{Vid=0x3554,Pid=0xF520,Name="Teevo Terra Pro",Page=0xFF02,Usage=2,Input=17,Output=17};
  var other=new HidInfo{Vid=0x3554,Pid=0xF505,Name="Other Mouse",Page=0xFF02,Usage=2,Input=17,Output=17};
  check(Batteries.FindKnownMouseReceiver(new[]{other,teevo},"Teevo Terra Pro")==teevo
   &&Batteries.FindKnownMouseReceiver(new[]{other},"Teevo Terra Pro")==null
   &&Batteries.FindKnownMouseReceiver(new[]{teevo},null)==null,"A silent receiver is retained only for the mouse that previously provided a reading");
  var wrongMouse=Batteries.PreserveLastReading(new BatteryState{Key="mouse",Name="Other Mouse",ReceiverPresent=true},last,now);
  check(wrongMouse.Percent<0,"A different mouse must not inherit a saved battery level");
  File.WriteAllText(Path.Combine(Program.Root,"test-results.txt"),count+" checks, "+failures.Count+" failures\n"+string.Join("\n",failures));
  return failures.Count==0?0:1;
 }
}
