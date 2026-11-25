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
 static byte[] Logi(HidChannel d,byte feature,byte function,params byte[] args) {
  byte[] b=new byte[20];b[0]=0x11;b[1]=0xFF;b[2]=feature;b[3]=function;Array.Copy(args,0,b,4,Math.Min(args.Length,16));
  if(!d.Send(b))return null;var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<500){var r=d.Read((int)(500-watch.ElapsedMilliseconds));if(r==null)break;if(r.Length<7 || r[1]!=0xFF)continue;
   if(r[2]==feature && r[3]==function)return r.Skip(4).ToArray();if((r[2]==0xFF || r[2]==0x8F)&&r[3]==feature && r[4]==function)return null;
  }return null;
 }
 static byte Feature(HidChannel d,int id){var r=Logi(d,0,0x0D,(byte)(id>>8),(byte)id);return r==null?(byte)0:r[0];}
 public static int VoltagePercent(int mv) {
  int[] v={4102,4046,3993,3950,3915,3877,3838,3809,3789,3772,3758,3747,3736,3722,3707,3687,3661,3644,3548,3426,3293};
  if(mv>=v[0])return 100;if(mv<=v[v.Length-1])return 0;for(int i=1;i<v.Length;i++)if(mv>=v[i])return (int)Math.Round(100-(i-1)*5-5.0*(v[i-1]-mv)/(v[i-1]-v[i]));return -1;
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
  var ultimate2C=EightBitDo.ReadUltimate2C(all);
  if(ultimate2C!=null)result.Add(ultimate2C);
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
  var headset=all.Where(x=>x.Vid==0x046D && x.Pid==0x0ABA && x.Page==0xFF43 && x.Usage==0x0202 && x.Output>=20).ToList();
  if(headset.Count>0){var s=new BatteryState{Key="headset",Name="Logitech PRO X Wireless",Kind="Kulaklık",Status="Pil okunamadı; uyuyor veya kapalı olabilir"};
   foreach(var info in headset)using(var d=new HidChannel(info)){if(!d.Valid){s.Status="HID erişimi yok";continue;}
    foreach(int fid in new[]{0x1000,0x1004,0x1F20}){byte f=Feature(d,fid);if(f==0)continue;var p=Logi(d,f,(byte)(fid==0x1004?0x1D:0x0D));if(p==null||p.Length<3)continue;
     if(fid==0x1000 && p[0]<=100){s.Percent=p[0];s.Charging=p[2]>=1&&p[2]<=4;}
     else if(fid==0x1004 && p[0]<=100){s.Percent=p[0];s.Charging=p[2]>=1&&p[2]<=3;}
     else if(fid==0x1F20){int mv=(p[0]<<8)|p[1];if(mv<2500||mv>5000)continue;s.Millivolts=mv;s.Percent=VoltagePercent(mv);s.Approximate=true;s.Charging=p[2]==3;}
     if(s.Percent>=0){s.Sample=DateTime.Now;s.Connection=DeviceConnection.Connected;s.Status=s.Approximate?"Canlı • voltajdan yaklaşık yüzde":"Canlı • HID++";break;}
    }if(s.Percent>=0)break;
   }result.Add(s);
  }
  var keyboard=SteelSeries.ReadKeyboard();
  if(keyboard.Percent>=0||keyboard.Connection!=DeviceConnection.Unknown||all.Any(x=>x.Vid==0x1038&&(x.Pid==0x1630||x.Pid==0x1632||x.Pid==0x1633)))result.Add(keyboard);
  foreach(var ghub in GHub.Poll()){
   if(ghub.Stale&&result.Any(x=>x.Key==ghub.Key&&x.Percent>=0&&!x.Stale))continue;
   result.RemoveAll(x=>x.Key==ghub.Key);result.Add(ghub);
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
}
