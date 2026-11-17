$ErrorActionPreference = 'Stop'
$root = $env:TRACKPAD_UPDATE_ROOT
$file = $env:TRACKPAD_UPDATE_FILE
$panel = Join-Path $root 'Panel\AmtPtpControlPanel.WinUI3.exe'
$tray = Join-Path $root 'TrackpadBattery.exe'
$session = [Diagnostics.Process]::GetCurrentProcess().SessionId
$hadTray = $false
try {
    $parent = Get-Process -Id $env:TRACKPAD_PANEL_PID -ErrorAction SilentlyContinue
    if ($parent -and $parent.Path -eq $panel -and !$parent.WaitForExit(20000)) { throw 'The settings window is still open.' }
    $lock = [IO.File]::Open($file, 'Open', 'Read', 'Read')
    try {
        if ((Get-FileHash -InputStream $lock -Algorithm SHA256).Hash -ne $env:TRACKPAD_UPDATE_SHA) { throw 'The update could not be verified.' }
        $monitors = @(Get-Process TrackpadBattery -ErrorAction SilentlyContinue | Where-Object { $_.SessionId -eq $session -and $_.Path -eq $tray })
        $hadTray = $monitors.Count -gt 0
        foreach ($process in $monitors) { $process.Kill(); $process.WaitForExit() }
        $installer = Join-Path ([Environment]::SystemDirectory) 'msiexec.exe'
        $result = Start-Process $installer -ArgumentList @('/i', ('"' + $file + '"'), '/quiet', '/norestart', 'ADDLOCAL=Main,App') -Wait -PassThru
        if ($result.ExitCode -notin 0,3010) { throw "Windows Installer returned $($result.ExitCode)." }
        if ($result.ExitCode -eq 3010) {
            Add-Type -AssemblyName System.Windows.Forms
            [void][Windows.Forms.MessageBox]::Show('Restart Windows to finish updating Magic Trackpad.', 'Magic Trackpad')
        }
    } finally { $lock.Dispose() }
} catch {
    Add-Type -AssemblyName System.Windows.Forms
    [void][Windows.Forms.MessageBox]::Show(("The update did not finish. " + $_.Exception.Message), 'Magic Trackpad')
} finally {
    try {
        [IO.File]::Delete($file)
        [IO.Directory]::Delete([IO.Path]::GetDirectoryName($file))
    } catch [IO.IOException] { }
    if ($env:TRACKPAD_BACKGROUND -ne "1" -and (Test-Path -LiteralPath $panel)) { Start-Process -FilePath $panel }
    if (($hadTray -or $env:TRACKPAD_START_TRAY -eq "1") -and (Test-Path -LiteralPath $tray)) { Start-Process -FilePath $tray -ArgumentList "--resume" -WindowStyle Hidden }
}
