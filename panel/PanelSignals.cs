using System;
using System.Threading;

// Shared by the WinUI panel and the .NET Framework tray monitor.
internal static class PanelSignals
{
    internal const string Listener = @"Local\MagicTrackpad.Input.Owner";
    internal const string Show = @"Local\MagicTrackpad.Input.Show";
    internal const string Quit = @"Local\MagicTrackpad.Input.Quit";
    internal const string Switch = @"Local\MagicTrackpad.Input.Switch";

    internal static bool Send(string name)
    {
        try
        {
            using (var signal = EventWaitHandle.OpenExisting(name))
                return signal.Set();
        }
        catch (WaitHandleCannotBeOpenedException) { return false; }
    }
}
