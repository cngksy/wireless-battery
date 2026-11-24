using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// A child of the taskbar: follows Explorer stacking, fullscreen and auto-hide.
// Only this application's window styles/parent are changed.
class Widget:Form {
 public const float SlotDip=66f;
 // Long enough that moving to an icon to right-click it does not pop the details.
 public const int TooltipDelayMs=1200;
 const float TrayOverlapDip=9f;
 List<BatteryState> states=new List<BatteryState>();
 readonly System.Windows.Forms.Timer placement;
 readonly ToolTip tooltip=new ToolTip();
 readonly System.Windows.Forms.Timer hoverDelay;
 readonly MenuToggle menuToggle;
 string hoveredKey;
 IntPtr taskbar;readonly IntPtr host;
 float dpiScale=1;
 public bool EnabledByUser=true;
 [StructLayout(LayoutKind.Sequential)] struct Rect {public int Left,Top,Right,Bottom;}
 [StructLayout(LayoutKind.Sequential)] struct Pt {public int X,Y;}
 [StructLayout(LayoutKind.Sequential)] struct Extent {public int Width,Height;}
 [StructLayout(LayoutKind.Sequential,Pack=1)] struct Blend {public byte Op,Flags,Alpha,Format;}
 [StructLayout(LayoutKind.Sequential)] struct BitmapHeader {public uint Size;public int Width,Height;public ushort Planes,Bits;public uint Compression,ImageSize;public int XResolution,YResolution;public uint Colors,Important;}
 [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
 [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h,IntPtr dc);
 [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
 [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
 [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
 [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
 [DllImport("gdi32.dll")] static extern IntPtr CreateDIBSection(IntPtr dc,ref BitmapHeader header,uint usage,out IntPtr bits,IntPtr section,uint offset);
 [DllImport("user32.dll",SetLastError=true)] static extern bool UpdateLayeredWindow(IntPtr h,IntPtr dc,IntPtr dest,ref Extent size,IntPtr srcdc,ref Pt source,uint key,ref Blend blend,uint flags);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string cls,string name);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string cls,string name);
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h,out Rect rect);
 [DllImport("user32.dll")] static extern IntPtr SetParent(IntPtr child,IntPtr parent);
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h,int index);
 [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr h,int index,IntPtr value);
 [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
 [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr h);
 public Widget(MenuToggle toggle,IntPtr targetTaskbar) {
  host=targetTaskbar;
  menuToggle=toggle;
  Text="Wireless Battery taskbar";FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;
  BackColor=Color.Black;ForeColor=Color.White;
  DoubleBuffered=true;ClientSize=new Size(184,40);
  tooltip.ShowAlways=true;tooltip.UseAnimation=false;tooltip.UseFading=false;
  hoverDelay=new System.Windows.Forms.Timer{Interval=TooltipDelayMs};hoverDelay.Tick+=(s,e)=>{hoverDelay.Stop();ShowDeviceTooltip();};
  placement=new System.Windows.Forms.Timer{Interval=1000};placement.Tick+=(s,e)=>AnchorToTray();placement.Start();
 }
 protected override bool ShowWithoutActivation {get{return true;}}
 protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ExStyle|=0x08000000|0x80|0x80000;return p;}}
 protected override void OnShown(EventArgs e){base.OnShown(e);AnchorToTray();}
 public void SetStates(List<BatteryState> values) {
  states=values.OrderBy(x=>x.Kind=="Kulaklık"?0:x.Kind=="Mouse"?1:2).ToList();
  if(hoveredKey!=null&&!states.Any(x=>x.Key==hoveredKey))ClearDeviceTooltip();
  AnchorToTray();RenderLayer();
 }
 public static float SlotWidthDip(BatteryState state) {
  string label=state==null?"":state.Level;
  return SlotDip+(state!=null&&state.Charging?10:0)+(label.Length>3?6:0);
 }
 public static float DeviceLeftDip(List<BatteryState> values,int index) {
  float left=0;for(int i=0;i<index&&i<values.Count;i++)left+=SlotWidthDip(values[i]);return left;
 }
 public static float StripWidthDip(List<BatteryState> values) {
  float width=0;foreach(var state in values)width+=SlotWidthDip(state);return Math.Max(SlotDip,width);
 }
 public static int HitDevice(float x,float scale,List<BatteryState> values) {
  if(scale<=0||x<0)return -1;float dip=x/scale,left=0;
  for(int i=0;i<values.Count;i++){left+=SlotWidthDip(values[i]);if(dip<left)return i;}return -1;
 }
 public static string DeviceTooltip(BatteryState state) {
  var text=DeviceTooltipText(state);
  return state.Key=="mouse"&&state.Stale&&state.ReceiverPresent?text+"\n"+L.MouseReceiverWaiting:text;
 }
 static string DeviceTooltipText(BatteryState state) {
  string level=state.Percent>=0?"%"+state.Percent:state.Level;
  string time=state.Sample==default(DateTime)?"--:--":state.Sample.ToString("HH:mm");
  return state.Name+"\n"+L.BatteryLabel(state.Charging)+level+"\n"+L.TimeLabel+time+"\n"+L.By(DeviceSource(state));
 }
 public static string DeviceSource(BatteryState state) {
  string status=state.Status??"";
  if(status.IndexOf("G HUB",StringComparison.OrdinalIgnoreCase)>=0)return "G HUB";
  if(status.IndexOf("SteelSeries GG",StringComparison.OrdinalIgnoreCase)>=0)return "SteelSeries GG";
  if(status.IndexOf("XInput",StringComparison.OrdinalIgnoreCase)>=0)return "Windows XInput";
  if(status.IndexOf("Bluetooth",StringComparison.OrdinalIgnoreCase)>=0)return "Windows Bluetooth";
  if(status.IndexOf("HID++",StringComparison.OrdinalIgnoreCase)>=0||status.IndexOf("voltaj",StringComparison.OrdinalIgnoreCase)>=0)return "Logitech HID++";
  if(status.IndexOf("Razer USB HID",StringComparison.OrdinalIgnoreCase)>=0)return "Razer USB HID";
  if(status.IndexOf("HyperX USB HID",StringComparison.OrdinalIgnoreCase)>=0)return "HyperX USB HID";
  if(status.IndexOf("USB HID",StringComparison.OrdinalIgnoreCase)>=0||state.Key=="mouse")return "USB HID";
  return "Windows";
 }
 protected override void OnMouseMove(MouseEventArgs e) {
  base.OnMouseMove(e);int index=HitDevice(e.X,dpiScale,states);
  string key=index>=0?states[index].Key:null;if(key==hoveredKey)return;
  ClearDeviceTooltip();hoveredKey=key;if(key!=null)hoverDelay.Start();
 }
 protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);ClearDeviceTooltip();menuToggle.Press();}
 protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);menuToggle.Release(e.Button);}
 protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);ClearDeviceTooltip();}
 void ClearDeviceTooltip(){hoverDelay.Stop();tooltip.Hide(this);hoveredKey=null;}
 void ShowDeviceTooltip() {
  if(menuToggle.IsOpen)return;
  int index=states.FindIndex(x=>x.Key==hoveredKey);if(index<0)return;
  tooltip.Show(DeviceTooltip(states[index]),this,(int)Math.Round(DeviceLeftDip(states,index)*dpiScale),-(int)Math.Round(80*dpiScale),10000);
 }
 void AnchorToTray() {
  if(IsDisposed||!IsHandleCreated)return;
  IntPtr bar=host;Taskbars.Rect measured;int trayLeft;bool trayPadded;
  if(!Taskbars.GetPlacement(bar,out measured,out trayLeft,out trayPadded)){Hide();return;}
  Rect br=new Rect{Left=measured.Left,Top=measured.Top,Right=measured.Right,Bottom=measured.Bottom};
  if(taskbar!=bar){taskbar=bar;long style=GetWindowLongPtr(Handle,-16).ToInt64();SetWindowLongPtr(Handle,-16,new IntPtr((style&~0x80000000L)|0x40000000L));SetParent(Handle,bar);}
  float scale=GetDpiForWindow(bar)/96f;
  dpiScale=scale;
  if(states.Count==0){Hide();return;}
  int width=(int)Math.Ceiling(StripWidthDip(states)*scale);
  int height=br.Bottom-br.Top;
  // The Windows 11 tray reserves a little transparent padding before the
  // chevron. Let our equally transparent tail share nine DIPs of that padding
  // so the visual gap matches the spacing between native tray items.
  // Secondary taskbars have no such padding before the clock: no overlap.
  int x=trayLeft-br.Left-width+(trayPadded?(int)Math.Round(TrayOverlapDip*scale):0);
  if(x<0||height>br.Right-br.Left){Hide();return;}
  bool resized=ClientSize.Width!=width||ClientSize.Height!=height;
  SetWindowPos(Handle,IntPtr.Zero,x,0,width,height,0x10|0x4);
  if(resized)RenderLayer();
  if(EnabledByUser&&!Visible)Show();
  Rect wr;if(GetWindowRect(Handle,out wr)){try{string layout=string.Format("taskbar={0},{1},{2},{3}\ntray={4},{5},{6},{7}\nwidget={8},{9},{10},{11}\nchild=True\n",br.Left,br.Top,br.Right,br.Bottom,trayLeft,br.Top,br.Right,br.Bottom,wr.Left,wr.Top,wr.Right,wr.Bottom);string path=Program.DataPath("taskbar-layout-"+host.ToInt64().ToString("X")+".txt");if(!File.Exists(path)||File.ReadAllText(path)!=layout)File.WriteAllText(path,layout);}catch{}}
 }
 protected override void OnPaintBackground(PaintEventArgs e) {}
 protected override void OnPaint(PaintEventArgs e) {}
 void RenderLayer() {
  int width=ClientSize.Width,height=ClientSize.Height;if(!IsHandleCreated||width<1||height<1)return;
  using(var bmp=new Bitmap(width,height,PixelFormat.Format32bppPArgb)) {
   // Alpha 1 makes the whitespace inside each device's cell receive hover
   // events too, while remaining visually transparent.
   using(var g=Graphics.FromImage(bmp)){g.Clear(Color.FromArgb(1,0,0,0));DrawStrip(g,bmp.Size,states,dpiScale);}
   IntPtr screen=GetDC(IntPtr.Zero),memory=CreateCompatibleDC(screen),dib=IntPtr.Zero,previous=IntPtr.Zero;
   try {var header=new BitmapHeader{Size=40,Width=width,Height=-height,Planes=1,Bits=32};IntPtr bits;dib=CreateDIBSection(memory,ref header,0,out bits,IntPtr.Zero,0);if(dib==IntPtr.Zero)return;
    var data=bmp.LockBits(new Rectangle(0,0,width,height),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
    try{var row=new byte[width*4];for(int y=0;y<height;y++){Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),row,0,row.Length);Marshal.Copy(row,0,IntPtr.Add(bits,y*row.Length),row.Length);}}finally{bmp.UnlockBits(data);}
    previous=SelectObject(memory,dib);var extent=new Extent{Width=width,Height=height};var source=new Pt();var blend=new Blend{Alpha=255,Format=1};
    if(!UpdateLayeredWindow(Handle,screen,IntPtr.Zero,ref extent,memory,ref source,0,ref blend,2))File.WriteAllText(Program.DataPath("render-error.txt"),"UpdateLayeredWindow: "+Marshal.GetLastWin32Error());
   }finally{if(previous!=IntPtr.Zero)SelectObject(memory,previous);if(dib!=IntPtr.Zero)DeleteObject(dib);DeleteDC(memory);ReleaseDC(IntPtr.Zero,screen);}
  }
 }
 public static void DrawStrip(Graphics g,Size size,List<BatteryState> values,float scale=1) {
  g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
  float leftDip=0;
  for(int i=0;i<values.Count;i++) {
   var s=values[i];float origin=leftDip*scale;Color color=s.Dimmed?Color.FromArgb(155,155,155):s.Charging?Color.FromArgb(103,204,255):s.Percent<=15?Color.FromArgb(255,112,112):Color.White;
   var saved=g.Save();g.TranslateTransform((float)Math.Round(origin+3*scale),(float)Math.Round((size.Height-16*scale)/2));g.ScaleTransform(scale*16/28,scale*16/28);
   using(var pen=new Pen(color,2.0f)){pen.StartCap=pen.EndCap=LineCap.Round;pen.LineJoin=LineJoin.Round;
    if(s.Kind=="Kulaklık") {g.DrawArc(pen,2,2,23,24,180,180);g.DrawLine(pen,2,14,2,24);g.DrawLine(pen,25,14,25,24);g.DrawRectangle(pen,2,17,6,9);g.DrawRectangle(pen,19,17,6,9);}
    else if(s.Kind=="Mouse") {using(var path=new GraphicsPath()){path.AddArc(5,1,17,14,180,180);path.AddLine(22,8,22,19);path.AddArc(5,12,17,14,0,180);path.CloseFigure();g.DrawPath(pen,path);}g.DrawLine(pen,13.5f,3,13.5f,10);}
    else if(s.Kind=="Gamepad") {
     // Symmetric controller: flat top, two rounded grips, D-pad left, two buttons right.
     using(var path=new GraphicsPath()){
      path.AddLine(8,7,20,7);
      path.AddBezier(20,7,24.5f,7,26.5f,12,27,18);
      path.AddBezier(27,18,27.5f,24,23.5f,25,21.5f,21.5f);
      path.AddLine(21.5f,21.5f,19.5f,18.5f);
      path.AddLine(19.5f,18.5f,8.5f,18.5f);
      path.AddLine(8.5f,18.5f,6.5f,21.5f);
      path.AddBezier(6.5f,21.5f,4.5f,25,0.5f,24,1,18);
      path.AddBezier(1,18,1.5f,12,3.5f,7,8,7);
      path.CloseFigure();g.DrawPath(pen,path);
     }
     g.DrawLine(pen,5.5f,12.5f,10.5f,12.5f);g.DrawLine(pen,8,10,8,15);
     using(var dot=new SolidBrush(color)){g.FillEllipse(dot,17.6f,9.6f,2.8f,2.8f);g.FillEllipse(dot,20.6f,12.6f,2.8f,2.8f);}
    }
    else if(s.Kind=="Mikrofon") {g.DrawRectangle(pen,9,2,10,16);g.DrawArc(pen,5,10,18,13,0,180);g.DrawLine(pen,14,23,14,27);g.DrawLine(pen,9,27,19,27);}
    else if(s.Kind=="Aygıt") {g.DrawRectangle(pen,7,4,14,22);g.DrawLine(pen,11,1,17,1);g.DrawLine(pen,11,10,17,10);g.DrawLine(pen,11,15,17,15);g.DrawLine(pen,11,20,17,20);}
    else {g.DrawRectangle(pen,1,6,26,17);for(int row=0;row<2;row++)for(int col=0;col<5;col++)g.DrawLine(pen,5+col*4,11+row*4,6+col*4,11+row*4);g.DrawLine(pen,8,20,20,20);}
   }
   g.Restore(saved);
   if(s.Charging) {
    float bx=(float)Math.Round(origin+24*scale),by=(float)Math.Round((size.Height-10*scale)/2);
    using(var chargeBrush=new SolidBrush(color)) {
     g.FillPolygon(chargeBrush,new[]{new PointF(bx+3*scale,by),new PointF(bx,by+5*scale),new PointF(bx+3*scale,by+5*scale),new PointF(bx+1*scale,by+10*scale),new PointF(bx+7*scale,by+4*scale),new PointF(bx+4*scale,by+4*scale),new PointF(bx+6*scale,by)});
    }
   }
   using(var font=new Font("Segoe UI",12*scale,FontStyle.Regular,GraphicsUnit.Pixel))using(var brush=new SolidBrush(color)) {
    string label=s.Level;float labelX=(s.Charging?34:24)*scale;
    g.DrawString(label,font,brush,(float)Math.Round(origin+labelX),(float)Math.Round((size.Height-15*scale)/2),StringFormat.GenericTypographic);
   }
   leftDip+=SlotWidthDip(s);
  }
 }
 protected override void Dispose(bool disposing){if(disposing){placement.Dispose();hoverDelay.Dispose();tooltip.Dispose();}base.Dispose(disposing);}
}
