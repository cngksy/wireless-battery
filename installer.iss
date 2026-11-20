#define MyAppName "Wireless Battery"
#define MyAppVersion "1.4.3"
#define MyAppExeName "WirelessBattery.exe"

[Setup]
AppId={{D6195BA7-D34B-43F4-96B7-A0E34ED9AEBB}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=Wireless Battery
DefaultDirName={localappdata}\Programs\Wireless Battery
DefaultGroupName=Wireless Battery
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=.
OutputBaseFilename=WirelessBattery-Setup
SetupIconFile=assets\WirelessBattery.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
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
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Wireless Battery"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue

[Icons]
Name: "{group}\Wireless Battery"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall Wireless Battery"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,Wireless Battery}"; Flags: nowait postinstall skipifsilent

[Code]
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

  { Required only when upgrading from versions older than 1.3.3. }
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#MyAppExeName}', '',
    SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(500);
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
