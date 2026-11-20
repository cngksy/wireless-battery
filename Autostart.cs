using System;
using Microsoft.Win32;

public static class Autostart {
 const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run";
 const string ValueName="Wireless Battery";
 public static string CommandFor(string path){return "\""+path+"\"";}
 public static bool IsEnabled(){
  try{using(var key=Registry.CurrentUser.OpenSubKey(RunKey)){return key!=null&&string.Equals(key.GetValue(ValueName) as string,CommandFor(System.Windows.Forms.Application.ExecutablePath),StringComparison.OrdinalIgnoreCase);}}catch{return false;}
 }
 public static void Set(bool enabled){
  using(var key=Registry.CurrentUser.CreateSubKey(RunKey)){
   if(enabled)key.SetValue(ValueName,CommandFor(System.Windows.Forms.Application.ExecutablePath),RegistryValueKind.String);
   else key.DeleteValue(ValueName,false);
  }
 }
}
