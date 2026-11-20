using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class WindowsDevices {
 [StructLayout(LayoutKind.Sequential)] struct DeviceInfo {public int Size;public Guid ClassGuid;public uint DevInst;public IntPtr Reserved;}
 [StructLayout(LayoutKind.Sequential)] struct PropertyKey {public Guid Guid;public uint Id;public PropertyKey(string guid,uint id){Guid=new Guid(guid);Id=id;}}
 [DllImport("setupapi.dll",EntryPoint="SetupDiGetClassDevsW",CharSet=CharSet.Unicode)] static extern IntPtr GetDevices(IntPtr guid,string enumerator,IntPtr parent,uint flags);
 [DllImport("setupapi.dll")] static extern bool SetupDiEnumDeviceInfo(IntPtr set,uint index,ref DeviceInfo info);
 [DllImport("setupapi.dll",CharSet=CharSet.Unicode)] static extern bool SetupDiGetDeviceInstanceId(IntPtr set,ref DeviceInfo info,StringBuilder id,int length,out int required);
 [DllImport("setupapi.dll",CharSet=CharSet.Unicode)] static extern bool SetupDiGetDevicePropertyW(IntPtr set,ref DeviceInfo info,ref PropertyKey key,out uint type,byte[] value,uint length,out uint required,uint flags);
 [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
 [StructLayout(LayoutKind.Sequential)] struct XBattery {public byte Type,Level;}
 [DllImport("xinput1_4.dll")] static extern uint XInputGetBatteryInformation(uint index,byte type,out XBattery battery);
 static readonly PropertyKey Battery=new PropertyKey("104ea319-6ee2-4701-bd47-8ddbf425bbe5",2);
 static readonly PropertyKey Connected=new PropertyKey("83da6326-97a6-4088-9453-a1923f573b29",15);
 static readonly PropertyKey Container=new PropertyKey("8c7ed206-3f8a-4827-b3ab-ae9e1faefc6c",2);
 static readonly PropertyKey Name=new PropertyKey("a45c254e-df1c-4efd-8020-67d146a850e0",14);
 static readonly PropertyKey Description=new PropertyKey("a45c254e-df1c-4efd-8020-67d146a850e0",2);
 static readonly PropertyKey BluetoothClass=new PropertyKey("2bd67d8b-8beb-48d5-87e0-6cda3428040a",10);
 static byte[] Property(IntPtr set,ref DeviceInfo info,PropertyKey key,out uint type){var bytes=new byte[1024];uint needed;if(!SetupDiGetDevicePropertyW(set,ref info,ref key,out type,bytes,(uint)bytes.Length,out needed,0)||needed>bytes.Length)return null;Array.Resize(ref bytes,(int)needed);return bytes;}
 static string Text(IntPtr set,ref DeviceInfo info,PropertyKey key){uint type;var b=Property(set,ref info,key,out type);return b!=null&&type==18?Encoding.Unicode.GetString(b).TrimEnd('\0'):null;}
 public static string KindFromClass(uint cod,string name) {
  uint major=(cod>>8)&31,minor=(cod>>2)&63;
  if(major==4)return minor==4?"Mikrofon":"Kulaklık";
  if(major==5){if((minor&15)==1||(minor&15)==2)return "Gamepad";if((minor>>4)==1)return "Klavye";if((minor>>4)==2)return "Mouse";}
  string n=(name??"").ToLowerInvariant();if(n.Contains("keyboard"))return "Klavye";if(n.Contains("mouse"))return "Mouse";if(n.Contains("controller")||n.Contains("gamepad")||n.Contains("dualshock")||n.Contains("dualsense"))return "Gamepad";if(n.Contains("head")||n.Contains("buds"))return "Kulaklık";if(n.Contains("mic"))return "Mikrofon";return "Aygıt";
 }
 public static List<BatteryState> Poll() {
  var result=new List<BatteryState>();var merged=new Dictionary<string,BatteryState>();
  IntPtr set=GetDevices(IntPtr.Zero,null,IntPtr.Zero,6);
  if(set!=new IntPtr(-1))try {for(uint index=0;;index++) {
   var info=new DeviceInfo{Size=Marshal.SizeOf(typeof(DeviceInfo))};if(!SetupDiEnumDeviceInfo(set,index,ref info))break;
   var id=new StringBuilder(512);int needed;if(!SetupDiGetDeviceInstanceId(set,ref info,id,id.Capacity,out needed))continue;
   string instance=id.ToString();if(!instance.StartsWith("BTH",StringComparison.OrdinalIgnoreCase))continue;
   uint type;var bytes=Property(set,ref info,Battery,out type);int percent=-1;
   if(bytes!=null&&((type==7&&bytes.Length>=4)||(type==3&&bytes.Length>=1)))percent=type==7?(int)BitConverter.ToUInt32(bytes,0):bytes[0];
   if(percent<0||percent>100)continue;
   string name=Text(set,ref info,Name)??Text(set,ref info,Description)??"Bluetooth aygıtı";
   bytes=Property(set,ref info,Container,out type);string key=bytes!=null&&bytes.Length==16?new Guid(bytes).ToString():instance;
   bytes=Property(set,ref info,Connected,out type);bool connected=bytes==null||type!=17||bytes.Length<1||bytes[0]!=0;
   bytes=Property(set,ref info,BluetoothClass,out type);uint cod=bytes!=null&&type==7&&bytes.Length>=4?BitConverter.ToUInt32(bytes,0):0;
   var state=new BatteryState{Key="windows:"+key,Name=name,Kind=KindFromClass(cod,name),Percent=percent,Stale=!connected,Sample=DateTime.Now,Status=connected?"Windows Bluetooth pil bilgisi":"Windows son pil bilgisi • bağlı değil"};
   BatteryState existing;if(!merged.TryGetValue(state.Key,out existing)||(!existing.Stale&&connected)||existing.Stale&&!state.Stale)merged[state.Key]=state;
  }}finally{SetupDiDestroyDeviceInfoList(set);}
  result.AddRange(merged.Values);
  try{for(uint index=0;index<4;index++){XBattery battery;if(XInputGetBatteryInformation(index,0,out battery)!=0||battery.Type==0||battery.Type==1)continue;
   var state=new BatteryState{Key="xinput:"+index,Name="Xbox / XInput Gamepad "+(index+1),Kind="Gamepad",Status="Windows XInput • pil seviyesi",Sample=DateTime.Now};
   if((battery.Type==2||battery.Type==3)&&battery.Level<=3){state.Percent=new[]{0,15,60,100}[battery.Level];state.LevelText=new[]{"Boş","Az","Orta","Dolu"}[battery.Level];state.Approximate=true;}
   else{state.Status="Gamepad algılandı • pil seviyesi bildirilmedi";}result.Add(state);
  }}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
  return result;
 }
}
