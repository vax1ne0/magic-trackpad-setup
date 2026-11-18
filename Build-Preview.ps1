param([string]$OutputDirectory = (Join-Path $PSScriptRoot ('panel/bin/previews/' + (Get-Date -Format yyyyMMdd-HHmmss))))
$ErrorActionPreference = 'Stop'
$preview = [IO.Path]::GetFullPath($OutputDirectory)
dotnet publish (Join-Path $PSScriptRoot panel/AmtPtpControlPanel.WinUI3.csproj) -c Release -p:PublishProfile=win-x64 -o (Join-Path $preview Panel)
if ($LASTEXITCODE) { throw 'Preview compilation failed.' }
& (Join-Path $PSScriptRoot src/Build.ps1)
foreach ($file in 'src/MagicTrackpadSetup.exe','src/TrackpadBattery.exe','panel/Resources/MagicTrackpad.ico') {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $preview
}
Write-Output (Join-Path $preview MagicTrackpadSetup.exe)
