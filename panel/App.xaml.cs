using Microsoft.UI.Xaml;

namespace AmtPtpControlPanel.WinUI3
{
    public partial class App : Application
    {
        private MainWindow? _window;
        // Held on the UI thread for the full lifetime of the raw-input listener.
        private Mutex? listener;
        private readonly List<(EventWaitHandle signal, RegisteredWaitHandle wait)> signals=new();

        public App()
        {
            InitializeComponent();
            LocalizationManager.Initialize();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, ".."));
            bool background = Environment.GetCommandLineArgs().Contains("--tray-update");
            if (background)
            {
                if(StartPending(root,true)){Exit();return;}
                System.Diagnostics.Process.Start(System.IO.Path.Combine(root, "TrackpadBattery.exe"), "--resume");
                Exit();
                return;
            }
            // Older updaters start the new panel without arguments and pass the handoff in its environment.
            bool legacyRestart = Environment.GetEnvironmentVariable("TRACKPAD_BACKGROUND") == "0"
                && Environment.GetEnvironmentVariable("TRACKPAD_UPDATE_SHA") is { Length: 64 }
                && string.Equals(root, Environment.GetEnvironmentVariable("TRACKPAD_UPDATE_ROOT"), StringComparison.OrdinalIgnoreCase);
            Environment.SetEnvironmentVariable("TRACKPAD_UPDATE_SHA", null);
            bool inputOnly=Environment.GetCommandLineArgs().Contains("--keyboard-background");
            if(inputOnly && !AppPreferences.BackgroundInputEnabled()){Exit();return;}
            var identity=System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(AppContext.BaseDirectory.ToUpperInvariant()));
            string thisBuild=PanelSignals.Show+"."+Convert.ToHexString(identity);
            listener=new Mutex(false,PanelSignals.Listener);
            if(!TakeListener(0))
            {
                if(inputOnly || PanelSignals.Send(thisBuild)){Exit();return;}
                PanelSignals.Send(PanelSignals.Switch);
                if(!TakeListener(2000)){PanelSignals.Send(PanelSignals.Show);Exit();return;}
            }
            if(StartPending(root,inputOnly)){listener.ReleaseMutex();Exit();return;}
            var window = new MainWindow(Environment.GetCommandLineArgs().Contains("--updated") || legacyRestart);

            // Set the size before activation so the window does not visibly
            // resize shortly after it appears.
            var display = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(window.AppWindow.Id,
                Microsoft.UI.Windowing.DisplayAreaFallback.Primary);
            var area = display.WorkArea;
            window.AppWindow.Resize(new Windows.Graphics.SizeInt32(
                System.Math.Min(900, area.Width), System.Math.Min(720, area.Height)));

            _window = window;
            var queue=window.DispatcherQueue;
            void ShowSettings()
            {
                if(StartPending(root,false)){window.ExitPanel();return;}
                window.AppWindow.Show();
                window.Activate();
                window.AppWindow.MoveInZOrderAtTop();
            }
            void Listen(string name,Action action)
            {
                var signal=new EventWaitHandle(false,EventResetMode.AutoReset,name);
                var wait=ThreadPool.RegisterWaitForSingleObject(signal,(_,_)=>queue.TryEnqueue(()=>action()),null,Timeout.Infinite,false);
                signals.Add((signal,wait));
            }
            Listen(thisBuild,ShowSettings);
            Listen(PanelSignals.Show,ShowSettings);
            Listen(PanelSignals.Quit,window.ExitPanel);
            Listen(PanelSignals.Switch,()=>
            {
                if(!window.AppWindow.IsVisible)window.ExitPanel();
                else window.ShowBuildSwitchNotice();
            });
            window.Closed+=(_,_)=>{
                foreach(var (signal,wait) in signals){wait.Unregister(null);signal.Dispose();}
                listener.ReleaseMutex();listener.Dispose();
            };
            if(inputOnly)window.AppWindow.Show(false);
            else window.Activate();
            if(inputOnly)window.AppWindow.Hide();
        }

        bool TakeListener(int milliseconds)
        {
            try { return listener!.WaitOne(milliseconds); }
            catch(AbandonedMutexException){return true;}
        }

        static bool StartPending(string root,bool background)
        {
            try { return AppUpdates.TryStartPending(root,background); }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                return false;
            }
        }
    }
}
