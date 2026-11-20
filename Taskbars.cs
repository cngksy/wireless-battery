using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Forms;

public static class Taskbars {
 [StructLayout(LayoutKind.Sequential)] public struct Rect {public int Left,Top,Right,Bottom;}
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string cls,string name);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string cls,string name);
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out Rect rect);
 public static List<IntPtr> List() {
  var result=new List<IntPtr>();IntPtr main=FindWindow("Shell_TrayWnd",null);if(main!=IntPtr.Zero)result.Add(main);
  for(IntPtr other=FindWindowEx(IntPtr.Zero,IntPtr.Zero,"Shell_SecondaryTrayWnd",null);other!=IntPtr.Zero;other=FindWindowEx(IntPtr.Zero,other,"Shell_SecondaryTrayWnd",null))result.Add(other);
  return result;
 }
 class ClockCache {public int Right;public DateTime Time;}
 static readonly Dictionary<IntPtr,ClockCache> cache=new Dictionary<IntPtr,ClockCache>();
 public static bool GetPlacement(IntPtr bar,out Rect bounds,out int right){bool padded;return GetPlacement(bar,out bounds,out right,out padded);}
 // padded: the anchor is the primary TrayNotifyWnd, whose left edge has
 // transparent padding before the chevron. Secondary taskbars anchor directly
 // to the clock, which has no such padding, so nothing may overlap it.
 public static bool GetPlacement(IntPtr bar,out Rect bounds,out int right,out bool padded) {
  right=0;padded=false;if(!GetWindowRect(bar,out bounds))return false;
  IntPtr tray=FindWindowEx(bar,IntPtr.Zero,"TrayNotifyWnd",null);Rect trayBounds;
  if(tray!=IntPtr.Zero&&GetWindowRect(tray,out trayBounds)&&trayBounds.Left>bounds.Left){right=trayBounds.Left;padded=true;return true;}
  IntPtr clock=FindWindowEx(bar,IntPtr.Zero,"ClockButton",null);
  if(clock!=IntPtr.Zero&&GetWindowRect(clock,out trayBounds)){right=trayBounds.Left;return true;}
  // Windows 11 secondary taskbars draw the clock in XAML instead of a child HWND.
  ClockCache saved;if(cache.TryGetValue(bar,out saved)&&(DateTime.UtcNow-saved.Time).TotalSeconds<5){right=saved.Right;return right>bounds.Left;}
  int left=int.MaxValue;
  try{var root=AutomationElement.FromHandle(bar);var condition=new PropertyCondition(AutomationElement.AutomationIdProperty,"SystemTrayIcon");
   var items=root.FindAll(TreeScope.Descendants,condition);
   foreach(AutomationElement item in items){var r=item.Current.BoundingRectangle;if(r.Width>0&&r.Left>bounds.Left&&r.Right<=bounds.Right)left=Math.Min(left,(int)r.Left);}
  }catch(Exception){}
  right=left==int.MaxValue?bounds.Right-8:left;cache[bar]=new ClockCache{Right=right,Time=DateTime.UtcNow};return true;
 }
}

class WidgetManager:IDisposable {
 readonly Dictionary<IntPtr,Widget> widgets=new Dictionary<IntPtr,Widget>();
 readonly Timer timer=new Timer{Interval=2000};readonly ContextMenuStrip menu;readonly Action<bool> toggleMenu;
 List<BatteryState> states=new List<BatteryState>();bool enabled;
 public WidgetManager(ContextMenuStrip context,Action<bool> toggle,bool show){menu=context;toggleMenu=toggle;enabled=show;timer.Tick+=(s,e)=>RefreshHosts();timer.Start();}
 public void Update(List<BatteryState> values,bool show){states=values;enabled=show;RefreshHosts();foreach(var widget in widgets.Values){widget.EnabledByUser=show;widget.SetStates(values);if(!show)widget.Hide();}}
 void RefreshHosts(){
  var bars=Taskbars.List();foreach(var handle in new List<IntPtr>(widgets.Keys))if(!bars.Contains(handle)||widgets[handle].IsDisposed){widgets[handle].Dispose();widgets.Remove(handle);}
  foreach(var handle in bars)if(!widgets.ContainsKey(handle)){var widget=new Widget(menu,toggleMenu,handle){EnabledByUser=enabled};widgets.Add(handle,widget);if(enabled)widget.Show();widget.SetStates(states);}
 }
 public void Dispose(){timer.Stop();timer.Dispose();foreach(var widget in widgets.Values)widget.Dispose();widgets.Clear();}
}
