using System;
using System.IO;
using System.Security.Principal;
internal static class StartupPreference
{
    static readonly string Root = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
    static string TrayPath
    {
        get { return Path.GetFullPath(Path.Combine(Root, Path.GetFileName(Root) == "Panel" ? ".." : ".", "TrackpadBattery.exe")); }
    }
    const string TaskName = "Magic Trackpad Battery Monitor";
    static dynamic Connect()
    {
        var type = Type.GetTypeFromProgID("Schedule.Service", true);
        if (type == null) throw new InvalidOperationException("Task Scheduler is unavailable.");
        var instance = Activator.CreateInstance(type);
        if (instance == null) throw new InvalidOperationException("Could not open Task Scheduler.");
        dynamic scheduler = instance;
        scheduler.Connect();
        return scheduler;
    }
    internal static void Enable(string sid, bool startNow)
    {
        // A system-wide deployment has no interactive recipient at install time.
        var user = new SecurityIdentifier(sid);
        if (user.IsWellKnown(WellKnownSidType.LocalSystemSid)) return;
        string account = user.Translate(typeof(NTAccount)).Value;
        dynamic service = Connect();
        dynamic folder = service.GetFolder("\\");

        dynamic existing = FindTask(folder);
        if (existing != null && !OwnTask(existing, TrayPath))
            throw new InvalidOperationException("Another application already uses this startup task name.");
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
        task.Actions.Create(0).Path = TrayPath;
        dynamic saved = folder.RegisterTaskDefinition(TaskName, task, 6, account, null, 3, null);
        if (startNow) saved.Run(null);
    }

    internal static bool Enabled()
    {
        dynamic service = Connect();
        dynamic folder = service.GetFolder("\\");

        dynamic task = FindTask(folder);
        if (task == null) return false;
        return task.Enabled && OwnTask(task, TrayPath);
    }
    #if NET
    static dynamic? FindTask(dynamic folder)
    #else
    static dynamic FindTask(dynamic folder)
    #endif
    {
        try { return folder.GetTask(TaskName); }
        catch (System.Runtime.InteropServices.COMException ex) { if (ex.ErrorCode == unchecked((int)0x80070002)) return null; throw; }
        catch (FileNotFoundException) { return null; }
    }
    static bool OwnTask(dynamic task, string path)
    {
        return string.Equals((string)task.Definition.Actions.Item(1).Path,
            path, StringComparison.OrdinalIgnoreCase);
    }
    internal static void Disable() { Disable(TrayPath); }
    internal static void Disable(string path)
    {
        dynamic service = Connect();
        dynamic folder = service.GetFolder("\\");

        dynamic task = FindTask(folder);
        if (task != null && OwnTask(task, path)) folder.DeleteTask(TaskName, 0);
    }
}
