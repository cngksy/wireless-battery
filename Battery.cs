// GPL-3.0-or-later. Protocol references and attribution: README.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

public enum DeviceConnection { Unknown, Connected, Disconnected }

public class BatteryState {
 public string Key,Name,Kind,Status,LevelText;
 public bool BatterySupported=true;
 public int Percent=-1,Millivolts;
 public bool Charging,Approximate,Stale,ReceiverPresent;
 public DeviceConnection Connection;
 public DateTime Sample;
 public string Level {get{return !string.IsNullOrEmpty(LevelText)?LevelText:Percent<0?"--":Percent+"%";}}
 public bool Dimmed {get{
  if(Connection==DeviceConnection.Disconnected)return true;
  if(Connection==DeviceConnection.Connected)return false;
  return Percent<0||(Stale&&!(Key=="mouse"&&ReceiverPresent));
 }}
}
public static class Batteries {
 internal const string LogitechG305Key="logitech:g305";
 static ushort lastHeadsetPid;
 static int lastHeadsetPercent=-1;
 public static BatteryState PreserveLastReading(BatteryState current,BatteryState previous,DateTime now) {
  if((current.Percent>=0&&!current.Stale&&current.Connection!=DeviceConnection.Disconnected)
   ||previous==null||previous.Percent<0||previous.Percent>100||previous.Sample==default(DateTime)
   ||previous.Sample.ToUniversalTime()>now.ToUniversalTime())return current;
  if(current.Key=="mouse"&&!string.Equals(current.Name,previous.Name,StringComparison.OrdinalIgnoreCase))return current;
  current.LevelText=previous.LevelText;current.Percent=previous.Percent;current.Approximate=previous.Approximate;current.Sample=previous.Sample;current.Millivolts=previous.Millivolts;current.Stale=true;
  current.Status=previous.Status;current.BatterySupported=previous.BatterySupported;current.Charging=false;
  return current;
 }
 public static List<BatteryState> MergeReadings(IEnumerable<BatteryState> observed,IEnumerable<BatteryState> previous,DateTime now,bool scanned) {
  var remembered=new Dictionary<string,BatteryState>();
  var nowUtc=now.ToUniversalTime();
  foreach(var reading in previous) {
   if(reading==null||string.IsNullOrEmpty(reading.Key)||reading.Percent<0||reading.Percent>100||reading.Sample==default(DateTime)||reading.Sample.ToUniversalTime()>nowUtc)continue;
   BatteryState existing;
   if(!remembered.TryGetValue(reading.Key,out existing)||reading.Sample.ToUniversalTime()>existing.Sample.ToUniversalTime())
    remembered[reading.Key]=reading;
  }
  var result=new List<BatteryState>();
  foreach(var reading in observed) {
   BatteryState saved;
   if(remembered.TryGetValue(reading.Key,out saved)) {
    remembered.Remove(reading.Key);
    PreserveLastReading(reading,saved,now);
   }
   if(reading.Connection==DeviceConnection.Disconnected) {
    if(reading.Percent<0)continue;
    reading.Stale=true;reading.Charging=false;
   }
   result.Add(reading);
  }
  foreach(var saved in remembered.Values) {
   if(IsLogitechSourceKey(saved.Key)&&result.Count(x=>IsLogitechSourceKey(x.Key)&&IsOtherLogitechSource(x,saved)
    &&x.Percent>=0&&!x.Stale&&SameLogitechDevice(x,saved))==1
    &&remembered.Values.Count(x=>IsLogitechSourceKey(x.Key)&&!IsOtherLogitechSource(x,saved)&&SameLogitechDevice(x,saved))==1)continue;
   // Loss of the G HUB API alone does not establish a radio disconnect.
   var connection=scanned&&!saved.Key.StartsWith("ghub:",StringComparison.Ordinal)
    ?DeviceConnection.Disconnected:DeviceConnection.Unknown;
   var missing=new BatteryState{Key=saved.Key,Name=saved.Name,Kind=saved.Kind,Connection=connection};
   result.Add(PreserveLastReading(missing,saved,now));
  }
  return result;
 }
 static byte Checksum(byte[] b) {int sum=0;for(int i=0;i<16;i++)sum+=b[i];return unchecked((byte)(0x55-sum));}
 public static byte[] MouseQuery(HidChannel d,byte command) {
  byte[] b=new byte[17];b[0]=8;b[1]=command;b[16]=Checksum(b);Native.HidD_FlushQueue(d.Handle);if(!d.Send(b))return null;
  var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<650) {var r=d.Read((int)(650-watch.ElapsedMilliseconds));if(r==null)break;if(r.Length>=17 && r[0]==8 && r[1]==command && r[16]==Checksum(r))return r;}return null;
 }
 static byte[] Logi(HidChannel d,byte device,byte feature,byte function,params byte[] args) {
  return LogiRequest(d,500,device,feature,function,args);
 }
 internal static byte[] LogiRequest(HidChannel d,int timeout,byte device,byte feature,byte function,params byte[] args) {
  byte[] b=new byte[20];b[0]=0x11;b[1]=device;b[2]=feature;b[3]=function;Array.Copy(args,0,b,4,Math.Min(args.Length,16));
  var watch=Stopwatch.StartNew();
  if(!d.Send(b,timeout))return null;
  int remaining;
  while((remaining=timeout-(int)watch.ElapsedMilliseconds)>0){var r=d.Read(remaining);if(r==null)break;if(r.Length<7||(r[0]!=0x10&&r[0]!=0x11)||r[1]!=device)continue;
   if(r[2]==feature && r[3]==function)return r.Skip(4).ToArray();if((r[2]==0xFF || r[2]==0x8F)&&r[3]==feature && r[4]==function)return null;
  }return null;
 }
 static byte Feature(HidChannel d,byte device,int id){var r=Logi(d,device,0,0x0D,(byte)(id>>8),(byte)id);return r==null?(byte)0:r[0];}
 internal static bool IsG305BatteryChannel(HidInfo info) {
  return info.Vid==0x046D&&info.Pid==0xC53F&&info.Page==0xFF00&&info.Usage==2&&info.Input==20&&info.Output==20;
 }
 internal static int ParseG305Battery(byte[] payload) {
  return payload!=null&&payload.Length>=3&&payload[0]>0&&payload[0]<=100?payload[0]:-1;
 }
 static BatteryState ReadG305(IEnumerable<HidInfo> devices) {
  const byte pairedMouse=1;
  foreach(var info in devices.Where(IsG305BatteryChannel))using(var channel=new HidChannel(info)) {
   if(!channel.Valid)continue;
   byte feature=Feature(channel,pairedMouse,0x1000);
   if(feature==0||feature==0xFF)continue;
   int percent=ParseG305Battery(Logi(channel,pairedMouse,feature,0x0D));
   if(percent>=0)return new BatteryState {
    Key=LogitechG305Key,Name="Logitech G305 LIGHTSPEED",Kind="Mouse",Percent=percent,
    Connection=DeviceConnection.Connected,Sample=DateTime.Now,Status="Canlı • HID++"
   };
  }
  return null;
 }
 public static int VoltagePercent(int mv) {
  int[] v={4102,4046,3993,3950,3915,3877,3838,3809,3789,3772,3758,3747,3736,3722,3707,3687,3661,3644,3548,3426,3293};
  if(mv>=v[0])return 100;if(mv<=v[v.Length-1])return 0;for(int i=1;i<v.Length;i++)if(mv>=v[i])return (int)Math.Round(100-(i-1)*5-5.0*(v[i-1]-mv)/(v[i-1]-v[i]));return -1;
 }
 // G headsets that only expose ADC measurement (0x1F20) report millivolts, not a percentage.
 // The G535 calibration comes from BatteryHub's headset_percent (HeadsetControl / G HUB model).
 internal static int HeadsetPercent(ushort pid,int mv) {
  if(pid==0x0AC4)return Curve(mv,new[]{4175,3817,3766,3730,3664,3310},new[]{100,50,30,20,5,0});
  return VoltagePercent(mv);
 }
 static int Curve(int mv,int[] v,int[] p) {
  if(mv>=v[0])return p[0];
  for(int i=1;i<v.Length;i++)if(mv>=v[i])return (int)Math.Round(p[i-1]+(p[i]-p[i-1])*(double)(v[i-1]-mv)/(v[i-1]-v[i]));
  return p[p.Length-1];
 }
 internal static bool IsLogitechHeadset(HidInfo x) {
  return x!=null&&x.Vid==0x046D&&(x.Pid==0x0ABA||x.Pid==0x0AC4)
   &&x.Page==0xFF43&&x.Usage==0x0202&&x.Output>=20;
 }
 internal static string LogitechHeadsetName(ushort pid) {
  switch(pid) {
   case 0x0ABA:return "Logitech PRO X Wireless";
   case 0x0AC4:return "Logitech G535 LIGHTSPEED";
   default:return "Logitech headset";
  }
 }
 internal static bool TryParseAdc(ushort pid,byte[] p,out int percent,out int millivolts,out bool charging) {
  percent=-1;millivolts=0;charging=false;
  if(p==null||p.Length<3)return false;
  int mv=(p[0]<<8)|p[1];
  if(mv<2500||mv>5000)return false;
  int value=HeadsetPercent(pid,mv);
  if(value<0)return false;
  millivolts=mv;percent=value;charging=p[2]==3||p[2]==7;
  return true;
 }
 // ATK / VXE / Compx mice (receivers and charging cables under VID 3554 and
 // 373B) share a 17-byte report-08 protocol. Any vendor collection large
 // enough is a candidate; only a correctly checksummed reply is accepted.
 // Same scope as BatteryHub's kysona.cpp (see README references).
 public static bool IsCompxExcluded(ushort vid,ushort pid) {
  // ATK headsets (serial protocol), ATK 8K Nano (Holtek) and AULA/MCHOSE keyboard receivers.
  if(vid==0x373B)return pid==0x11CA||pid==0x11CB||pid==0x1203||pid==0x1204||pid==0x1178;
  if(vid==0x3554)return pid==0xFA09||pid==0xFB40;
  return true;
 }
 public static bool IsVxeMouse(HidInfo x) {
  return x!=null&&(x.Vid==0x3554||x.Vid==0x373B)&&!IsCompxExcluded(x.Vid,x.Pid)&&x.Page>=0xFF00&&x.Input>=17&&x.Output>=17;
 }
 static bool IsCompxCable(HidInfo x){return (x.Vid==0x3554&&(x.Pid==0xF58C||x.Pid==0xF58F||x.Pid==0xF506||x.Pid==0xF55E))||(x.Vid==0x373B&&x.Pid==0x1273);}
 public static bool IsVerifiedVxe(HidInfo x){return x!=null&&((x.Vid==0x3554&&(x.Pid==0xF58A||x.Pid==0xF58C))||(x.Vid==0x373B&&(x.Pid==0x1272||x.Pid==0x1273)));}
 public static string MouseName(HidInfo x) {
  if(x==null)return "Wireless mouse";
  // Keep the established name for the verified VXE R1 ids so settings stay familiar.
  if(IsVerifiedVxe(x))return "VXE R1 / NordicMouse";
  string n=(x.Name??"").Trim();
  foreach(var suffix in new[]{" Wireless Receiver"," Dongle"," Receiver"," 2.4G"})if(n.Length>suffix.Length&&n.EndsWith(suffix,StringComparison.OrdinalIgnoreCase))n=n.Substring(0,n.Length-suffix.Length).Trim();
  // Confirmed by an X1 SE owner; preserve an explicit product name if firmware supplies one.
  if(x.Vid==0x373B&&x.Pid==0x10C9&&n.Equals("Nearlink Mouse",StringComparison.OrdinalIgnoreCase))return "ATK X1 SE";
  if(n.Length==0||n.Equals("2.4G Wireless",StringComparison.OrdinalIgnoreCase))n=x.Vid==0x373B?"ATK wireless mouse":"Wireless mouse";
  return n;
 }
 internal static HidInfo FindKnownMouseReceiver(IEnumerable<HidInfo> candidates,string knownMouseName) {
  return string.IsNullOrEmpty(knownMouseName)?null:candidates.FirstOrDefault(x=>string.Equals(MouseName(x),knownMouseName,StringComparison.OrdinalIgnoreCase));
 }
 public static List<BatteryState> Poll(List<HidInfo> all,string knownMouseName=null) {
  var result=new List<BatteryState>();
  result.AddRange(RazerDevices.Poll(all));
  result.AddRange(HyperXDevices.Poll(all));
  result.AddRange(AulaDevices.Poll(all));
  var harpe=AsusRog.ReadHarpe(all);
  if(harpe!=null)result.Add(harpe);
  result.AddRange(PlayStation.Poll(all));
  var piao=IpiPiao.Read(all);
  if(piao!=null)result.Add(piao);
  var ultimate2C=EightBitDo.ReadUltimate2C(all);
  if(ultimate2C!=null)result.Add(ultimate2C);
  var ultimate2=EightBitDo.ReadUltimate2(all);
  if(ultimate2!=null)result.Add(ultimate2);
  var mice=all.Where(IsVxeMouse)
   .OrderByDescending(IsCompxCable).ThenByDescending(x=>x.Page==0xFF02&&x.Usage==2).ToList();
  if(mice.Count>0) {
   var knownReceiver=FindKnownMouseReceiver(mice,knownMouseName);
   var s=new BatteryState{Key="mouse",Name=MouseName(knownReceiver??mice[0]),Kind="Mouse",ReceiverPresent=true,Status="Alıcı bağlı • pil ölçümü bekleniyor"};
   // Receiver command 03 can report zero while the radio is idle. It is not
   // a reliable power-off signal, so always ask for the actual battery report.
   foreach(var info in mice)using(var d=new HidChannel(info)){if(!d.Valid){s.Status="HID erişimi yok";continue;}
    for(int attempt=0;attempt<2;attempt++){var r=MouseQuery(d,4);
     if(r!=null && r[6]<=100){s.Percent=r[6];s.Charging=r[7]!=0;s.Millivolts=(r[8]<<8)|r[9];s.Sample=DateTime.Now;s.Status="Canlı • USB HID";s.Name=MouseName(info);break;}}
    if(s.Percent>=0)break;
   }
   // A matching receiver can stay listed after its first valid reading.
   // Unrelated ATK/Compx devices still need a valid reply before appearing.
   if(s.Percent>=0||knownReceiver!=null||mice.Any(IsVerifiedVxe))result.Add(s);
  }
  var headset=all.Where(IsLogitechHeadset).ToList();
  if(headset.Count>0){var s=new BatteryState{Key="headset",Name=LogitechHeadsetName(headset[0].Pid),Kind="Kulaklık",Status="Pil okunamadı; uyuyor veya kapalı olabilir"};
   foreach(var info in headset)using(var d=new HidChannel(info)){if(!d.Valid){s.Status="HID erişimi yok";continue;}
    foreach(int fid in new[]{0x1000,0x1004,0x1F20}){byte f=Feature(d,0xFF,fid);if(f==0)continue;var p=Logi(d,0xFF,f,(byte)(fid==0x1004?0x1D:0x0D));if(p==null||p.Length<3)continue;
     if(fid==0x1000 && p[0]<=100){s.Percent=p[0];s.Charging=p[2]>=1&&p[2]<=4;}
     else if(fid==0x1004 && p[0]<=100){s.Percent=p[0];s.Charging=p[2]>=1&&p[2]<=3;}
     else if(fid==0x1F20){int percent,mv;bool charging;if(!TryParseAdc(info.Pid,p,out percent,out mv,out charging))continue;
      s.Millivolts=mv;s.Approximate=true;s.Charging=charging;
      // Voltage rises while charging, so keep the last discharging reading instead of
      // letting the curve jump to ~100%. BatteryHub does the same for G headsets.
      if(charging&&lastHeadsetPid==info.Pid&&lastHeadsetPercent>=0)s.Percent=lastHeadsetPercent;
      else {s.Percent=percent;if(!charging){lastHeadsetPid=info.Pid;lastHeadsetPercent=percent;}}}
     if(s.Percent>=0){s.Name=LogitechHeadsetName(info.Pid);s.Sample=DateTime.Now;s.Connection=DeviceConnection.Connected;s.Status=s.Approximate?"Canlı • voltajdan yaklaşık yüzde":"Canlı • HID++";break;}
    }if(s.Percent>=0)break;
   }result.Add(s);
  }
  var keyboard=SteelSeries.ReadKeyboard();
  if(keyboard.Percent>=0||keyboard.Connection!=DeviceConnection.Unknown||all.Any(x=>x.Vid==0x1038&&(x.Pid==0x1630||x.Pid==0x1632||x.Pid==0x1633)))result.Add(keyboard);
  var ghubReadings=GHub.Poll();
  foreach(var ghub in ghubReadings){
   if(ghub.Stale&&result.Any(x=>x.Key==ghub.Key&&x.Percent>=0&&!x.Stale))continue;
   result.RemoveAll(x=>x.Key==ghub.Key);result.Add(ghub);
  }
  AddLogitechReceiverReadings(result,ghubReadings,LogitechReceivers.Poll(all));
  if(!result.Any(x=>x.Key==LogitechG305Key&&x.Percent>=0&&!x.Stale)) {
   var g305=ReadG305(all);
   if(g305!=null) {
    result.RemoveAll(x=>x.Key==LogitechG305Key);
    result.Add(g305);
   }
  }
  if (!result.Any(x => x.Key == LogitechG735.DeviceKey && x.Percent >= 0 && !x.Stale)) {
   foreach (var info in all.Where(LogitechG735.Matches)) {
    int percent;
    byte chargeState;
    if (!LogitechG735.TryRead(info, out percent, out chargeState)) continue;
    result.RemoveAll(x => x.Key == LogitechG735.DeviceKey);
    result.Add(new BatteryState {
     Key = LogitechG735.DeviceKey, Name = "Logitech G735", Kind = "Kulaklık",
     Percent = percent, Charging = chargeState == 1 || chargeState == 2,
     ReceiverPresent = true, Connection = DeviceConnection.Connected, Sample = DateTime.Now, Status = "Canlı • USB HID"
    });
    break;
   }
  }
  result.AddRange(WindowsDevices.Poll());
  return result;
 }
 internal static void AddLogitechReceiverReadings(List<BatteryState> result,IEnumerable<BatteryState> ghub,IEnumerable<BatteryState> receiver) {
  var ghubReadings=ghub.ToList();
  var native=receiver.ToList();
  foreach(var reading in native) {
   var matches=ghubReadings.Where(x=>SameLogitechDevice(x,reading)).ToList();
   if(matches.Count==1&&native.Count(x=>SameLogitechDevice(x,reading))==1) {
    var ghubReading=matches[0];
    if(ghubReading.Percent>=0&&!ghubReading.Stale)continue;
    result.Remove(ghubReading);
   }
   result.Add(reading);
  }
 }

 static bool IsLogitechSourceKey(string key) {
  return key!=null&&(key.StartsWith("ghub:",StringComparison.Ordinal)||key.StartsWith("logitech:hidpp:",StringComparison.Ordinal));
 }

 static bool IsOtherLogitechSource(BatteryState a,BatteryState b) {
  return a.Key.StartsWith("ghub:",StringComparison.Ordinal)!=b.Key.StartsWith("ghub:",StringComparison.Ordinal);
 }

 static bool SameLogitechDevice(BatteryState a,BatteryState b) {
  if(a.Kind!=b.Kind)return false;
  var name=LogitechModelName(a.Name);
  return name.Length>0&&name==LogitechModelName(b.Name);
 }

 static string LogitechModelName(string name) {
  if(string.IsNullOrWhiteSpace(name))return "";
  var words=name.ToUpperInvariant().Split((char[])null,StringSplitOptions.RemoveEmptyEntries).ToList();
  if(words[0]=="LOGITECH")words.RemoveAt(0);
  if(words.Count>0&&(words[words.Count-1]=="MOUSE"||words[words.Count-1]=="KEYBOARD"||words[words.Count-1]=="HEADSET")) {
   words.RemoveAt(words.Count-1);
   while(words.Count>0) {
    string word=words[words.Count-1];
    if(word!="GAMING"&&word!="WIRELESS"&&word!="MECHANICAL")break;
    words.RemoveAt(words.Count-1);
   }
  }
  string model=string.Join(" ",words.Select(x=>x=="LS"?"LIGHTSPEED":x));
  switch(model) {
   case "X2 SUPERSTRIKE":
   case "PRO X 2 SUPERSTRIKE":
   case "PRO X2 SUPERSTRIIKE": // Firmware spelling in the DeviceNameType response.
    return "PRO X2 SUPERSTRIKE";
   default:return model;
  }
 }
}
