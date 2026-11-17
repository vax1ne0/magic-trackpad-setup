using System;
using System.IO;
using System.Security.Principal;
internal static class StartupPreference
{
    static readonly string Root = Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
    const string TaskName = "Magic Trackpad Battery Monitor";
    internal static void Enable(string sid, bool startNow)
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
        if (startNow) saved.Run(null);
    }

    internal static bool Enabled()
    {
        dynamic service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));
        service.Connect();
        dynamic task;
        try { task = service.GetFolder("\\").GetTask(TaskName); }
        catch (System.Runtime.InteropServices.COMException ex) { if (ex.ErrorCode == unchecked((int)0x80070002)) return false; throw; }
        catch (FileNotFoundException) { return false; }
        return task.Enabled && OwnTask(task);
    }
    static bool OwnTask(dynamic task)
    {
        return string.Equals((string)task.Definition.Actions.Item(1).Path,
            Path.Combine(Root, "TrackpadBattery.exe"), StringComparison.OrdinalIgnoreCase);
    }
    internal static void Disable()
    {
        dynamic service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));
        service.Connect();
        dynamic folder = service.GetFolder("\\");
        if (OwnTask(folder.GetTask(TaskName))) folder.DeleteTask(TaskName, 0);
    }
}
