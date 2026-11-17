using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Windows.Forms;

internal static class Setup
{
    static readonly string Root = AppDomain.CurrentDomain.BaseDirectory;


    static int InstallDriver(string infPath)
    {
        bool alreadyPresent = SetupCleanup.FindDriver() != null;
        var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "pnputil.exe"),
            "/add-driver \"" + Path.GetFullPath(infPath) + "\" /install");
        info.UseShellExecute = false;
        info.CreateNoWindow = true;
        using (var process = Process.Start(info))
        {
            process.WaitForExit();
            if (!alreadyPresent && (process.ExitCode == 0 || process.ExitCode == 3010) && SetupCleanup.FindDriver() != null)
                SetupCleanup.RecordDriver();
            return process.ExitCode == 3010 ? 0 : process.ExitCode;
        }
    }

    [STAThread] static int Main(string[] args)
    {
        try
        {
            if (args.Length == 2 && args[0] == "--remove-empty-root")
            {
                string dir = Path.GetFullPath(args[1]);
                if (Directory.Exists(dir) && (File.GetAttributes(dir) & FileAttributes.ReparsePoint) == 0 &&
                    Directory.GetFileSystemEntries(dir).Length == 0) Directory.Delete(dir);
                return 0;
            }
            if (args.Length == 1 && args[0] == "--remove-driver") { SetupCleanup.RemoveOwnedDriver(); return 0; }
            if (args.Length == 1 && args[0] == "--clear-preferences") { SetupCleanup.ClearPreferences(); return 0; }
            if (args.Length == 2 && args[0] == "--prepare-update") { SetupCleanup.PrepareUpdate(args[1]); return 0; }
            if (args.Length == 1 && args[0] == "--resume-monitor") { StartupPreference.Resume(); return 0; }
            if (args.Length == 2 && args[0] == "--stop-apps") { SetupCleanup.StopApps(args[1]); return 0; }
            if (args.Length == 2 && (args[0] == "--clean-downloads" || args[0] == "--prune-downloads"))
            {
                SetupCleanup.CleanDownloads(args[1], args[0] == "--prune-downloads");
                return 0;
            }
            if (args.Length == 2 && args[0] == "--remove-autostart")
            {
                StartupPreference.Disable(args[1]);
                return 0;
            }
            if (args.Length == 2 && args[0] == "--install-driver") return InstallDriver(args[1]);
            if (args.Length == 2 && args[0] == "--launch-app")
            {
                // ShellExecute supports the app's elevation manifest. Cancelling UAC
                // must not turn a completed installation into a setup failure.
                Process.Start(new ProcessStartInfo(Path.GetFullPath(args[1])) { UseShellExecute = true });
                return 0;
            }
            if ((args.Length == 2 || args.Length == 3) && args[0] == "--start-monitor")
            {
                StartupPreference.Enable(args[1], args.Length == 2 || args[2] != "5");
                return 0;
            }
            Process.Start(Path.Combine(Root, "Panel", "AmtPtpControlPanel.WinUI3.exe"));
            Process.Start(Path.Combine(Root, "TrackpadBattery.exe"), "--resume");
            return 0;
        }
        catch (Exception ex)
        {
            if (args.Length == 0) MessageBox.Show(ex.Message, "Magic Trackpad");
            else Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
