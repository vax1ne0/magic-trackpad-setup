$ErrorActionPreference='Stop'
$dir=New-Item -ItemType Directory -Force (Join-Path $PSScriptRoot 'payload')
$archive=Join-Path $dir 'driver.zip'
$url='https://github.com/vitoplantamura/MagicTrackpad2ForWindows/releases/download/v2.3/MT2FW11-amd64-20261111-MSSigned.zip'
if (!(Test-Path $archive)) { Invoke-WebRequest $url -OutFile $archive }
if ((Get-FileHash $archive).Hash -ne 'CBCC67302E03B83733A3C4E42B1667E6AE3BFE333B33879CDE2F4115A6608121') { throw 'Unexpected driver release hash' }
Expand-Archive $archive (Join-Path $dir extracted) -Force
Copy-Item "$dir/extracted/AMD64/*" $dir -Force
$sig=Get-AuthenticodeSignature "$dir/amtptpdevice.cat"
if ($sig.Status -ne 'Valid' -or $sig.SignerCertificate.Subject -notlike '*Microsoft Windows Hardware Compatibility Publisher*') { throw 'Driver catalog signature did not verify' }
$commit='3392291af4814efb33d0d2b8b99948eca33a77a1'
if (!(Test-Path "$dir/UpstreamSource.zip")) { Invoke-WebRequest "https://codeload.github.com/vitoplantamura/MagicTrackpad2ForWindows/zip/$commit" -OutFile "$dir/UpstreamSource.zip" }
if ((Get-FileHash "$dir/UpstreamSource.zip").Hash -ne 'D30AB31F4D59F7432EAF316F18551D61EA561630F77A9EC68221169F867DD804') { throw 'Unexpected upstream source hash' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$z=[IO.Compression.ZipFile]::OpenRead("$dir/UpstreamSource.zip")
try { [IO.Compression.ZipFileExtensions]::ExtractToFile($z.GetEntry("MagicTrackpad2ForWindows-$commit/LICENSE"),"$dir/UpstreamLicense.txt",$true) } finally { $z.Dispose() }
