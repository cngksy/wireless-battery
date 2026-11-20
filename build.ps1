$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /win32icon:assets\WirelessBattery.ico /resource:assets\WirelessBattery.ico,WirelessBattery.Icon /win32manifest:app.manifest /out:WirelessBattery.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\UIAutomationClient.dll /r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\UIAutomationTypes.dll /r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll Native.cs Battery.cs SteelSeries.cs GHub.cs WindowsDevices.cs UserSettings.cs Taskbars.cs Widget.cs Localization.cs Autostart.cs Updates.cs App.cs RegressionChecks.cs AssemblyInfo.cs
if ($LASTEXITCODE -ne 0) { throw 'Derleme başarısız' }

