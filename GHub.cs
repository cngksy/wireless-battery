using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

public static class GHub {
 static object Field(Dictionary<string,object> data,string key){object value;return data!=null&&data.TryGetValue(key,out value)?value:null;}
 static Dictionary<string,object> Map(object value){return value as Dictionary<string,object>;}
 static async Task<Dictionary<string,object>> Get(ClientWebSocket socket,string path,CancellationToken token) {
  var json=new JavaScriptSerializer{MaxJsonLength=1024*1024};
  byte[] request=Encoding.UTF8.GetBytes(json.Serialize(new{msgId=Guid.NewGuid().ToString(),verb="GET",path=path}));
  await socket.SendAsync(new ArraySegment<byte>(request),WebSocketMessageType.Text,true,token);
  for(int attempt=0;attempt<32;attempt++)using(var buffer=new MemoryStream()) {
   var block=new byte[8192];WebSocketReceiveResult frame;
   do {frame=await socket.ReceiveAsync(new ArraySegment<byte>(block),token);if(frame.MessageType==WebSocketMessageType.Close)return null;if(buffer.Length+frame.Count>1024*1024)throw new InvalidDataException();buffer.Write(block,0,frame.Count);}while(!frame.EndOfMessage);
   var result=Map(json.DeserializeObject(Encoding.UTF8.GetString(buffer.ToArray())));
   if((string)Field(result,"path")==path)return result;
  }return null;
 }
 public static List<BatteryState> Poll(){return PollAsync().GetAwaiter().GetResult();}
 static async Task<List<BatteryState>> PollAsync() {
  var states=new List<BatteryState>();
  using(var timeout=new CancellationTokenSource(3000))using(var socket=new ClientWebSocket())try {
   socket.Options.AddSubProtocol("json");socket.Options.Proxy=null;
   await socket.ConnectAsync(new Uri("ws://127.0.0.1:9010"),timeout.Token);
   var listing=await Get(socket,"/devices/list",timeout.Token);
   var devices=Field(Map(Field(listing,"payload")),"deviceInfos") as object[];if(devices==null)return states;
   foreach(var item in devices) {
    var device=Map(item);if(device==null||!true.Equals(Field(Map(Field(device,"capabilities")),"hasBatteryStatus")))continue;
    string id=Field(device,"id") as string;if(string.IsNullOrEmpty(id))continue;
    string type=Field(device,"deviceType") as string;string kind=type=="MOUSE"?"Mouse":type=="KEYBOARD"?"Klavye":type=="HEADSET"?"Kulaklık":type=="GAMEPAD"?"Gamepad":"Aygıt";
    int pid=Convert.ToInt32(Field(device,"pid"));
    string name=Field(device,"extendedDisplayName") as string??Field(device,"displayName") as string??"Logitech";
    string key=pid==0x0ABA||pid==0x0AC4?"headset":pid==LogitechG735.Product?LogitechG735.DeviceKey:"ghub:"+(Field(device,"path") as string??id);
    if(kind=="Mouse"&&(pid==0x4074||pid==0xC53F||name.IndexOf("G305",StringComparison.OrdinalIgnoreCase)>=0))
     key=Batteries.LogitechG305Key;
    string connection=Field(device,"state") as string;
    if(connection=="INACTIVE"||connection=="DISCONNECTED"){
     states.Add(new BatteryState{Key=key,Name=name,Kind=kind,Connection=DeviceConnection.Disconnected,Stale=true,Status="G HUB"});
     continue;
    }
    var result=await Get(socket,"/battery/"+Uri.EscapeDataString(id)+"/state",timeout.Token);
    if((string)Field(Map(Field(result,"result")),"code")!="SUCCESS")continue;
    var payload=Map(Field(result,"payload"));object percentage=Field(payload,"percentage");if(percentage==null)continue;
    double raw=Convert.ToDouble(percentage);if(double.IsNaN(raw)||raw<0||raw>100)continue;
    states.Add(new BatteryState{Key=key,Name=name,Kind=kind,Percent=(int)Math.Round(raw),Charging=true.Equals(Field(payload,"charging"))||(string)Field(payload,"chargeStatus")=="CHARGING",Connection=DeviceConnection.Connected,Sample=DateTime.Now,Status="G HUB tarafından bildirilen yüzde"});
   }
  }catch(Exception){}finally{socket.Abort();}
  return states;
 }
}
