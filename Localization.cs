using System.Globalization;

public static class L {
 static string Pick(string tr,string en,string es) {
  switch(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName) {
   case "tr":return tr;
   case "es":return es;
   default:return en;
  }
 }
 public static string Refresh {get{return Pick("Şimdi yenile","Refresh now","Actualizar ahora");}}
 public static string TaskbarIndicator {get{return Pick("Görev çubuğu göstergesi","Taskbar indicator","Indicador en la barra de tareas");}}
 public static string TaskbarDisplay {get{return Pick("Gösterilecek ekran","Show on display","Mostrar en la pantalla");}}
 public static string AllDisplays {get{return Pick("Tüm ekranlar","All displays","Todas las pantallas");}}
 public static string DisplayOption(string label){return Pick("Ekran ","Display ","Pantalla ")+label;}
 public static string DisplayDisconnected(string label){return label+Pick(" (bağlı değil)"," (disconnected)"," (desconectada)");}
 public static string RememberLastCharge {get{return Pick("Son pil yüzdesini hatırla","Remember last charge","Recordar el último nivel de batería");}}
 public static string LastCharge {get{return Pick("Son pil bilgisi: ","Last charge: ","Último nivel de batería: ");}}
 public static string Disconnected {get{return Pick("Bağlı değil","Disconnected","Sin conexión");}}
 public static string ConnectionUnknown {get{return Pick("Bağlantı doğrulanamadı","Connection not confirmed","Conexión sin confirmar");}}
 public static string StartWithWindows {get{return Pick("Windows açılırken başlat","Start with Windows","Iniciar con Windows");}}
 public static string Exit {get{return Pick("Çıkış","Exit","Salir");}}
 public static string NoDevices {get{return Pick("Pil bildiren cihaz bulunamadı","No battery-reporting devices found","No se encontraron dispositivos que informen del nivel de batería");}}
 public static string SystemBattery {get{return Pick("Bilgisayar pili","PC battery","Batería del equipo");}}
 public static string BluetoothDevice {get{return Pick("Bluetooth aygıtı","Bluetooth device","Dispositivo Bluetooth");}}
 public static string XInputController(uint number){return Pick("Xbox / XInput Gamepad ","Xbox / XInput Gamepad ","Mando Xbox / XInput ")+number;}
 public static string BatteryLevel(int level) {
  switch(level) {
   case 0:return Pick("Boş","Empty","Agotada");
   case 1:return Pick("Az","Low","Baja");
   case 2:return Pick("Orta","Medium","Media");
   default:return Pick("Dolu","Full","Llena");
  }
 }
 public static string Scanning {get{return Pick("taranıyor","scanning","buscando dispositivos");}}
 public static string ReadError {get{return Pick("okuma hatası","read error","error de lectura");}}
 public static string DeviceCount(int count){return count+" "+Pick("cihaz",count==1?"device":"devices",count==1?"dispositivo":"dispositivos");}
 public static string BatteryLabel(bool charging){return charging?Pick("Şarj Ediliyor: ","Charging: ","Cargando: "):Pick("Pil Yüzdesi: ","Battery: ","Batería: ");}
 public static string TimeLabel {get{return Pick("Saat: ","Time: ","Hora: ");}}
 public static string By(string provider){return Pick(provider+" tarafından","Via "+provider,"Fuente: "+provider);}
 public static string LowBattery(string level){return Pick("Pil az: ","Low battery: ","Batería baja: ")+level;}
 public static string CheckUpdates {get{return Pick("Güncelleştirmeleri kontrol et","Check for updates","Buscar actualizaciones");}}
 public static string DownloadUpdate(string v){return Pick("Yeni sürüm: "+v+" — Yükle","New version: "+v+" — Install","Nueva versión: "+v+" — Instalar");}
 public static string UpdateAvailable(string v){return Pick("Yeni sürüm hazır: "+v+". İndirip kurmak için tıklayın.","Version "+v+" is available. Click to download and install.","La versión "+v+" está disponible. Haz clic para descargarla e instalarla.");}
 public static string UpToDate(string v){return Pick("Zaten güncel sürümü kullanıyorsunuz ("+v+").","You are already on the latest version ("+v+").","Ya tienes la última versión ("+v+").");}
 public static string UpdateCompleted(string v){return Pick("Güncellendi! Yeni sürüm: "+v,"Updated successfully! Version: "+v,"Actualización completada. Versión "+v+".");}
 public static string InstallingUpdate {get{return Pick("Güncelleme indiriliyor ve doğrulanıyor. Kurulum bitince uygulama yeniden açılacak.","Downloading and verifying the update. The app will restart after installation.","Descargando y verificando la actualización. La aplicación se reiniciará después de la instalación.");}}
 public static string InstallUpdateError {get{return Pick("Güncelleme kurulamadı. Ayrıntılar update-error.txt dosyasında.","Could not install the update. See update-error.txt for details.","No se pudo instalar la actualización. Consulta update-error.txt para más detalles.");}}
 public static string UpdateError {get{return Pick("Güncelleme denetlenemedi. İnternet bağlantısını kontrol edin.","Could not check for updates. Check your internet connection.","No se pudieron buscar actualizaciones. Comprueba tu conexión a Internet.");}}
 public static string DeviceReport {get{return Pick("Cihaz raporunu kopyala","Copy device report","Copiar informe del dispositivo");}}
 public static string DeviceReportCopied {get{return Pick("Cihaz raporu panoya kopyalandı. Ctrl+V ile yapıştırabilirsiniz.","Device report copied to clipboard. Paste it with Ctrl+V.","Informe del dispositivo copiado al portapapeles. Pégalo con Ctrl+V.");}}
 public static string DeviceReportListening(int s){return Pick("Cihaz raporu hazırlanıyor ("+s+" sn). Bu sırada cihazları kullanmaya devam edebilirsiniz.","Preparing the device report ("+s+" s). You can keep using your devices.","Preparando el informe del dispositivo ("+s+" s). Puedes seguir usando tus dispositivos.");}
 public static string DeviceReportError {get{return Pick("Cihaz raporu panoya kopyalanamadı.","Could not copy the device report.","No se pudo copiar el informe del dispositivo.");}}
 public static string MouseReceiverWaiting {get{return Pick("Son ölçüm • USB alıcısı bağlı","Last reading • USB receiver present","Última lectura • Receptor USB conectado");}}
 public static string StartupError {get{return Pick("Windows başlangıç ayarı değiştirilemedi.","Could not change the Windows startup setting.","No se pudo cambiar la opción de inicio con Windows.");}}
}
