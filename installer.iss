#define MyAppName "Wireless Battery"
#define MyAppVersion "1.4.11"
#define MyAppExeName "WirelessBattery.exe"
#ifndef SetupCompression
  #define SetupCompression "none"
#endif
#ifndef SetupOutput
  #define SetupOutput "WirelessBattery-Setup"
#endif

[Setup]
AppId={{D6195BA7-D34B-43F4-96B7-A0E34ED9AEBB}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=Wireless Battery
AppPublisherURL=https://github.com/cngksy/wireless-battery
AppSupportURL=https://github.com/cngksy/wireless-battery/issues
AppUpdatesURL=https://github.com/cngksy/wireless-battery/releases
VersionInfoVersion={#MyAppVersion}.0
VersionInfoProductVersion={#MyAppVersion}.0
VersionInfoCompany=Wireless Battery
VersionInfoDescription=Wireless Battery Setup
VersionInfoProductName=Wireless Battery
VersionInfoCopyright=GPL-3.0-or-later
DefaultDirName={localappdata}\Programs\Wireless Battery
DefaultGroupName=Wireless Battery
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=.
OutputBaseFilename={#SetupOutput}
SetupIconFile=assets\WirelessBattery.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression={#SetupCompression}
#if SetupCompression != "none"
SolidCompression=yes
#endif
WizardStyle=modern
CloseApplications=no
RestartApplications=no
LicenseFile=LICENSE

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Files]
Source: "WirelessBattery.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Registry]
; The app itself manages its startup entry; the installer only removes it on uninstall.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "Wireless Battery"; Flags: uninsdeletevalue

[Icons]
Name: "{group}\Wireless Battery"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall Wireless Battery"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,Wireless Battery}"; Flags: nowait postinstall skipifsilent

Filename: "{app}\{#MyAppExeName}"; Parameters: "--updated-from={param:FROMVERSION|}"; Flags: nowait; Check: IsAutomaticUpdate

[Code]
function IsAutomaticUpdate(): Boolean;
begin
  Result := ExpandConstant('{param:AUTORESTART|0}') <> '0';
end;

procedure StopWirelessBattery();
var
  ResultCode: Integer;
begin
  { New versions exit cooperatively and remove their tray/taskbar windows. }
  if FileExists(ExpandConstant('{app}\{#MyAppExeName}')) then
  begin
    Exec(ExpandConstant('{app}\{#MyAppExeName}'), '--exit', '', SW_HIDE,
      ewWaitUntilTerminated, ResultCode);
    Sleep(750);
  end;

  { Versions since 1.3.3 exit cooperatively; nothing is force-killed. }
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopWirelessBattery();
  Result := '';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    StopWirelessBattery();
end;
