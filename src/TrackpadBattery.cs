using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

// Read-only client of the installed MagicTrackpad2ForWindows driver protocol.
// Protocol reference: AmtPtpControlPanel.WinUI3/DeviceInterop.cs in the upstream repository.
internal static class BatteryReader
{
    internal static string Diagnostic = "";
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern SafeFileHandle CreateFile(string name, uint access, uint sharing,
        IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool DeviceIoControl(SafeFileHandle handle, uint code, IntPtr input,
        uint inputSize, out uint output, uint outputSize, out uint returned, IntPtr overlapped);

    internal static string Read(out int percentage)
    {
        percentage = -1;
        try
        {
            using (var handle = CreateFile(@"\\.\AmtPtpControlDeviceUm", 0xC0000000, 3,
                IntPtr.Zero, 3, 0, IntPtr.Zero))
            {
                uint value, returned;
                if (handle.IsInvalid) Diagnostic = "Open error " + Marshal.GetLastWin32Error();
                else if (DeviceIoControl(handle, 0x222004, IntPtr.Zero, 0,
                    out value, 4, out returned, IntPtr.Zero) && returned >= 4 && value <= 100)
                {
                    percentage = (int)value;
                    return "Bluetooth";
                }
                else Diagnostic = "Read error " + Marshal.GetLastWin32Error();
            }
        }
        catch { }
        try
        {
            using (var pipe = new NamedPipeClientStream(".", "AmtPtpControlPanelUsbUmInterface",
                PipeDirection.InOut, PipeOptions.Asynchronous))
            {
                pipe.Connect(300);
                byte[] command = { 1 };
                var write = pipe.WriteAsync(command, 0, 1);
                if (!write.Wait(1000)) return "Unavailable";
                write.GetAwaiter().GetResult();
                byte[] reply = new byte[2];
                int count = 0;
                while (count < 2)
                {
                    var read = pipe.ReadAsync(reply, count, 2 - count);
                    if (!read.Wait(1000)) return "Unavailable";
                    int received = read.GetAwaiter().GetResult();
                    if (received == 0) return "Unavailable";
                    count += received;
                }
                if (reply[0] == 0 && reply[1] <= 100)
                {
                    percentage = reply[1];
                    return "USB";
                }
            }
        }
        catch { }
        return "Unavailable";
    }
}

internal sealed class RuntimeEstimate
{
    readonly string file;
    long seconds, points;
    int last = -1;
    DateTime previous, anchor;
    public RuntimeEstimate(string file)
    {
        this.file = file;
        try
        {
            string[] parts = File.ReadAllText(file).Split(',');
            long s, p;
            if (parts.Length == 2 && long.TryParse(parts[0], out s) && long.TryParse(parts[1], out p)
                && s > 0 && p > 0 && s < 3155760000L && p < 1000000)
            { seconds = s; points = p; }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public string Observe(int level, string connection, DateTime now)
    {
        if (connection != "Bluetooth" || level < 0)
        {
            last = -1;
            anchor = DateTime.MinValue;
            return connection == "USB" ? "Connected by USB" : "Time remaining unavailable";
        }
        var gap = now - previous;
        if (last < 0 || gap.TotalSeconds > 100 || gap.TotalSeconds < 0 || level > last)
            anchor = DateTime.MinValue;
        else if (level < last)
        {
            // Time complete intervals between observed drops. The first drop
            // after reconnecting may represent time spent away from the PC.
            if (anchor != DateTime.MinValue && now > anchor)
            {
                seconds += (long)(now - anchor).TotalSeconds;
                points += last - level;
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(file));
                    File.WriteAllText(file + ".tmp", seconds + "," + points);
                    if (File.Exists(file)) File.Replace(file + ".tmp", file, null);
                    else File.Move(file + ".tmp", file);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            anchor = now;
        }
        previous = now;
        last = level;
        if (level == 0) return "Battery empty";
        if (points < 5 || seconds < 3600) return "Estimating battery life";
        double hours = level * (double)seconds / (3600 * points);
        return hours < 1 ? "About " + Math.Max(1, Math.Round(hours * 60)) + " min left"
            : "About " + Math.Round(hours) + " h left";
    }
}

internal sealed class TrayApp : ApplicationContext
{
    readonly NotifyIcon tray = new NotifyIcon();
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    readonly ToolStripMenuItem status = new ToolStripMenuItem("Checking battery…");
    readonly ToolStripMenuItem updated = new ToolStripMenuItem("Waiting for the trackpad");
    readonly ToolStripMenuItem runtime = new ToolStripMenuItem("Estimating battery life");
    readonly RuntimeEstimate estimate = new RuntimeEstimate(Path.Combine(DataFolder, "discharge.csv"));
    readonly ToolStripMenuItem startup = new ToolStripMenuItem("Show battery at sign-in");
    readonly Control dispatcher = new Control();
    Icon currentIcon;
    bool busy, closed;
    const string AppName = "Magic Trackpad Battery Monitor";
    internal static readonly string DataFolder = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "BatteryData");

    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr handle);

    internal TrayApp()
    {
        var unused = dispatcher.Handle;
        var menu = new ContextMenuStrip();
        status.Enabled = updated.Enabled = runtime.Enabled = false;
        menu.Items.Add(status);
        menu.Items.Add(runtime);
        menu.Items.Add(updated);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Check battery now", null, delegate { Refresh(); });
        menu.Items.Add("Windows gestures", null, delegate { Open("ms-settings:devices-touchpad"); });
        menu.Items.Add("Trackpad settings", null, delegate
        {
            Open(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "TrackpadControlPanel.exe"));
        });
        try { dynamic scheduler = Scheduler(); scheduler.GetFolder("\\").GetTask(AppName); startup.Checked = true; }
        catch { }
        startup.Click += delegate
        {
            try
            {
                dynamic scheduler = Scheduler();
                dynamic folder = scheduler.GetFolder("\\");
                if (startup.Checked) folder.DeleteTask(AppName, 0);
                else
                {
                    string user = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
                    dynamic task = scheduler.NewTask(0);
                    task.RegistrationInfo.Description = "Shows Magic Trackpad battery in the notification area.";
                    task.Principal.UserId = user;
                    task.Principal.LogonType = 3;
                    task.Principal.RunLevel = 1;
                    task.Settings.DisallowStartIfOnBatteries = false;
                    task.Settings.StopIfGoingOnBatteries = false;
                    task.Settings.ExecutionTimeLimit = "PT0S";
                    task.Settings.MultipleInstances = 2;
                    dynamic trigger = task.Triggers.Create(9);
                    trigger.UserId = user;
                    dynamic action = task.Actions.Create(0);
                    action.Path = Application.ExecutablePath;
                    folder.RegisterTaskDefinition(AppName, task, 6, user, null, 3, null);
                }
                startup.Checked = !startup.Checked;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Trackpad Battery"); }
        };
        menu.Items.Add(startup);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit battery monitor", null, delegate { ExitThread(); });
        tray.ContextMenuStrip = menu;
        tray.Text = "Magic Trackpad: reading battery...";
        SetIcon(-1);
        tray.Visible = true;
        tray.DoubleClick += delegate { Refresh(); };
        timer.Interval = 60000;
        timer.Tick += delegate { Refresh(); };
        timer.Start();
        Refresh();
    }

    static dynamic Scheduler()
    {
        dynamic scheduler = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));
        scheduler.Connect();
        return scheduler;
    }

    static void Open(string target)
    {
        try { System.Diagnostics.Process.Start(target); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Trackpad Battery"); }
    }

    void Refresh()
    {
        if (busy || closed) return;
        busy = true;
        Task.Run(delegate
        {
            int level;
            string connection = BatteryReader.Read(out level);
            string reading = level >= 0 ? level + "% (" + connection + ")" : "Battery unavailable";
            string remaining = estimate.Observe(level, connection, DateTime.UtcNow);
            string timestamp = DateTimeOffset.Now.ToString("o");
            try
            {
                Directory.CreateDirectory(DataFolder);
                File.WriteAllText(Path.Combine(DataFolder, "status.txt"), reading + Environment.NewLine + timestamp + Environment.NewLine + remaining);
            }
            catch { }
            try
            {
                dispatcher.BeginInvoke((Action)delegate
                {
                    busy = false;
                    if (closed) return;
                    status.Text = "Magic Trackpad: " + reading;
                    updated.Text = "Last checked " + DateTime.Now.ToString("t");
                    runtime.Text = remaining;
                    tray.Text = "Magic Trackpad: " + reading + "\n" + remaining;
                    SetIcon(level);
                });
            }
            catch (InvalidOperationException) { }
        });
    }

    void SetIcon(int level)
    {
        using (var bitmap = BatteryArt.Tray(level, 64))
        {
            IntPtr native = bitmap.GetHicon();
            Icon next;
            try { using (var borrowed = Icon.FromHandle(native)) next = (Icon)borrowed.Clone(); }
            finally { DestroyIcon(native); }
            tray.Icon = next;
            if (currentIcon != null) currentIcon.Dispose();
            currentIcon = next;
        }
    }

    protected override void ExitThreadCore()
    {
        closed = true;
        timer.Stop();
        timer.Dispose();
        tray.Visible = false;
        tray.Dispose();
        if (currentIcon != null) currentIcon.Dispose();
        dispatcher.Dispose();
        base.ExitThreadCore();
    }
}

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--remove-autostart")
        {
            dynamic scheduler = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));
            scheduler.Connect();
            dynamic folder = scheduler.GetFolder("\\");
            foreach (dynamic task in folder.GetTasks(0))
            {
                if (task.Name == "Magic Trackpad Battery Monitor" &&
                    string.Equals((string)task.Definition.Actions.Item(1).Path, Application.ExecutablePath, StringComparison.OrdinalIgnoreCase))
                {
                    folder.DeleteTask(task.Name, 0);
                    break;
                }
            }
            return;
        }
        bool first;
        using (var mutex = new Mutex(true, @"Local\MagicTrackpadSetup.Battery.Tray", out first))
        {
            if (!first) return;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayApp());
        }
    }
}
