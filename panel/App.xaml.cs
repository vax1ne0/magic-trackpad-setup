using Microsoft.UI.Xaml;

namespace AmtPtpControlPanel.WinUI3
{
    public partial class App : Application
    {
        private Window? _window;

        public App()
        {
            InitializeComponent();
            LocalizationManager.Initialize();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, ".."));
            bool background = Environment.GetCommandLineArgs().Contains("--tray-update");
            try
            {
                if (AppUpdates.TryStartPending(root, background)) { Exit(); return; }
            }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
            if (background)
            {
                System.Diagnostics.Process.Start(System.IO.Path.Combine(root, "TrackpadBattery.exe"), "--resume");
                Exit();
                return;
            }
            // Older updaters start the new panel without arguments and pass the handoff in its environment.
            bool legacyRestart = Environment.GetEnvironmentVariable("TRACKPAD_BACKGROUND") == "0"
                && Environment.GetEnvironmentVariable("TRACKPAD_UPDATE_SHA") is { Length: 64 }
                && string.Equals(root, Environment.GetEnvironmentVariable("TRACKPAD_UPDATE_ROOT"), StringComparison.OrdinalIgnoreCase);
            Environment.SetEnvironmentVariable("TRACKPAD_UPDATE_SHA", null);
            var window = new MainWindow(Environment.GetCommandLineArgs().Contains("--updated") || legacyRestart);

            // Set the size before activation so the window does not visibly
            // resize shortly after it appears.
            var display = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(window.AppWindow.Id,
                Microsoft.UI.Windowing.DisplayAreaFallback.Primary);
            var area = display.WorkArea;
            window.AppWindow.Resize(new Windows.Graphics.SizeInt32(
                System.Math.Min(900, area.Width), System.Math.Min(720, area.Height)));

            _window = window;
            window.Activate();
        }
    }
}
