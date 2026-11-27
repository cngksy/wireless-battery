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
  var redmi=new BatteryState{Key="windows:redmi-profile",Name="REDMI Buds 8 Pro Hands-Free AG",Kind="Kulaklık",
   Percent=100,Stale=true,Connection=DeviceConnection.Disconnected};
  var bluetoothProfiles=new List<BatteryState>{redmi};
  WindowsRuntimeBattery.MergeConnectedBluetooth(bluetoothProfiles,"windows:redmi-endpoint","REDMI Buds 8 Pro",0,-1,now);
  check(bluetoothProfiles.Count==1&&bluetoothProfiles[0].Key==redmi.Key&&bluetoothProfiles[0].Percent==100
   &&bluetoothProfiles[0].Sample==now&&new UserSettings().IncludesReading(bluetoothProfiles[0]),
   "A connected Bluetooth endpoint can confirm a battery reading from its hands-free profile");
  WindowsRuntimeBattery.MergeConnectedBluetooth(bluetoothProfiles,"windows:no-battery","Another headset",0,-1,now);
  check(bluetoothProfiles.Count==1,"A connected endpoint without a battery reading does not add an empty indicator");
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
  check(DeviceReport.IsListenCandidate(new HidInfo{Vid=0x040B,Pid=0x0897,Page=0xF100,Usage=0,Input=9,Output=65})
   &&!DeviceReport.IsListenCandidate(new HidInfo{Vid=0x040B,Pid=0x0897,Page=0x000C,Usage=1,Input=3}),"Fuxi H3 diagnostics can listen to the documented vendor channel");
  var aula=new HidInfo{Vid=0x05AC,Pid=0x024F,Page=0xFF60,Usage=0x0061,Input=33,Output=33};
  check(AulaDevices.IsF75MaxBatteryChannel(aula),"F75 Max targets the reported 33-byte Windows HID collection");
  aula.Page=0xFF59;
  check(!AulaDevices.IsF75MaxBatteryChannel(aula),"F75 Max does not send battery queries on the other vendor collection");
  var aulaRequest=AulaDevices.F75MaxBatteryRequest();
  check(aulaRequest.Length==33&&aulaRequest[0]==0&&aulaRequest[1]==0x20&&aulaRequest[2]==1&&aulaRequest[32]==0x21,
   "F75 Max battery query includes the Windows report ID and payload checksum");
  check(AulaDevices.F75MaxBatteryPercent(new byte[]{0,0x20,1,0,83})==83
   &&AulaDevices.F75MaxBatteryPercent(new byte[]{0,0x20,1,0,0})<0
   &&AulaDevices.F75MaxBatteryPercent(new byte[]{0,0x20,1,0,101})<0
   &&AulaDevices.F75MaxBatteryPercent(new byte[]{0,0x20,2,0,83})<0
   &&AulaDevices.F75MaxBatteryPercent(aulaRequest)<0,"F75 Max accepts only a valid percentage response, never a request echo");
  var harpeCable=new HidInfo{Vid=0x0B05,Pid=0x1A92,Page=0xFF01,Usage=1,Input=65,Output=65};
  check(AsusRog.IsHarpeWiredChannel(harpeCable),"Harpe Ace matches the cable's battery collection");
  harpeCable.Pid=0x19FC;
  check(!AsusRog.IsHarpeWiredChannel(harpeCable),"The ASUS keyboard is never treated as a Harpe Ace");
  var omniPair=new HidInfo{Vid=0x0B05,Pid=0x1ACE,Page=0xFF02,Usage=1,Input=64,Output=64,
   Path=@"\\?\hid#vid_0b05&pid_1ace&mi_02&col01#7&receiver&0&0000#{hid}"};
  var omniMouse=new HidInfo{Vid=0x0B05,Pid=0x1ACE,Page=0xFF01,Usage=1,Input=64,Output=64,
   Path=@"\\?\hid#vid_0b05&pid_1ace&mi_02&col03#7&receiver&0&0002#{hid}"};
  check(AsusRog.SameOmniReceiver(omniPair,omniMouse),"OMNI uses the matching receiver's mouse collection from the report");
  omniMouse.Path=omniMouse.Path.Replace("receiver","other-receiver");
  check(!AsusRog.SameOmniReceiver(omniPair,omniMouse),"Pairing data never crosses between separate OMNI receivers");
  check(AsusRog.OmniHasHarpe(new byte[]{1,0xA0,0,2,0,0x85,0x1A,2,0,0x94,0x1A,3,5})
   &&!AsusRog.OmniHasHarpe(new byte[]{1,0xA0,0,1,0,0x65,0x1B,3,5})
   &&!AsusRog.OmniHasHarpe(new byte[]{1,0xA0,0,0,0,0x94,0x1A,3,5})
   &&!AsusRog.OmniHasHarpe(new byte[]{1,0xA0,0,1,0,0x94,0x1A,2,5})
   &&!AsusRog.OmniHasHarpe(new byte[]{1,0xA1,0,1,0,0x94,0x1A,3,5}),
   "OMNI requires the Harpe Aim Lab model in the valid pair list");
  bool harpeCharging;
  check(AsusRog.HarpeBatteryPercent(new byte[]{0,0x12,0x07,0,0,54,2,20,0,0,1},0,out harpeCharging)==54&&harpeCharging
   &&AsusRog.HarpeBatteryPercent(new byte[]{3,0x12,0x07,0,0,53,2,20,0,0,0},3,out harpeCharging)==53&&!harpeCharging,
   "Harpe Ace uses the right report ID and charging flag in both connection modes");
  check(AsusRog.HarpeBatteryPercent(new byte[]{3,0x12,0x07,0,0,0,0,0,0,0,0},3,out harpeCharging)<0
   &&AsusRog.HarpeBatteryPercent(new byte[]{3,0x12,0x07,0,0,0xFF,0xAA,0,0,0,0},3,out harpeCharging)<0
   &&AsusRog.HarpeBatteryPercent(new byte[]{5,0x12,0x01,0,0,2,0,0,0,0,0},3,out harpeCharging)<0,
   "Harpe Ace ignores standby, error replies and unsolicited events from the device report");
  var second=new System.Drawing.Rectangle(-1920,78,1920,1080);var menuSize=new System.Drawing.Size(277,254);
  string display1=@"\\.\DISPLAY1",display2=@"\\.\DISPLAY2";
  check(Taskbars.DisplayNumber(display2)==2,"Monitor label uses the Windows display number");
  string display3=@"\\.\DISPLAY3";
  check(Taskbars.IsDisplayOptionChecked(null,null)&&!Taskbars.IsDisplayOptionChecked(null,display2)
   &&Taskbars.ShowsOnDisplay(null,display2),"All displays shows every indicator but only checks the all-displays item");
  var selectedDisplays=Taskbars.ToggleDisplay(null,display2);
  check(Taskbars.IsDisplayOptionChecked(selectedDisplays,display2)&&!Taskbars.IsDisplayOptionChecked(selectedDisplays,null)
   &&!Taskbars.ShowsOnDisplay(selectedDisplays,display1),"Selecting a display from all-displays mode shows only that display");
  var twoDisplays=Taskbars.ToggleDisplay(selectedDisplays,display3);
  check(Taskbars.ShowsOnDisplay(twoDisplays,display2)&&Taskbars.ShowsOnDisplay(twoDisplays,display3)
   &&!Taskbars.ShowsOnDisplay(twoDisplays,display1),"Selecting another display keeps the earlier selection");
  settings.TaskbarDisplays=Taskbars.ToggleDisplay(twoDisplays,display3);
  var restoredDisplays=json.Deserialize<UserSettings>(json.Serialize(settings)).TaskbarDisplays;
  check(restoredDisplays.Length==1&&Taskbars.ShowsOnDisplay(restoredDisplays,display2)
   &&json.Deserialize<UserSettings>("{\"ShowWidget\":true}").TaskbarDisplays==null,
   "Custom selections survive a restart and older settings keep all displays");
  check(!Taskbars.ShowsOnDisplay(Taskbars.ToggleDisplay(restoredDisplays,display2),display2),
   "An empty selection hides all indicators");
  var nearEdge=MenuToggle.MenuOrigin(new System.Drawing.Point(-60,1134),menuSize,second);
  check(nearEdge.X==-277&&nearEdge.Y==1134-254,"Menu near the right edge of a secondary monitor stays on that monitor");
  var leftEdge=MenuToggle.MenuOrigin(new System.Drawing.Point(-1910,1134),menuSize,second);
  check(leftEdge.X==-1910&&leftEdge.X>=second.Left,"Menu never crosses the left edge either");
  var top=MenuToggle.MenuOrigin(new System.Drawing.Point(100,90),menuSize,second);
  check(top.Y==90,"Menu opens below the cursor when there is no room above");
  check(MenuToggle.SubmenuDirection(new System.Drawing.Rectangle(-277,930,275,22),132,second)==System.Windows.Forms.ToolStripDropDownDirection.Left
   &&MenuToggle.SubmenuDirection(new System.Drawing.Rectangle(-1700,930,275,22),132,second)==System.Windows.Forms.ToolStripDropDownDirection.Right,
   "The display submenu opens away from the nearest monitor edge when space is limited");
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
  var alpha2=new HidInfo{Vid=0x03F0,Pid=0x08BE,Page=0xFF13,Usage=0xFF00,Input=64,Output=64};
  check(HyperXDevices.IsCloudAlpha2Channel(alpha2),"Cloud Alpha 2 recognizes the station's battery collection");
  alpha2.Usage=1;
  check(!HyperXDevices.IsCloudAlpha2Channel(alpha2),"Cloud Alpha 2 skips the station's other vendor collection");
  alpha2.Usage=0xFF00;alpha2.Pid=0x0ABE;
  check(!HyperXDevices.IsCloudAlpha2Channel(alpha2),"Cloud Alpha 2 skips the Chat USB device");
  bool alpha2Charging;
  check(HyperXDevices.ParseCloudAlpha2(new byte[]{0x51,0x02,51,0,0x1A,0,0},out alpha2Charging)==51&&!alpha2Charging
   &&HyperXDevices.ParseCloudAlpha2(new byte[]{0x51,0x02,67,0,0x1A,0,0x80},out alpha2Charging)==67&&alpha2Charging,
   "Cloud Alpha 2 reads the exact percentage and the charging flag");
  check(HyperXDevices.ParseCloudAlpha2(new byte[]{0x51,0x02,101,0,0,0,0},out alpha2Charging)<0
   &&HyperXDevices.ParseCloudAlpha2(new byte[]{0x51,0x02,51},out alpha2Charging)<0
   &&HyperXDevices.ParseCloudAlpha2(new byte[]{0x61,0x02,90,0,0,0,0},out alpha2Charging)<0,
   "Cloud Alpha 2 rejects invalid, truncated and unrelated replies");
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
  var g305=new HidInfo{Vid=0x046D,Pid=0xC53F,Page=0xFF00,Usage=2,Input=20,Output=20};
  check(Batteries.IsG305BatteryChannel(g305)&&Batteries.ParseG305Battery(new byte[]{57,20,0})==57
   &&Batteries.ParseG305Battery(new byte[]{0,0,0})<0,"G305 recognizes the long HID++ channel and only accepts a reported percentage");
  g305.Usage=4;
  check(!Batteries.IsG305BatteryChannel(g305),"G305 does not probe other vendor collections");
  var ultimate2=new HidInfo{Vid=0x2DC8,Pid=0x310B,Page=0xFF7A,Usage=1,Input=64,Output=64,
   Name="8BitDo Ultimate 2 Wireless Controller for PC"};
  check(EightBitDo.IsUltimate2BatteryChannel(ultimate2),"Ultimate 2 Wireless recognizes the reported vendor collection");
  ultimate2.Pid=0x310A;
  check(!EightBitDo.IsUltimate2BatteryChannel(ultimate2),"Ultimate 2 battery queries are not sent to the Ultimate 2C");
  var ultimate2Reply=new byte[64];
  Array.Copy(new byte[]{0x02,0x04,0x04,0x00,0x21,0x01},ultimate2Reply,6);
  ultimate2Reply[18]=0x80|52;
  check(EightBitDo.ParseUltimate2Battery(ultimate2Reply)==(0x80|52),"Ultimate 2 preserves the charging flag in a valid battery report");
  ultimate2Reply[4]=0x20;
  check(EightBitDo.ParseUltimate2Battery(ultimate2Reply)<0,"Ultimate 2 rejects a reply to another command");
  var teevo=new HidInfo{Vid=0x3554,Pid=0xF520,Name="Teevo Terra Pro",Page=0xFF02,Usage=2,Input=17,Output=17};
  var other=new HidInfo{Vid=0x3554,Pid=0xF505,Name="Other Mouse",Page=0xFF02,Usage=2,Input=17,Output=17};
  check(Batteries.FindKnownMouseReceiver(new[]{other,teevo},"Teevo Terra Pro")==teevo
   &&Batteries.FindKnownMouseReceiver(new[]{other},"Teevo Terra Pro")==null
   &&Batteries.FindKnownMouseReceiver(new[]{teevo},null)==null,"A silent receiver is retained only for the mouse that previously provided a reading");
  var wrongMouse=Batteries.PreserveLastReading(new BatteryState{Key="mouse",Name="Other Mouse",ReceiverPresent=true},last,now);
  check(wrongMouse.Percent<0,"A different mouse must not inherit a saved battery level");
  var unifying=new HidInfo{Vid=0x046D,Pid=0xC52B,Page=0xFF00,Usage=2,Input=20,Output=20};
  check(LogitechReceivers.SlotCount(unifying)==6,"HID++ discovers Unifying slots by receiver class");
  unifying.Pid=0xC548;
  check(LogitechReceivers.SlotCount(unifying)==6,"HID++ discovers Bolt slots by receiver class");
  unifying.Pid=0xC53F;
  check(LogitechReceivers.SlotCount(unifying)==0,"The dedicated G305 route is not probed twice");
  var superstrikeReceiver=new HidInfo{Vid=0x046D,Pid=0xC54D,Page=0xFF00,Usage=2,Input=20,Output=20};
  var superstrikeShortInterface=new HidInfo{Vid=0x046D,Pid=0xC54D,Page=0xFF00,Usage=1,Input=7,Output=7};
  check(LogitechReceivers.SlotCount(superstrikeReceiver)==1&&LogitechReceivers.SlotCount(superstrikeShortInterface)==0,
   "PRO X2 SUPERSTRIKE polls slot 1 on the LIGHTSPEED receiver's long HID++ interface");
  var ghubMouse=new BatteryState{Key="ghub:g502",Name="G502 X PLUS Wireless Gaming Mouse",Kind="Mouse",Percent=100,Sample=now};
  var ghubKeys=new BatteryState{Key="ghub:g515",Name="G515 LIGHTSPEED TKL Gaming Keyboard",Kind="Klavye",Percent=94,Sample=now};
  var hidMouse=new BatteryState{Key="logitech:hidpp:mouse:1",Name="Logitech G502 X PLUS",Kind="Mouse",Percent=99,Sample=now};
  var hidKeys=new BatteryState{Key="logitech:hidpp:keyboard:1",Name="Logitech G515 LS TKL",Kind="Klavye",Percent=93,Sample=now};
  var hidOther=new BatteryState{Key="logitech:hidpp:other:1",Name="Logitech G502 X LIGHTSPEED",Kind="Mouse",Percent=70,Sample=now};
  var logitechResults=new List<BatteryState>{ghubMouse,ghubKeys};
  Batteries.AddLogitechReceiverReadings(logitechResults,new[]{ghubMouse,ghubKeys},new[]{hidMouse,hidKeys,hidOther});
  check(logitechResults.Count==3&&logitechResults[0]==ghubMouse&&logitechResults[1]==ghubKeys&&logitechResults[2]==hidOther,
   "G HUB and HID++ share one indicator per unambiguously matching model and preserve distinct models");
  check(Batteries.MergeReadings(logitechResults,new[]{hidMouse,hidKeys},now,true).Count==3,
   "Remembered HID++ duplicates do not reappear when G HUB supplies the live reading");
  var logitechFallback=new List<BatteryState>();
  Batteries.AddLogitechReceiverReadings(logitechFallback,new BatteryState[0],new[]{hidMouse,hidKeys});
  check(logitechFallback.Count==2,"The receiver still supplies both models without G HUB");
  var offlineGHub=new BatteryState{Key=ghubMouse.Key,Name=ghubMouse.Name,Kind=ghubMouse.Kind,Stale=true,Connection=DeviceConnection.Disconnected};
  var logitechOffline=new List<BatteryState>{offlineGHub};
  Batteries.AddLogitechReceiverReadings(logitechOffline,new[]{offlineGHub},new[]{hidMouse});
  check(logitechOffline.Count==1&&logitechOffline[0]==hidMouse,"A live HID++ reading replaces a matching offline G HUB entry");
  var superstrike=LogitechReceivers.ParseBattery(new byte[]{53,0,1,0},0x1004);
  check(superstrike!=null&&superstrike.Percent==53&&superstrike.Charging&&!superstrike.Approximate,
   "PRO X2 SUPERSTRIKE reports an exact percentage and charging over Unified Battery");
  var nativeSuperstrike=new BatteryState{Key="logitech:hidpp:superstrike:1",Name="Logitech PRO X2 SUPERSTRIKE",Kind="Mouse",Percent=53,Sample=now};
  var ghubSuperstrike=new BatteryState{Key="ghub:superstrike",Name="PRO X 2 SUPERSTRIKE Wireless Gaming Mouse",Kind="Mouse",Percent=53,Sample=now};
  var superstrikeResults=new List<BatteryState>{ghubSuperstrike};
  Batteries.AddLogitechReceiverReadings(superstrikeResults,new[]{ghubSuperstrike},new[]{nativeSuperstrike});
  check(superstrikeResults.Count==1&&superstrikeResults[0]==ghubSuperstrike,
   "PRO X2 SUPERSTRIKE appears once when G HUB and the receiver both report it");
  var unified=LogitechReceivers.ParseBattery(new byte[]{0,2,1,0},0x1004);
  check(unified!=null&&unified.Level==L.BatteryLevel(1)&&unified.Charging
   &&Widget.DeviceTooltip(unified).Contains(L.BatteryLabel(true)+L.BatteryLevel(1)),
   "Unified battery levels are displayed as levels, including in the tooltip");
  var hidppPercent=LogitechReceivers.ParseBattery(new byte[]{67,0,0},0x1000);
  check(hidppPercent!=null&&hidppPercent.Percent==67&&!hidppPercent.Charging
   &&LogitechReceivers.ParseBattery(new byte[]{67,0,5},0x1000)==null
   &&LogitechReceivers.ParseBattery(new byte[]{0,0,0},0x1004)==null,"HID++ ignores unavailable and invalid reports");
  var dualSenseReport=new byte[64];dualSenseReport[0]=0x01;dualSenseReport[53]=0x16;
  var dualSense=PlayStation.Decode(0x0CE6,dualSenseReport,false);
  check(dualSense!=null&&dualSense.Percent==65&&dualSense.Charging&&dualSense.Approximate
   &&PlayStation.Decode(0x0CE6,dualSenseReport,true)==null,"DualSense USB report and rejection of Bluetooth simple mode");
  var dualShockReport=new byte[78];dualShockReport[0]=0x11;dualShockReport[32]=7;
  var dualShock=PlayStation.Decode(0x09CC,dualShockReport,true);
  check(dualShock!=null&&dualShock.Percent==75&&!dualShock.Charging
   &&PlayStation.Decode(0x09CC,new byte[10],true)==null,"DualShock only accepts battery-bearing extended Bluetooth reports");
  var adapterReport=new byte[64];adapterReport[0]=1;adapterReport[31]=4;
  check(PlayStation.Decode(0x0BA0,adapterReport,false)==null,"An unpaired DualShock adapter has no battery reading");
  var systemBattery=WindowsDevices.SystemBattery(8,82);
  check(systemBattery!=null&&systemBattery.Charging&&systemBattery.Percent==82&&!new UserSettings().IsShown(systemBattery)
   &&WindowsDevices.SystemBattery(128,82)==null&&WindowsDevices.SystemBattery(255,255)==null,
   "System battery is opt-in and omitted when Windows has no usable reading");
  check(WindowsRuntimeBattery.ReadPercent((byte)80)==80&&WindowsRuntimeBattery.ReadPercent((uint)80)==80
   &&WindowsRuntimeBattery.ReadPercent((byte)101)==-1&&WindowsRuntimeBattery.ReadPercent(null)==-1,
   "Windows Bluetooth percentages reject unknown values and accept the standard byte property");
  check(WindowsDevices.BluetoothName("HD 350BT Hands-Free AG Audio")=="HD 350BT"
   &&WindowsDevices.KindFromClass(0,"HD 350BT")=="Kulaklık","HD 350BT is displayed as a headset with the profile suffix removed");
  check(WindowsRuntimeBattery.CapacityPercent(250,500)==50&&WindowsRuntimeBattery.CapacityPercent(0,500)==0
   &&WindowsRuntimeBattery.CapacityPercent(500,0)==-1&&WindowsRuntimeBattery.CapacityPercent(null,500)==-1,
   "Xbox Gaming Input uses real capacities without inventing a percentage for absent data");
  var piao=new HidInfo{Vid=0x372E,Pid=0x1014,Page=0xFF00,Usage=1,Feature=64};
  byte[] piaoRequest=IpiPiao.Request();
  check(IpiPiao.Matches(piao)&&piaoRequest.Length==64&&piaoRequest[0]==3&&piaoRequest[1]==0x20
   &&piaoRequest[5]==0x4F&&piaoRequest[6]==0x81,"IPI PIAO uses the documented feature report on its receiver");
  piao.Pid=0x1015;
  check(IpiPiao.Matches(piao),"IPI PIAO cable uses the same feature channel");
  piao.Page=0xFF06;
  check(!IpiPiao.Matches(piao),"IPI PIAO does not send the request to the wrong vendor collection");
  check(IpiPiao.Decode(new byte[]{3,0x50,0,1,0,0x4F,0x81,64})==64
   &&IpiPiao.Decode(new byte[]{3,0x50,0,1,0,0x81,0xE4,0})==0xE4
   &&IpiPiao.Decode(new byte[]{3,0x50,2,0,0,0x4F,0x81,64})<0
   &&IpiPiao.Decode(new byte[]{3,0x50,0,1,0,0x81,0x7F,0})<0
   &&IpiPiao.Decode(piaoRequest)<0,"IPI PIAO accepts documented battery replies and rejects an echo or invalid reading");
  check(IpiPiao.Request(2)[1]==0x22&&IpiPiao.Decode(new byte[]{3,0x50,0,1,2,0x81,64,0},2)==64,
   "IPI PIAO accepts the alternate documented battery request and response");
  var thread=System.Threading.Thread.CurrentThread;
  var originalLanguage=thread.CurrentUICulture;
  try {
   foreach(string locale in new[]{"es-ES","es-MX"}) {
    thread.CurrentUICulture=System.Globalization.CultureInfo.GetCultureInfo(locale);
    var chargingTooltip=Widget.DeviceTooltip(new BatteryState{Name="Mouse",Percent=53,Charging=true,Sample=now,Status="USB HID"});
    check(L.DeviceReport=="Copiar informe del dispositivo"&&L.CheckUpdates=="Buscar actualizaciones"
     &&L.DeviceCount(1)=="1 dispositivo"&&L.DeviceCount(2)=="2 dispositivos"
     &&chargingTooltip=="Mouse\nCargando: %53\nHora: 23:00\nFuente: USB HID",
     "Spanish menu labels and charging tooltip follow the display culture for "+locale);
   }
   thread.CurrentUICulture=System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
   check(L.DeviceReport=="Cihaz raporunu kopyala","Turkish menu labels are preserved");
   thread.CurrentUICulture=System.Globalization.CultureInfo.GetCultureInfo("de-DE");
   check(L.DeviceReport=="Copy device report","Other display languages still fall back to English");
  } finally { thread.CurrentUICulture=originalLanguage; }
  File.WriteAllText(Path.Combine(Program.Root,"test-results.txt"),count+" checks, "+failures.Count+" failures\n"+string.Join("\n",failures));
  return failures.Count==0?0:1;
 }
}
