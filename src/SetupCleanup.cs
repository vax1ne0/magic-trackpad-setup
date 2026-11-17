using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32;

internal static class SetupCleanup
{
    // Fingerprints of the v2.3 payload validated by Prepare-Payload.ps1.
    const string InfHash = "1194ECD4904BE32564457549976F7753B5C353AA0EF86CF4A9BAC1E26089F58E";
    const string CatalogHash = "F3F370752BAA3835B21966295A24ED7034AA82BA80C02AF3BF641BCE8C72D6CE";
    const string OwnerKey = @"SOFTWARE\Magic Trackpad Setup\DriverOwnership";

    [DllImport("newdev.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool DiUninstallDriverW(IntPtr parent, string inf, uint flags, out bool reboot);

    static bool Matches(string path, string hash)
    {
        if (!File.Exists(path)) return false;
        using (var stream = File.OpenRead(path))
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "") == hash;
    }

    internal static string FindDriver()
    {
        var store = Path.Combine(Environment.SystemDirectory, @"DriverStore\FileRepository");
        foreach (string dir in Directory.GetDirectories(store, "amtptpdevice.inf_amd64_*"))
        {
            string inf = Path.Combine(dir, "AmtPtpDevice.inf");
            if (Matches(inf, InfHash) && Matches(Path.Combine(dir, "amtptpdevice.cat"), CatalogHash)) return inf;
        }
        return null;
    }

    internal static void RecordDriver()
    {
        using (var key = Registry.LocalMachine.CreateSubKey(OwnerKey)) key.SetValue(CatalogHash, 1);
    }

    internal static void RemoveOwnedDriver()
    {
        using (var key = Registry.LocalMachine.OpenSubKey(OwnerKey, true))
        {
            if (key == null || !Equals(key.GetValue(CatalogHash), 1)) return;
            string inf = FindDriver();
            bool reboot;
            if (inf != null && !DiUninstallDriverW(IntPtr.Zero, inf, 0, out reboot))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            foreach (string path in new[] {
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\WUDF\Services\AmtPtpDeviceUsbUm\Parameters",
                @"SYSTEM\CurrentControlSet\Services\AmtPtpHidFilter\Parameters" })
            using (var parameters = Registry.LocalMachine.OpenSubKey(path, true))
            {
                if (parameters == null) continue;
                foreach (string value in new[] { "ButtonDisabled", "FeedbackClick", "FeedbackRelease",
                    "StopPressure", "StopSize", "IgnoreButtonFinger", "IgnoreNearFingers", "PalmRejection",
                    "InvertMovement", "AllowNonAdminAccess" })
                    parameters.DeleteValue(value, false);
            }
            key.DeleteValue(CatalogHash);
        }
        DeleteEmptyKey(Registry.LocalMachine, OwnerKey);
        DeleteEmptyKey(Registry.LocalMachine, @"SOFTWARE\Magic Trackpad Setup");
    }

    static void DeleteEmptyKey(RegistryKey hive, string name)
    {
        using (var key = hive.OpenSubKey(name))
            if (key == null || key.ValueCount != 0 || key.SubKeyCount != 0) return;
        hive.DeleteSubKey(name, false);
    }

    internal static void ClearPreferences()
    {
        const string name = @"Software\AmtPtpControlPanel";
        using (var key = Registry.CurrentUser.OpenSubKey(name, true))
        {
            if (key == null) return;
            foreach (string value in new[] { "Language", "IndependentRelease", "LowBatteryAlert" })
                key.DeleteValue(value, false);
        }
        DeleteEmptyKey(Registry.CurrentUser, name);
    }

    internal static void PrepareUpdate(string root)
    {
        bool running = false;
        foreach (string relative in new[] { "TrackpadBattery.exe", @"Panel\AmtPtpControlPanel.WinUI3.exe" })
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(relative)))
        using (process)
        {
            try
            {
                if (string.Equals(process.MainModule.FileName, Path.GetFullPath(Path.Combine(root, relative)),
                    StringComparison.OrdinalIgnoreCase)) running = true;
            }
            catch (Win32Exception) { running = true; } // Elevated app requires an elevated check.
            catch (InvalidOperationException) { }
        }
        if (!running) return;
        using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
        {
            if (new System.Security.Principal.WindowsPrincipal(identity).IsInRole(
                System.Security.Principal.WindowsBuiltInRole.Administrator))
            {
                StopApps(root);
                return;
            }
        }
        // MSI extracts embedded executables with a .tmp extension. CreateProcess can
        // run them, while ShellExecute cannot elevate them by file association.
        string executable = System.Reflection.Assembly.GetExecutingAssembly().Location.Replace("'", "''");
        string arguments = ("--stop-apps \"" + Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + "\\.\"").Replace("'", "''");
        string command = "$p = New-Object Diagnostics.ProcessStartInfo; $p.FileName = '" + executable +
            "'; $p.Arguments = '" + arguments + "'; $p.UseShellExecute = $false; $p.CreateNoWindow = $true; " +
            "$c = [Diagnostics.Process]::Start($p); $c.WaitForExit(); exit $c.ExitCode";
        string encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(command));
        var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"),
            "-NoProfile -NonInteractive -EncodedCommand " + encoded)
        { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
        using (var helper = Process.Start(info))
        {
            helper.WaitForExit();
            if (helper.ExitCode != 0) throw new IOException("Could not close Magic Trackpad for the update.");
        }
    }

    internal static void StopApps(string root)
    {
        foreach (string relative in new[] { "TrackpadBattery.exe", @"Panel\AmtPtpControlPanel.WinUI3.exe" })
        {
            string expected = Path.GetFullPath(Path.Combine(root, relative));
            foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(relative)))
            using (process)
            {
                try
                {
                    if (!string.Equals(process.MainModule.FileName, expected, StringComparison.OrdinalIgnoreCase)) continue;
                    if (process.CloseMainWindow() && process.WaitForExit(2500)) continue;
                    process.Kill();
                    if (!process.WaitForExit(10000)) throw new IOException("Could not stop " + relative);
                }
                catch (InvalidOperationException) { if (!process.HasExited) throw; }
            }
        }
    }

    static bool IsLink(string path)
    {
        return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    }

    internal static void CleanDownloads(string root, bool upgrading)
    {
        root = Path.GetFullPath(root);
        if (!Directory.Exists(root) || IsLink(root)) return;
        // The 1.0 installer unpacked this fixed list into stage-{GUID}.
        foreach (string stage in Directory.GetDirectories(root, "stage-*"))
        {
            Guid id;
            if (!Guid.TryParseExact(Path.GetFileName(stage).Substring(6), "N", out id) || IsLink(stage)) continue;
            foreach (string name in new[] { "AmtPtpDevice.inf", "amtptpdevice.cat", "AmtPtpDeviceUsbUm.dll",
                "AmtPtpHidFilter.sys", "AmtPtpControlPanel.WinUI3-amd64.exe", "MT2FW11-amd64-20261111-MSSigned.zip" })
            {
                string file = Path.Combine(stage, name);
                if (File.Exists(file) && !IsLink(file)) File.Delete(file);
            }
            if (Directory.GetFileSystemEntries(stage).Length == 0) Directory.Delete(stage);
        }
        string cache = Path.Combine(root, "Updates");
        if (!Directory.Exists(cache) || IsLink(cache)) return;
        foreach (string dir in Directory.GetDirectories(cache))
        {
            Guid id;
            if (!Guid.TryParseExact(Path.GetFileName(dir), "N", out id) || IsLink(dir)) continue;
            string msi = Path.Combine(dir, "update.msi");
            try
            {
                string temporary = Path.Combine(dir, "pending.tmp");
                if (File.Exists(temporary) && !IsLink(temporary)) File.Delete(temporary);
                if (File.Exists(msi) && !IsLink(msi)) File.Delete(msi);
                if (Directory.GetFileSystemEntries(dir).Length == 0) Directory.Delete(dir);
            }
            catch (IOException ex)
            {
                // The active updater retains a read lock and clears its download after MSI exits.
                int code = ex.HResult & 0xffff;
                if (!upgrading || (code != 32 && code != 33)) throw;
            }
        }
    }
}
