$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$winmd = Join-Path $env:WINDIR 'System32\WinMetadata'
$winrt = Join-Path (Split-Path $compiler) 'System.Runtime.WindowsRuntime.dll'
$windowsRefs = @('Windows.Devices.winmd','Windows.Foundation.winmd','Windows.Gaming.winmd','Windows.System.winmd') | ForEach-Object { '/r:' + (Join-Path $winmd $_) }
$runtime = Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL\System.Runtime\v4.0_4.0.0.0__b03f5f7f11d50a3a\System.Runtime.dll'
& $compiler @windowsRefs "/r:$winrt" "/r:$runtime" /nologo /target:winexe /platform:x64 /optimize+ /win32icon:assets\WirelessBattery.ico /resource:assets\WirelessBattery.ico,WirelessBattery.Icon /win32manifest:app.manifest /out:WirelessBattery.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\UIAutomationClient.dll /r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\UIAutomationTypes.dll /r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll Native.cs Battery.cs Razer.cs HyperX.cs Aula.cs AsusRog.cs EightBitDo.cs LogitechG735.cs LogitechReceivers.cs PlayStation.cs SteelSeries.cs GHub.cs WindowsDevices.cs WindowsRuntimeBattery.cs IpiPiao.cs UserSettings.cs Taskbars.cs Widget.cs Localization.cs Autostart.cs Updates.cs App.cs RegressionChecks.cs AssemblyInfo.cs
if ($LASTEXITCODE -ne 0) { throw 'Derleme başarısız' }

