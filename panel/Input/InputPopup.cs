using System.Runtime.InteropServices;

namespace AmtPtpControlPanel.WinUI3;

// A no-activate popup keeps the typing destination focused, even during a drag.
internal class InputPopup : IDisposable
{
    protected const int NoActivate = 0x08000000, ToolWindow = 0x80, Layered = 0x80000;
    protected readonly nint handle;
    delegate nint Proc(nint hwnd, uint msg, nint wp, nint lp, nuint id, nuint data);
    readonly Proc proc;
    [StructLayout(LayoutKind.Sequential)]
    protected struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    protected struct Point { public int X,Y; }
    [DllImport("user32", CharSet=CharSet.Unicode)] static extern nint CreateWindowEx(int ex,string cls,string name,uint style,int x,int y,int width,int height,nint parent,nint menu,nint module,nint param);
    [DllImport("comctl32")] static extern bool SetWindowSubclass(nint hwnd,Proc proc,nuint id,nuint data);
    [DllImport("comctl32")] protected static extern nint DefSubclassProc(nint hwnd,uint msg,nint wp,nint lp);
    [DllImport("user32")] static extern bool DestroyWindow(nint hwnd);
    [DllImport("user32")] protected static extern bool ShowWindow(nint hwnd,int command);
    [DllImport("user32")] protected static extern bool SetWindowPos(nint hwnd,nint after,int x,int y,int width,int height,uint flags);
    [DllImport("user32")] protected static extern bool GetCursorPos(out Point point);
    public InputPopup(string title, int extraStyle)
    {
        handle = CreateWindowEx(NoActivate|ToolWindow|extraStyle,"STATIC",title,0x80000000,0,0,1,1,0,0,0,0);
        proc = (w,m,a,b,_,_) => Message(w,m,a,b);
        if (handle == 0 || !SetWindowSubclass(handle,proc,1,0))
        {
            if(handle!=0) DestroyWindow(handle);
            throw new InvalidOperationException("The input window could not open.");
        }
    }
    protected virtual nint Message(nint w,uint m,nint a,nint b) =>
        m==0x21 ? 3 : DefSubclassProc(w,m,a,b); // MA_NOACTIVATE
    public virtual void Dispose() => DestroyWindow(handle);
}

internal sealed class PointerParking(Action restore) : InputPopup("Keyboard pointer", Layered)
{
    [DllImport("user32")] static extern bool ClipCursor(in Rect rect);
    [DllImport("user32",EntryPoint="ClipCursor")] static extern bool ReleaseClip(nint none);
    [DllImport("user32")] static extern nint SetCursor(nint cursor);
    [DllImport("user32")] static extern bool RegisterHotKey(nint hwnd,int id,uint modifiers,uint vk);
    [DllImport("user32")] static extern bool UnregisterHotKey(nint hwnd,int id);
    [DllImport("user32")] static extern bool SetLayeredWindowAttributes(nint hwnd,uint color,byte alpha,uint flags);
    [DllImport("user32")] static extern int ShowCursor(bool show);
    [DllImport("user32")] static extern nint LoadCursorW(nint module,nint id);
    int visibilityCalls;
    public bool Active {get; private set;}
    public bool Engage()
    {
        if(Active) return true;
        if(!GetCursorPos(out var pt) || !RegisterHotKey(handle,1,0x4003,0x79)) return false;
        var bounds=new Rect {Left=pt.X,Top=pt.Y,Right=pt.X+1,Bottom=pt.Y+1};
        // Nonzero alpha keeps this one pixel hit-testable. Its mouse messages
        // terminate here, while the panel continues to receive raw HID input.
        if(SetLayeredWindowAttributes(handle,0,1,2) &&
           SetWindowPos(handle,-1,pt.X,pt.Y,1,1,0x50) && ClipCursor(in bounds))
        {
            Active=true;
            do { visibilityCalls++; } while (ShowCursor(false)>=0);
            SetCursor(0);
            return true;
        }
        ShowWindow(handle,0);
        UnregisterHotKey(handle,1);
        return false;
    }
    public void Release()
    {
        if(!Active) return;
        Active=false;
        ReleaseClip(0);
        while(visibilityCalls>0){ShowCursor(true);visibilityCalls--;}
        SetCursor(LoadCursorW(0,32512));
        ShowWindow(handle,0);
        UnregisterHotKey(handle,1);
    }
    protected override nint Message(nint w,uint m,nint a,nint b)
    {
        if(m==0x84)return 1;
        if(m==0x20){SetCursor(0);return 1;}
        if(m==0x312){restore();return 0;}
        if(m>=0x200 && m<=0x20E) return 0;
        return base.Message(w,m,a,b);
    }
    public override void Dispose(){Release();base.Dispose();}
}
