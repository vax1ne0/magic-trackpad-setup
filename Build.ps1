$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    & ./Prepare-Payload.ps1
    dotnet publish panel/AmtPtpControlPanel.WinUI3.csproj -c Release -p:PublishProfile=win-x64
    if ($LASTEXITCODE) { throw 'Settings panel compilation failed' }
    Copy-Item panel/bin/publish/win-x64/AmtPtpControlPanel.WinUI3.exe payload/TrackpadControlPanel.exe -Force
    $sources = Get-ChildItem panel -Force | Where-Object Name -notin @('bin','obj','.vs')
    Compress-Archive -LiteralPath $sources.FullName -DestinationPath payload/PanelSource.zip -Force
    & ./src/Build.ps1
    New-Item -ItemType Directory -Force dist | Out-Null
    $wix = if ($env:WIX_EXE) { $env:WIX_EXE } else { 'wix' }
    & $wix build -arch x64 -ext WixToolset.UI.wixext -o dist/Magic-Trackpad-Setup-1.2.0-x64.msi installer/MagicTrackpadSetup.wxs
    if ($LASTEXITCODE) { throw 'MSI build failed' }
} finally { Pop-Location }


