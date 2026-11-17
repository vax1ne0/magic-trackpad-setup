$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    $common = @('/nologo','/target:winexe','/platform:x64','/optimize+','/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/r:Microsoft.CSharp.dll','/win32manifest:app.manifest','/win32icon:TrackpadBattery.ico')
    & $compiler @common /out:TrackpadBattery.exe TrackpadBattery.cs BatteryArt.cs ..\panel\DeviceInterop.cs ..\panel\AppPreferences.cs ..\panel\StartupPreference.cs
    if ($LASTEXITCODE) { throw 'Battery app compilation failed' }
    & $compiler @common /out:MagicTrackpadSetup.exe Setup.cs ..\panel\StartupPreference.cs
    if ($LASTEXITCODE) { throw 'Setup compilation failed' }
} finally { Pop-Location }
