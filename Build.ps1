$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    & ./src/Build.ps1
    New-Item -ItemType Directory -Force dist | Out-Null
    $wix = if ($env:WIX_EXE) { $env:WIX_EXE } else { 'wix' }
    & $wix build -arch x64 -ext WixToolset.UI.wixext -o dist/Magic-Trackpad-Setup-1.0.0-x64.msi installer/MagicTrackpadSetup.wxs
    if ($LASTEXITCODE) { throw 'MSI build failed' }
} finally { Pop-Location }
