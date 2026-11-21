using System.Globalization;

public static class L {
 static bool Turkish {get{return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName=="tr";}}
 static string Pick(string tr,string en){return Turkish?tr:en;}
 public static string Refresh {get{return Pick("Şimdi yenile","Refresh now");}}
 public static string TaskbarIndicator {get{return Pick("Görev çubuğu göstergesi","Taskbar indicator");}}
 public static string StartWithWindows {get{return Pick("Windows açılırken başlat","Start with Windows");}}
 public static string Exit {get{return Pick("Çıkış","Exit");}}
 public static string NoDevices {get{return Pick("Pil bildiren cihaz bulunamadı","No battery-reporting devices found");}}
 public static string Scanning {get{return Pick("taranıyor","scanning");}}
 public static string ReadError {get{return Pick("okuma hatası","read error");}}
 public static string DeviceCount(int count){return count+" "+Pick("cihaz","device"+(count==1?"":"s"));}
 public static string BatteryLabel(bool charging){return charging?Pick("Şarj Ediliyor: ","Charging: "):Pick("Pil Yüzdesi: ","Battery: ");}
 public static string TimeLabel {get{return Pick("Saat: ","Time: ");}}
 public static string By(string provider){return Turkish?provider+" tarafından":"Via "+provider;}
 public static string LowBattery(string level){return Pick("Pil az: ","Low battery: ")+level;}
 public static string CheckUpdates {get{return Pick("Güncelleştirmeleri kontrol et","Check for updates");}}
 public static string DownloadUpdate(string v){return Pick("Yeni sürüm: "+v+" — Yükle","New version: "+v+" — Install");}
 public static string UpdateAvailable(string v){return Pick("Yeni sürüm hazır: "+v+". İndirip kurmak için tıklayın.","Version "+v+" is available. Click to download and install.");}
 public static string UpToDate(string v){return Pick("Zaten güncel sürümü kullanıyorsunuz ("+v+").","You are already on the latest version ("+v+").");}
 public static string UpdateCompleted(string v){return Pick("Güncellendi! Yeni sürüm: "+v,"Updated successfully! Version: "+v);}
 public static string InstallingUpdate {get{return Pick("Güncelleme indiriliyor ve doğrulanıyor. Kurulum bitince uygulama yeniden açılacak.","Downloading and verifying the update. The app will restart after installation.");}}
 public static string InstallUpdateError {get{return Pick("Güncelleme kurulamadı. Ayrıntılar update-error.txt dosyasında.","Could not install the update. See update-error.txt for details.");}}
 public static string UpdateError {get{return Pick("Güncelleme denetlenemedi. İnternet bağlantısını kontrol edin.","Could not check for updates. Check your internet connection.");}}
 public static string DeviceReport {get{return Pick("Cihaz raporunu kopyala","Copy device report");}}
 public static string DeviceReportCopied {get{return Pick("Cihaz raporu panoya kopyalandı. Ctrl+V ile yapıştırabilirsiniz.","Device report copied to clipboard. Paste it with Ctrl+V.");}}
 public static string DeviceReportListening(int s){return Pick("Cihaz raporu hazırlanıyor ("+s+" sn). Bu sırada cihazları kullanmaya devam edebilirsiniz.","Preparing the device report ("+s+" s). You can keep using your devices.");}
 public static string DeviceReportError {get{return Pick("Cihaz raporu panoya kopyalanamadı.","Could not copy the device report.");}}
 public static string StartupError {get{return Pick("Windows başlangıç ayarı değiştirilemedi.","Could not change the Windows startup setting.");}}
}
