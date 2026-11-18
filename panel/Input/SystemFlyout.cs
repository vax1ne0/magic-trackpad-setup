using System.Runtime.InteropServices;

namespace AmtPtpControlPanel.WinUI3;

// Explorer's flyout service is optional. Absolute level changes work independently.
internal static class SystemFlyout
{
    [ComImport, Guid("6d5140c1-7436-11ce-8034-00aa006009fa"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IServiceProvider
    {
        [PreserveSig] int QueryService(in Guid service,in Guid iid,[MarshalAs(UnmanagedType.Interface)] out IFlyoutDisplay display);
    }

    [ComImport, Guid("41f9d2fb-7834-4ab6-8b1b-73e74064b465"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IFlyoutDisplay
    {
        [PreserveSig] int ShowFlyout(int kind,uint options);
    }

    [DllImport("user32", CharSet=CharSet.Unicode)] static extern nint FindWindow(string cls,string title);
    [DllImport("user32", CharSet=CharSet.Unicode)] static extern uint RegisterWindowMessage(string name);
    [DllImport("user32", EntryPoint="PostMessageW")] static extern bool PostMessage(nint window,uint msg,nint kind,nint data);
    static readonly uint shellHook=RegisterWindowMessage("SHELLHOOK");

    static readonly object gate=new();
    static Surface.EdgeControl? lastControl;
    static long lastShown;
    internal static void Refresh(Surface.EdgeControl control)
    {
        lock(gate)
        {
            long now=Environment.TickCount64;
            if(lastControl==control && now-lastShown<450)return;
            lastControl=control;
            lastShown=now;
            TryShow(control);
        }
    }

    internal static bool TryShow(Surface.EdgeControl control)
    {
        object? shell=null;
        IFlyoutDisplay? display=null;
        try
        {
            var host=FindWindow("NativeHWNDHost","");
            if(host!=0 && shellHook!=0)return PostMessage(host,shellHook,control==Surface.EdgeControl.Volume ? 0x37 : 0x38,0);
            var shellType=Type.GetTypeFromCLSID(new Guid("c2f03a33-21f5-47fa-b4bb-156362a2f239"));
            shell=Activator.CreateInstance(shellType!);
            var iid=typeof(IFlyoutDisplay).GUID;
            return shell is IServiceProvider services && services.QueryService(in iid,in iid,out display)>=0 &&
                display.ShowFlyout(control==Surface.EdgeControl.Volume ? 0 : 3,0)>=0;
        }
        catch(Exception ex) when(ex is COMException or UnauthorizedAccessException or InvalidCastException){return false;}
        finally
        {
            if(display!=null)Marshal.ReleaseComObject(display);
            if(shell!=null)Marshal.ReleaseComObject(shell);
        }
    }
}
