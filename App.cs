using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Web.Script.Serialization;

static class Program {
 public static string Root=AppDomain.CurrentDomain.BaseDirectory;
 public static string DataRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Wireless Battery");
 public static string DataPath(string name){Directory.CreateDirectory(DataRoot);return Path.Combine(DataRoot,name);}
 static void PrepareData(){Directory.CreateDirectory(DataRoot);foreach(var name in new[]{"settings.json","battery-report.json"}){string oldPath=Path.Combine(Root,name),newPath=DataPath(name);try{if(!File.Exists(newPath)&&File.Exists(oldPath))File.Copy(oldPath,newPath);}catch{}}}
 public static List<BatteryState> SavedReadings(){try{return new JavaScriptSerializer().Deserialize<List<BatteryState>>(File.ReadAllText(DataPath("battery-report.json"))).Where(x=>x.Percent>=0&&x.Percent<=100&&x.Sample!=default(DateTime)).ToList();}catch{return new List<BatteryState>();}}
 [STAThread] static void Main(string[] args) {
  if(args.Contains("--scan")) {try{var all=Native.Scan();File.WriteAllText(Path.Combine(Root,"devices.txt"),string.Join(Environment.NewLine,all.Select(x=>x.ToString())));var states=Batteries.Poll(all);File.WriteAllText(Path.Combine(Root,"battery-report.json"),new JavaScriptSerializer().Serialize(states));}catch(Exception e){File.WriteAllText(Path.Combine(Root,"scan-error.txt"),e.ToString());Environment.ExitCode=1;}return;}
  if(args.Contains("--render")){var values=new JavaScriptSerializer().Deserialize<List<BatteryState>>(File.ReadAllText(Path.Combine(Root,"battery-report.json"))).Where(x=>x.Key!="keyboard"||x.Percent>=0).OrderBy(x=>x.Kind=="Kulaklık"?0:1).ToList();using(var bmp=new Bitmap((int)Math.Ceiling(Widget.StripWidthDip(values)),44)){using(var g=Graphics.FromImage(bmp)){g.Clear(Color.FromArgb(24,24,24));Widget.DrawStrip(g,bmp.Size,values);}bmp.Save(Path.Combine(Root,"taskbar-preview.png"));}return;}
  if(args.Contains("--test")){Environment.ExitCode=RegressionChecks.Run();return;}
  PrepareData();
  using(var exitSignal=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\WirelessBatteryExit")){
   if(args.Contains("--exit")){exitSignal.Set();return;}
   bool first;using(var mutex=new Mutex(true,"Local\\WirelessBatteryMonitor",out first)){if(!first)return;Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new BatteryApp(exitSignal));}
  }
 }
}
class BatteryApp:ApplicationContext {
 NotifyIcon main;ContextMenuStrip menu;WidgetManager widgets;UserSettings settings=UserSettings.Load();List<ToolStripItem> deviceItems=new List<ToolStripItem>();ToolStripSeparator devicesStart;
 System.Windows.Forms.Timer timer,exitTimer;EventWaitHandle exitSignal;bool busy;bool disposed;List<BatteryState> states=new List<BatteryState>();HashSet<string> alerts=new HashSet<string>();
 [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
 MenuToggle menuToggle;
 public BatteryApp(EventWaitHandle signal){
  exitSignal=signal;
  states=Program.SavedReadings();menu=new ContextMenuStrip();
  menu.Items.Add("Wireless Battery").Enabled=false;menu.Items.Add(new ToolStripSeparator());
  var toggle=new ToolStripMenuItem(L.TaskbarIndicator){Checked=settings.ShowWidget,CheckOnClick=true};
  toggle.CheckedChanged+=(s,e)=>{settings.ShowWidget=toggle.Checked;settings.Save();ApplyVisibility();};
  menu.Items.Add(toggle);menu.Items.Add(new ToolStripSeparator());
  try{Autostart.Set(settings.StartWithWindows);}catch{settings.StartWithWindows=false;}
  var startup=new ToolStripMenuItem(L.StartWithWindows){Checked=Autostart.IsEnabled(),CheckOnClick=true};
  bool changingStartup=false;
  startup.CheckedChanged+=(s,e)=>{if(changingStartup)return;try{Autostart.Set(startup.Checked);settings.StartWithWindows=startup.Checked;settings.Save();}catch{changingStartup=true;startup.Checked=Autostart.IsEnabled();changingStartup=false;if(main!=null)Balloon("Wireless Battery",L.StartupError,ToolTipIcon.Warning,null);}};
  menu.Items.Add(startup);
  updateItem=new ToolStripMenuItem(L.CheckUpdates);updateItem.Click+=(s,e)=>{if(available!=null)OpenRelease(available.Url);else CheckUpdates(true);};menu.Items.Add(updateItem);
  // Device entries are inserted between devicesStart and the separator after it.
  devicesStart=new ToolStripSeparator();menu.Items.Add(devicesStart);menu.Items.Add(new ToolStripSeparator());
  menu.Items.Add(L.Refresh,null,(s,e)=>Refresh());
  menu.Items.Add(new ToolStripSeparator());menu.Items.Add(L.Exit,null,(s,e)=>ExitThread());
  menu.Opening+=(s,e)=>RebuildDeviceMenu();
  main=new NotifyIcon{Icon=MakeAppIcon(),Text="Wireless Battery • "+L.Scanning,Visible=true};
  menuToggle=new MenuToggle(menu);
  main.MouseDown+=(s,e)=>menuToggle.Press();
  main.MouseUp+=(s,e)=>menuToggle.Release(e.Button);
  widgets=new WidgetManager(menuToggle,settings.ShowWidget);
  timer=new System.Windows.Forms.Timer{Interval=60000};timer.Tick+=(s,e)=>Refresh();timer.Start();Refresh();
  exitTimer=new System.Windows.Forms.Timer{Interval=250};exitTimer.Tick+=(s,e)=>{if(exitSignal.WaitOne(0))ExitThread();};exitTimer.Start();
  main.BalloonTipClicked+=(s,e)=>{if(balloonUrl!=null)OpenRelease(balloonUrl);};
  // First automatic check shortly after start, then every six hours.
  updateTimer=new System.Windows.Forms.Timer{Interval=30000};updateTimer.Tick+=(s,e)=>{updateTimer.Interval=6*60*60*1000;if(settings.CheckUpdates)CheckUpdates(false);};updateTimer.Start();
 }
 ToolStripMenuItem updateItem;UpdateInfo available;string balloonUrl;bool checkingUpdates;System.Windows.Forms.Timer updateTimer;
 void Balloon(string title,string text,ToolTipIcon icon,string url){balloonUrl=url;main.ShowBalloonTip(url!=null?10000:5000,title,text,icon);}
 static void OpenRelease(string url){if(!Updates.IsSafeReleaseUrl(url))url=Updates.ReleasesPage;try{System.Diagnostics.Process.Start(url);}catch{}}
 async void CheckUpdates(bool manual){
  if(checkingUpdates||disposed)return;checkingUpdates=true;
  try{
   var found=await Task.Run(()=>Updates.Check());if(disposed)return;
   if(found==null){available=null;updateItem.Text=L.CheckUpdates;if(manual)Balloon("Wireless Battery",L.UpToDate(Updates.Current.ToString()),ToolTipIcon.Info,null);return;}
   available=found;updateItem.Text=L.DownloadUpdate(found.Version.ToString());
   if(manual||settings.NotifiedUpdate!=found.Tag){settings.NotifiedUpdate=found.Tag;settings.Save();Balloon("Wireless Battery",L.UpdateAvailable(found.Version.ToString()),ToolTipIcon.Info,found.Url);}
  }catch(Exception e){if(manual&&!disposed)Balloon("Wireless Battery",L.UpdateError,ToolTipIcon.Warning,null);try{File.WriteAllText(Program.DataPath("update-error.txt"),e.ToString());}catch{}}
  finally{checkingUpdates=false;}
 }
 void RebuildDeviceMenu(){
  foreach(var item in deviceItems){menu.Items.Remove(item);item.Dispose();}deviceItems.Clear();int index=menu.Items.IndexOf(devicesStart)+1;
  foreach(var state in states.OrderBy(x=>x.Name)){
   string key=state.Key;var entry=new ToolStripMenuItem(state.Name+"  "+state.Level){Checked=settings.IsShown(state),ToolTipText=Widget.DeviceTooltip(state)};
   entry.Click+=(s,e)=>{settings.SetShown(key,!entry.Checked);entry.Checked=settings.IsShown(state);ApplyVisibility();};
   menu.Items.Insert(index++,entry);deviceItems.Add(entry);
  }
  if(states.Count==0){var item=new ToolStripMenuItem(L.NoDevices){Enabled=false};menu.Items.Insert(index++,item);deviceItems.Add(item);}
 }
 void ApplyVisibility(){widgets.Update(states.Where(settings.IsShown).ToList(),settings.ShowWidget);}
 async void Refresh(){if(busy||disposed)return;busy=true;try{var next=await Task.Run(()=>Batteries.Poll(Native.Scan()));if(disposed)return;if(!next.Any(x=>x.Key=="mouse")&&states.Any(x=>x.Key=="mouse"))next.Add(new BatteryState{Key="mouse",Name="VXE R1 / NordicMouse",Kind="Mouse",Status="Alıcı bulunamadı"});foreach(var item in next)Batteries.PreserveLastReading(item,states.Find(x=>x.Key==item.Key),DateTime.Now);states=next;File.WriteAllText(Program.DataPath("battery-report.json"),new JavaScriptSerializer().Serialize(states));
   foreach(var s in states){
    if(settings.IsShown(s)&&!s.Stale&&s.Percent>=0&&s.Percent<=15&&!s.Charging&&alerts.Add(s.Key))Balloon(s.Name,L.LowBattery(s.Level),ToolTipIcon.Warning,null);
    if(s.Charging||s.Percent>25)alerts.Remove(s.Key);
   }main.Text="Wireless Battery • "+L.DeviceCount(states.Count)+" • "+DateTime.Now.ToString("HH:mm");ApplyVisibility();
  }catch(Exception e){if(!disposed){main.Text="Wireless Battery • "+L.ReadError;try{File.WriteAllText(Program.DataPath("error.txt"),e.ToString());}catch{}}}finally{busy=false;}}
 static Icon MakeAppIcon(){using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("WirelessBattery.Icon"))using(var icon=new Icon(stream,16,16))return (Icon)icon.Clone();}
 protected override void ExitThreadCore(){disposed=true;timer.Stop();timer.Dispose();exitTimer.Stop();exitTimer.Dispose();updateTimer.Stop();updateTimer.Dispose();var appIcon=main.Icon;main.Visible=false;main.Dispose();appIcon.Dispose();widgets.Dispose();base.ExitThreadCore();}
}

// Shared by the tray icon and every taskbar indicator: while the menu is open,
// any left or right click on them only closes it; a right click opens it only
// when it was closed at the moment the button went down.
class MenuToggle {
 readonly ContextMenuStrip menu;DateTime closedAt=DateTime.MinValue;bool openAtPress;
 // The menu may auto-close on the same mouse-down just before we see it.
 public const double CloseGraceMs=300;
 public MenuToggle(ContextMenuStrip context){menu=context;menu.Closed+=(s,e)=>closedAt=DateTime.UtcNow;}
 public bool IsOpen {get{return menu.Visible;}}
 public static bool WasOpen(bool visible,double msSinceClosed){return visible||msSinceClosed<CloseGraceMs;}
 public static bool ShouldOpen(MouseButtons button,bool openAtPress,bool visible){return button==MouseButtons.Right&&!openAtPress&&!visible;}
 public void Press(){openAtPress=WasOpen(menu.Visible,(DateTime.UtcNow-closedAt).TotalMilliseconds);if(menu.Visible)menu.Close(ToolStripDropDownCloseReason.AppClicked);}
 public void Release(MouseButtons button){if(ShouldOpen(button,openAtPress,menu.Visible))menu.Show(Cursor.Position);}
}
