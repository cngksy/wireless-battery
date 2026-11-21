using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

// Checks GitHub Releases for a newer version. Only reads the public
// "latest release" metadata and verifies downloaded installers before launch.
public class UpdateInfo {public Version Version;public string Tag,Url,InstallerUrl,ChecksumUrl;}
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
  var info=new UpdateInfo{Version=ParseTag(tag),Tag=tag,Url=IsSafeReleaseUrl(url)?url:ReleasesPage};
  object assetsValue;
  if(data.TryGetValue("assets",out assetsValue)){
   var assets=assetsValue as System.Collections.IEnumerable;
   if(assets!=null)foreach(var item in assets){var asset=item as Dictionary<string,object>;if(asset==null)continue;
    var name=asset.ContainsKey("name")?asset["name"] as string:null;var link=asset.ContainsKey("browser_download_url")?asset["browser_download_url"] as string:null;
    if(!IsAssetUrl(link))continue;
    if(name=="WirelessBattery-Setup.exe")info.InstallerUrl=link;
    if(name=="SHA256.txt")info.ChecksumUrl=link;
   }
  }
  return info;
 }
 public static bool IsAssetUrl(string url){Uri uri;return Uri.TryCreate(url,UriKind.Absolute,out uri)&&uri.Scheme=="https"&&uri.Host=="github.com"&&uri.AbsolutePath.StartsWith("/"+Repo+"/releases/download/",StringComparison.Ordinal)&&string.IsNullOrEmpty(uri.UserInfo);}
 sealed class UpdateClient:WebClient {
  protected override WebRequest GetWebRequest(Uri address){var request=base.GetWebRequest(address);request.Timeout=60000;var http=request as HttpWebRequest;if(http!=null)http.ReadWriteTimeout=60000;return request;}
 }
 public static string InstallerHash(string manifest){
  string expected=null;
  foreach(string line in manifest.Split('\n')){var match=Regex.Match(line.Trim(),@"^([0-9a-fA-F]{64})\s+\*?WirelessBattery-Setup\.exe$");if(match.Success){if(expected!=null)throw new InvalidDataException("Duplicate installer checksum.");expected=match.Groups[1].Value;}}
  if(expected==null)throw new InvalidDataException("Installer checksum missing.");return expected;
 }
 public static bool VerifyFile(string path,string expected){
  using(var hash=System.Security.Cryptography.SHA256.Create())using(var input=File.OpenRead(path))return BitConverter.ToString(hash.ComputeHash(input)).Replace("-","").Equals(expected,StringComparison.OrdinalIgnoreCase);
 }
 public static string DownloadVerified(UpdateInfo info){
  if(!IsAssetUrl(info.InstallerUrl)||!IsAssetUrl(info.ChecksumUrl))throw new InvalidDataException("Missing official installer or checksums.");
  string folder=Path.Combine(Path.GetTempPath(),"WirelessBattery-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
  string path=Path.Combine(folder,"WirelessBattery-Setup.exe");
  try{using(var client=new UpdateClient()){
   client.Headers[HttpRequestHeader.UserAgent]="WirelessBattery-Updater";
   string expected=InstallerHash(client.DownloadString(info.ChecksumUrl));
   client.DownloadFile(info.InstallerUrl,path);
   if(!VerifyFile(path,expected))throw new InvalidDataException("Installer checksum mismatch.");
  }return path;}catch{try{File.Delete(path);Directory.Delete(folder);}catch{}throw;}
 }
}
