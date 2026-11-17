$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    $common = @('/nologo','/target:winexe','/platform:x64','/optimize+','/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/r:Microsoft.CSharp.dll','/win32manifest:app.manifest','/win32icon:TrackpadBattery.ico')
    $version = ([xml](Get-Content ../panel/AmtPtpControlPanel.WinUI3.csproj -Raw)).Project.PropertyGroup.Version
    $generated = New-Item -ItemType Directory -Path ../payload -Force
    $info = Join-Path $generated.FullName 'AppVersion.cs'
    ('[assembly:System.Reflection.AssemblyFileVersion("{0}.0")]' -f $version) | Set-Content $info
    & $compiler @common /out:TrackpadBattery.exe $info TrackpadBattery.cs BatteryArt.cs ..\panel\DeviceInterop.cs ..\panel\AppPreferences.cs ..\panel\StartupPreference.cs
    if ($LASTEXITCODE) { throw 'Battery app compilation failed' }
    & $compiler @common /out:MagicTrackpadSetup.exe $info Setup.cs ..\panel\StartupPreference.cs
    if ($LASTEXITCODE) { throw 'Setup compilation failed' }
} finally { Pop-Location }
