using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

public class UserSettings {
 public bool ShowWidget=true;
 public bool StartWithWindows=true;
 public bool CheckUpdates=true;
 public bool RememberLastCharge=true;
 public string NotifiedUpdate;
 public Dictionary<string,bool> Devices=new Dictionary<string,bool>();
 public bool IsShown(BatteryState state){bool shown;return Devices.TryGetValue(state.Key,out shown)?shown:state.BatterySupported;}
 public bool IncludesReading(BatteryState state){return RememberLastCharge||!state.Stale||!state.Dimmed;}
 public void SetShown(string key,bool shown){Devices[key]=shown;Save();}
 public static UserSettings Load(){try{var value=new JavaScriptSerializer().Deserialize<UserSettings>(File.ReadAllText(Program.DataPath("settings.json")));if(value!=null&&value.Devices!=null)return value;}catch{}return new UserSettings();}
 public void Save(){File.WriteAllText(Program.DataPath("settings.json"),new JavaScriptSerializer().Serialize(this));}
}
