using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Text;

public class HidInfo {
 public string Path, Name;
 public ushort Vid, Pid, Page, Usage, Input, Output, Feature;
 public override string ToString() { return string.Format("{0:X4}:{1:X4} {2:X4}:{3:X4} in={4} out={5} feature={6} {7}",Vid,Pid,Page,Usage,Input,Output,Feature,Name); }
}
public static class Native {
 [StructLayout(LayoutKind.Sequential)] public struct Interface { public int Size; public Guid Guid; public int Flags; public IntPtr Reserved; }
 [StructLayout(LayoutKind.Sequential)] public struct Attributes { public int Size; public ushort Vid,Pid,Version; }
 [StructLayout(LayoutKind.Sequential)] public struct Overlap { public IntPtr Internal,InternalHigh; public uint Offset,OffsetHigh; public IntPtr Event; }
 [DllImport("hid.dll")] static extern void HidD_GetHidGuid(out Guid g);
 [DllImport("hid.dll")] static extern bool HidD_GetAttributes(SafeFileHandle h,ref Attributes a);
 [DllImport("hid.dll")] static extern bool HidD_GetPreparsedData(SafeFileHandle h,out IntPtr p);
 [DllImport("hid.dll")] static extern bool HidD_FreePreparsedData(IntPtr p);
 [DllImport("hid.dll")] static extern int HidP_GetCaps(IntPtr p,IntPtr caps);
 [DllImport("hid.dll",CharSet=CharSet.Unicode)] static extern bool HidD_GetProductString(SafeFileHandle h,StringBuilder name,int size);
 [DllImport("hid.dll")] public static extern bool HidD_FlushQueue(SafeFileHandle h);
 [DllImport("setupapi.dll",CharSet=CharSet.Unicode)] static extern IntPtr SetupDiGetClassDevs(ref Guid g,string e,IntPtr w,uint flags);
 [DllImport("setupapi.dll")] static extern bool SetupDiEnumDeviceInterfaces(IntPtr d,IntPtr dev,ref Guid g,uint i,ref Interface f);
 [DllImport("setupapi.dll",CharSet=CharSet.Unicode)] static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr d,ref Interface f,IntPtr detail,uint size,out uint required,IntPtr device);
 [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr d);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] public static extern SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool ReadFile(SafeFileHandle h,IntPtr buffer,int len,out uint count,ref Overlap o);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool WriteFile(SafeFileHandle h,IntPtr buffer,int len,out uint count,ref Overlap o);
 [DllImport("kernel32.dll")] static extern IntPtr CreateEvent(IntPtr attr,bool manual,bool state,string name);
 [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr h,uint ms);
 [DllImport("kernel32.dll")] static extern bool CancelIo(SafeFileHandle h);
 [DllImport("kernel32.dll")] static extern bool GetOverlappedResult(SafeFileHandle h,ref Overlap o,out uint count,bool wait);
 [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
 public static List<HidInfo> Scan() {
  var list=new List<HidInfo>(); Guid g; HidD_GetHidGuid(out g); IntPtr d=SetupDiGetClassDevs(ref g,null,IntPtr.Zero,18);
  if(d==new IntPtr(-1)) throw new InvalidOperationException("Windows HID listesi okunamadı.");
  try { for(uint i=0;;i++) { var f=new Interface{Size=Marshal.SizeOf(typeof(Interface))}; if(!SetupDiEnumDeviceInterfaces(d,IntPtr.Zero,ref g,i,ref f)) break;
   uint n; SetupDiGetDeviceInterfaceDetail(d,ref f,IntPtr.Zero,0,out n,IntPtr.Zero); if(n==0)continue;
   IntPtr b=Marshal.AllocHGlobal((int)n); try { Marshal.WriteInt32(b,IntPtr.Size==8?8:6); if(!SetupDiGetDeviceInterfaceDetail(d,ref f,b,n,out n,IntPtr.Zero))continue;
    string path=Marshal.PtrToStringUni(IntPtr.Add(b,4)); using(var h=CreateFile(path,0,3,IntPtr.Zero,3,0,IntPtr.Zero)) {
     if(h.IsInvalid)continue; var a=new Attributes{Size=Marshal.SizeOf(typeof(Attributes))}; if(!HidD_GetAttributes(h,ref a))continue;
     var info=new HidInfo{Path=path,Vid=a.Vid,Pid=a.Pid}; var name=new StringBuilder(256); HidD_GetProductString(h,name,512); info.Name=name.ToString(); IntPtr pp;
     if(HidD_GetPreparsedData(h,out pp)) { IntPtr caps=Marshal.AllocHGlobal(64); try { if(HidP_GetCaps(pp,caps)>=0) { info.Usage=(ushort)Marshal.ReadInt16(caps,0); info.Page=(ushort)Marshal.ReadInt16(caps,2); info.Input=(ushort)Marshal.ReadInt16(caps,4); info.Output=(ushort)Marshal.ReadInt16(caps,6); info.Feature=(ushort)Marshal.ReadInt16(caps,8); } } finally {Marshal.FreeHGlobal(caps);HidD_FreePreparsedData(pp);} }
     list.Add(info);
    }
   } finally {Marshal.FreeHGlobal(b);} }
  } finally {SetupDiDestroyDeviceInfoList(d);} return list;
 }
 public static byte[] Transfer(SafeFileHandle h,byte[] bytes,int length,bool write,int timeout) {
  if(length<1 || length>8192)return null; IntPtr b=Marshal.AllocHGlobal(length); var o=new Overlap{Event=CreateEvent(IntPtr.Zero,true,false,null)};
  try { if(write) {var padded=new byte[length];Array.Copy(bytes,padded,Math.Min(bytes.Length,length));Marshal.Copy(padded,0,b,length);} uint n;
   bool ok=write?WriteFile(h,b,length,out n,ref o):ReadFile(h,b,length,out n,ref o);
   if(!ok && Marshal.GetLastWin32Error()!=997)return null;
   if(WaitForSingleObject(o.Event,(uint)timeout)!=0) {CancelIo(h);GetOverlappedResult(h,ref o,out n,true);return null;}
   if(!GetOverlappedResult(h,ref o,out n,false))return null;var result=new byte[n];Marshal.Copy(b,result,0,(int)n);return result;
  } finally {CloseHandle(o.Event);Marshal.FreeHGlobal(b);}
 }
}
public sealed class HidChannel:IDisposable {
 public HidInfo Info; public SafeFileHandle Handle;
 public HidChannel(HidInfo info) {Info=info;Handle=Native.CreateFile(info.Path,0xC0000000,3,IntPtr.Zero,3,0x40000000,IntPtr.Zero);}
 public bool Valid { get {return !Handle.IsInvalid;} }
 public bool Send(byte[] data) { return Valid && Info.Output>=data.Length && Native.Transfer(Handle,data,Info.Output,true,500)!=null; }
 public byte[] Read(int timeout) {return Native.Transfer(Handle,null,Info.Input,false,timeout);}
 public void Dispose(){Handle.Dispose();}
}
