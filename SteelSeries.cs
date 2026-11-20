using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Web.Script.Serialization;

// Reads the same status response as the installed GG frontend. No device writes.
public static class SteelSeries {
 static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=1024*1024};
 static Dictionary<string,object> Map(object value){return value as Dictionary<string,object>;}
 static object Field(Dictionary<string,object> data,string key){object value;return data!=null&&data.TryGetValue(key,out value)?value:null;}
 static Dictionary<string,object> Get(string address,string path) {
  Uri endpoint;
  if(!Uri.TryCreate("https://"+address+path,UriKind.Absolute,out endpoint)||endpoint.Host!="127.0.0.1"||endpoint.UserInfo.Length!=0||endpoint.Scheme!="https")throw new InvalidDataException("GG adresi yerel değil");
  var request=(HttpWebRequest)WebRequest.Create(endpoint);request.Method="GET";request.Proxy=null;request.AllowAutoRedirect=false;request.Timeout=2500;request.ReadWriteTimeout=2500;ServicePointManager.SecurityProtocol=(SecurityProtocolType)3072;
  // GG uses a local self-signed certificate. This exception belongs only to this
  // exact loopback request; global TLS validation is never changed.
  request.ServerCertificateValidationCallback=(sender,certificate,chain,errors)=>certificate!=null&&request.Address.Host=="127.0.0.1";
  using(var response=(HttpWebResponse)request.GetResponse())using(var reader=new StreamReader(response.GetResponseStream())) {
   var buffer=new char[4096];var text=new System.Text.StringBuilder();int n;
   while((n=reader.Read(buffer,0,buffer.Length))>0){if(text.Length+n>1024*1024)throw new InvalidDataException("GG yanıtı çok uzun");text.Append(buffer,0,n);}
   return Map(Json.DeserializeObject(text.ToString()));
  }
 }
 public static BatteryState ReadKeyboard() {
  var state=new BatteryState{Key="keyboard",Name="SteelSeries Apex Pro TKL Wireless",Kind="Klavye",Status="SteelSeries GG pil verisi alınamadı"};
  try {
   var path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"SteelSeries","GG","coreProps.json");
   var props=Map(Json.DeserializeObject(File.ReadAllText(path)));
   string engine=Field(props,"encryptedAddress") as string;string gg=Field(props,"ggEncryptedAddress") as string;
   if(string.IsNullOrEmpty(engine)||string.IsNullOrEmpty(gg))return state;
   var active=Get(gg,"/v1/loadouts/active");string id=Field(Map(Field(active,"activeLoadout")),"id") as string;
   Guid parsed;if(!Guid.TryParse(id,out parsed))return state;
   var result=Get(engine,"/v2/device/config/loadout?device=apex_pro_tkl_wireless&loadoutId="+parsed.ToString());
   var statuses=Map(Field(Map(Field(result,"loadoutConfiguration")),"statuses"));
   var battery=Map(Field(statuses,"batteryLevels"));var connection=Map(Field(statuses,"wirelessConnection"));
   bool connected=(string)Field(connection,"twoFourGHZStatus")=="connected"||(string)Field(connection,"wiredStatus")=="connected"||(string)Field(connection,"btStatus")=="connected";
   if(!connected){state.Status="Klavye bağlı değil • SteelSeries GG";return state;}
   if(!true.Equals(Field(battery,"statusKnown")))return state;
   object raw=Field(battery,"percent");if(raw==null)return state;double percent=Convert.ToDouble(raw,CultureInfo.InvariantCulture);
   if(double.IsNaN(percent)||percent<0||percent>100)return state;
   state.Percent=(int)Math.Round(percent);state.Charging=true.Equals(Field(battery,"charging"));state.Sample=DateTime.Now;state.Status="Canlı • SteelSeries GG yerel durum verisi";
  }catch(WebException error){try{File.WriteAllText(Program.DataPath("steelseries-error.txt"),error.ToString());}catch{}state.Status="SteelSeries GG servisine ulaşılamadı";}catch(Exception){state.Status="SteelSeries GG durum verisi okunamadı";}
  return state;
 }
}
