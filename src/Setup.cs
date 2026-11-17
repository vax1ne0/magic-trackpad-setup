using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Windows.Forms;

internal static class Setup
{
    static readonly string Root = AppDomain.CurrentDomain.BaseDirectory;
    const string TaskName = "Magic Trackpad Battery Monitor";

    static int InstallDriver()
    {
        var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "pnputil.exe"),
            "/add-driver \"" + Path.Combine(Root, "Driver", "AmtPtpDevice.inf") + "\" /install");
        info.UseShellExecute = false;
        info.CreateNoWindow = true;
        using (var process = Process.Start(info))
        {
            process.WaitForExit();
            return process.ExitCode == 3010 ? 0 : process.ExitCode;
        }
    }

    static void StartForUser(string sid)
    {
        // A system-wide deployment has no interactive recipient at install time.
        var user = new SecurityIdentifier(sid);
        if (user.IsWellKnown(WellKnownSidType.LocalSystemSid)) return;
        string account = user.Translate(typeof(NTAccount)).Value;
        dynamic service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));
        service.Connect();
        dynamic folder = service.GetFolder("\\");
        dynamic task = service.NewTask(0);
        task.RegistrationInfo.Description = "Display the Magic Trackpad battery in the taskbar.";
        task.Principal.UserId = account;
        task.Principal.LogonType = 3;
        task.Principal.RunLevel = 1;
        task.Settings.DisallowStartIfOnBatteries = false;
        task.Settings.StopIfGoingOnBatteries = false;
        task.Settings.ExecutionTimeLimit = "PT0S";
        task.Settings.MultipleInstances = 2;
        task.Triggers.Create(9).UserId = account;
        task.Actions.Create(0).Path = Path.Combine(Root, "TrackpadBattery.exe");
        dynamic saved = folder.RegisterTaskDefinition(TaskName, task, 6, account, null, 3, null);
        saved.Run(null);
    }

    [STAThread] static int Main(string[] args)
    {
        try
        {
            if (args.Length == 1 && args[0] == "--install-driver") return InstallDriver();
            if (args.Length == 2 && args[0] == "--start-monitor")
            {
                StartForUser(args[1]);
                return 0;
            }
            Process.Start(Path.Combine(Root, "TrackpadControlPanel.exe"));
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
