$ErrorActionPreference = 'Stop'
$root = $env:TRACKPAD_UPDATE_ROOT
$file = $env:TRACKPAD_UPDATE_FILE
$panel = Join-Path $root 'Panel\AmtPtpControlPanel.WinUI3.exe'
$tray = Join-Path $root 'TrackpadBattery.exe'
$session = [Diagnostics.Process]::GetCurrentProcess().SessionId
$hadTray = $false
$installed = $false
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
        $installed = $true
        if ($result.ExitCode -eq 3010) {
            Add-Type -AssemblyName System.Windows.Forms
            [void][Windows.Forms.MessageBox]::Show('Restart Windows to finish updating Magic Trackpad.', 'Magic Trackpad')
        }
    } finally { $lock.Dispose() }
} catch {
    Add-Type -AssemblyName System.Windows.Forms
    [void][Windows.Forms.MessageBox]::Show(("The update did not finish. " + $_.Exception.Message), 'Magic Trackpad')
} finally {
    # A scanner can briefly keep the MSI open after Windows Installer exits.
    for ($attempt = 0; $attempt -lt 6; $attempt++) {
        try {
            [IO.File]::Delete($file)
            [IO.Directory]::Delete([IO.Path]::GetDirectoryName($file))
            break
        } catch [IO.DirectoryNotFoundException] { break }
        catch [IO.IOException] {
            $code = $_.Exception.GetBaseException().HResult -band 0xffff
            if ($code -notin 32,33 -or $attempt -eq 5) { break }
            Start-Sleep -Milliseconds 250
        } catch [UnauthorizedAccessException] { break }
    }
    $env:TRACKPAD_UPDATE_SHA = $null
    if (($hadTray -or $env:TRACKPAD_START_TRAY -eq "1") -and (Test-Path -LiteralPath $tray)) { Start-Process -FilePath $tray -ArgumentList "--resume" -WindowStyle Hidden }
    if ($env:TRACKPAD_BACKGROUND -ne '1' -and (Test-Path -LiteralPath $panel)) {
        Add-Type -TypeDefinition 'using System.Runtime.InteropServices; public static class UpdateFocus { [DllImport("user32")] public static extern bool AllowSetForegroundWindow(int id); }'
        $launch = @{ FilePath = $panel; WindowStyle = 'Normal'; PassThru = $true }
        if ($installed) { $launch.ArgumentList = '--updated' }
        $settings = Start-Process @launch
        try { [void][UpdateFocus]::AllowSetForegroundWindow($settings.Id) } finally { $settings.Dispose() }
    }
}
