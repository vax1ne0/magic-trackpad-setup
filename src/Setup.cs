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
        var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "pnputil.exe"),
            "/add-driver \"" + Path.GetFullPath(infPath) + "\" /install");
        info.UseShellExecute = false;
        info.CreateNoWindow = true;
        using (var process = Process.Start(info))
        {
            process.WaitForExit();
            return process.ExitCode == 3010 ? 0 : process.ExitCode;
        }
    }

    [STAThread] static int Main(string[] args)
    {
        try
        {
            if (args.Length == 2 && args[0] == "--remove-autostart")
            {
                StartupPreference.Disable(args[1]);
                return 0;
            }
            if (args.Length == 2 && args[0] == "--install-driver") return InstallDriver(args[1]);
            if (args.Length == 2 && args[0] == "--start-monitor")
            {
                StartupPreference.Enable(args[1], true);
                return 0;
            }
            Process.Start(Path.Combine(Root, "Panel", "AmtPtpControlPanel.WinUI3.exe"));
            Process.Start(Path.Combine(Root, "TrackpadBattery.exe"));
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
