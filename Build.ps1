$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    & ./Prepare-Payload.ps1
    $panelOutput = Join-Path $PSScriptRoot ("panel/bin/publish/" + [Guid]::NewGuid())
    dotnet publish panel/AmtPtpControlPanel.WinUI3.csproj -c Release -p:PublishProfile=win-x64 -o $panelOutput
    if ($LASTEXITCODE) { throw 'Settings panel compilation failed' }
    & ./installer/PanelFiles.ps1 $panelOutput
    $sources = Get-ChildItem panel -Force | Where-Object Name -notin @('bin','obj','.vs')
    Compress-Archive -LiteralPath $sources.FullName -DestinationPath payload/PanelSource.zip -Force
    & ./src/Build.ps1
    New-Item -ItemType Directory -Force dist | Out-Null
    $wix = if ($env:WIX_EXE) { $env:WIX_EXE } else { 'wix' }
    & $wix build -arch x64 -ext WixToolset.UI.wixext -o dist/Magic-Trackpad-Setup-1.5.0-x64.msi installer/MagicTrackpadSetup.wxs payload/PanelFiles.wxs
    if ($LASTEXITCODE) { throw 'MSI build failed' }
} finally { Pop-Location }
