using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

// Checks GitHub Releases for a newer version. Only reads the public
// "latest release" metadata; never downloads or installs anything itself.
public class UpdateInfo {public Version Version;public string Tag,Url;}
public static class Updates {
 public const string Repo="cngksy/wireless-battery";
 public static string ReleasesPage {get{return "https://github.com/"+Repo+"/releases/latest";}}
 public static Version Current {get{var v=Assembly.GetExecutingAssembly().GetName().Version;return new Version(v.Major,v.Minor,Math.Max(0,v.Build));}}
 public static Version ParseTag(string tag) {
  if(string.IsNullOrEmpty(tag))return null;
  var m=Regex.Match(tag.Trim(),@"^[vV]?(\d+)\.(\d+)(?:\.(\d+))?$");if(!m.Success)return null;
  return new Version(int.Parse(m.Groups[1].Value),int.Parse(m.Groups[2].Value),m.Groups[3].Success?int.Parse(m.Groups[3].Value):0);
 }
 public static bool IsNewer(string tag,Version current){var v=ParseTag(tag);return v!=null&&current!=null&&v>current;}
 public static bool IsSafeReleaseUrl(string url){return url!=null&&url.StartsWith("https://github.com/"+Repo+"/releases/",StringComparison.OrdinalIgnoreCase);}
 // Returns the newer release, or null when up to date. Throws on network errors.
 public static UpdateInfo Check() {
  ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
  var request=(HttpWebRequest)WebRequest.Create("https://api.github.com/repos/"+Repo+"/releases/latest");
  request.UserAgent="WirelessBattery-UpdateCheck";request.Accept="application/vnd.github+json";request.Timeout=15000;request.ReadWriteTimeout=15000;
  string body;using(var response=(HttpWebResponse)request.GetResponse())using(var reader=new StreamReader(response.GetResponseStream()))body=reader.ReadToEnd();
  var data=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(body);
  object tagValue,urlValue,draft,pre;string tag=data.TryGetValue("tag_name",out tagValue)?tagValue as string:null;
  if(data.TryGetValue("draft",out draft)&&draft is bool&&(bool)draft)return null;
  if(data.TryGetValue("prerelease",out pre)&&pre is bool&&(bool)pre)return null;
  if(!IsNewer(tag,Current))return null;
  string url=data.TryGetValue("html_url",out urlValue)?urlValue as string:null;
  return new UpdateInfo{Version=ParseTag(tag),Tag=tag,Url=IsSafeReleaseUrl(url)?url:ReleasesPage};
 }
}
