$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
$publishRoot = Join-Path $PSScriptRoot 'panel/bin/publish'
$panelOutput = Join-Path $publishRoot ([Guid]::NewGuid().ToString())
try {
    $version = ([xml](Get-Content panel/AmtPtpControlPanel.WinUI3.csproj -Raw)).Project.PropertyGroup.Version
    & ./Prepare-Payload.ps1
    dotnet publish panel/AmtPtpControlPanel.WinUI3.csproj -c Release -p:PublishProfile=win-x64 -o $panelOutput
    if ($LASTEXITCODE) { throw 'Settings panel compilation failed' }
    & ./installer/PanelFiles.ps1 $panelOutput
    $sources = Get-ChildItem panel -Force | Where-Object Name -notin @('bin','obj','.vs')
    Compress-Archive -LiteralPath $sources.FullName -DestinationPath payload/PanelSource.zip -Force
    & ./src/Build.ps1
    & ./installer/Build-Branding.ps1
    New-Item -ItemType Directory -Force dist | Out-Null
    $wix = if ($env:WIX_EXE) { $env:WIX_EXE } else { 'wix' }
    & $wix build -arch x64 -d "ProductVersion=$version" -ext WixToolset.UI.wixext -o "dist/Magic-Trackpad-Setup-$version-x64.msi" -loc installer/Setup.en-us.wxl installer/MagicTrackpadSetup.wxs payload/PanelFiles.wxs
    if ($LASTEXITCODE) { throw 'MSI build failed' }
    $installerName = "Magic-Trackpad-Setup-$version-x64.msi"
    $digest = (Get-FileHash (Join-Path 'dist' $installerName) -Algorithm SHA256).Hash.ToLowerInvariant()
    "$digest  $installerName" | Set-Content dist/SHA256SUMS.txt -Encoding ascii
} finally {
    try {
        if (Test-Path -LiteralPath $panelOutput) {
            $staging = Get-Item -LiteralPath $panelOutput
            if ($staging.Parent.FullName -eq [IO.Path]::GetFullPath($publishRoot) -and
                !($staging.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                Remove-Item -LiteralPath $staging.FullName -Recurse -Force
            }
        }
    } finally { Pop-Location }
}
