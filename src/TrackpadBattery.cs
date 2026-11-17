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
    readonly Control dispatcher = new Control();
    Icon currentIcon;
    bool busy, closed, warnedLow;
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
        menu.Items.Add("Trackpad settings", null, delegate
        {
            Open(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "MagicTrackpadSetup.exe"));
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, delegate { ExitThread(); });
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += delegate { Refresh(); };
        timer.Interval = 15000;
        timer.Tick += delegate { Refresh(); };
        timer.Start();
        Refresh();
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
            string connection = TrackpadDevice.Read(out level);
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
                    if (level < 0)
                    {
                        tray.Visible = false;
                        return;
                    }
                    status.Text = "Magic Trackpad: " + reading;
                    updated.Text = "Last checked " + DateTime.Now.ToString("t");
                    runtime.Text = remaining;
                    tray.Text = "Magic Trackpad: " + reading + "\n" + remaining;
                    SetIcon(level);
                    tray.Visible = true;
                    // Keep the warning latched across disconnects and small charge fluctuations.
                    if (level >= 25) warnedLow = false;
                    if (!warnedLow && connection == "Bluetooth" && level <= 20 &&
                        AppPreferences.Enabled(AppPreferences.LowBatteryAlert, true))
                    {
                        warnedLow = true;
                        tray.ShowBalloonTip(5000, "Magic Trackpad", "Low battery (" + level + "%).", ToolTipIcon.Info);
                    }
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
            StartupPreference.Disable();
            return;
        }
        if (args.Length == 0 && File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Updates\pending.txt")))
        {
            System.Diagnostics.Process.Start(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                @"Panel\AmtPtpControlPanel.WinUI3.exe"), "--tray-update");
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
