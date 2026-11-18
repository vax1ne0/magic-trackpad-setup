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

internal sealed class PointerParking : InputPopup
{
    [DllImport("user32")] static extern bool ClipCursor(in Rect rect);
    [DllImport("user32",EntryPoint="ClipCursor")] static extern bool ReleaseClip(nint none);
    [DllImport("user32")] static extern nint SetCursor(nint cursor);
    [DllImport("user32")] static extern bool RegisterHotKey(nint hwnd,int id,uint modifiers,uint vk);
    [DllImport("user32")] static extern bool UnregisterHotKey(nint hwnd,int id);
    [DllImport("user32")] static extern bool SetLayeredWindowAttributes(nint hwnd,uint color,byte alpha,uint flags);
    [DllImport("user32")] static extern int ShowCursor(bool show);
    [DllImport("user32")] static extern nint LoadCursorW(nint module,nint id);
    [DllImport("user32")] static extern int GetSystemMetrics(int index);
    [DllImport("user32")] static extern nuint SetTimer(nint hwnd,nuint id,uint ms,nint callback);
    [DllImport("user32")] static extern bool KillTimer(nint hwnd,nuint id);
    [StructLayout(LayoutKind.Explicit,Size=40)] struct MouseInput
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(16)] public int Data;
        [FieldOffset(20)] public uint Flags;
    }
    [DllImport("user32")] static extern uint SendInput(uint count,MouseInput[] inputs,int size);
    readonly Action restore;
    bool pointing,leftDown;
    [DllImport("user32",EntryPoint="#2689")] static extern bool RegisterTouchpadCapableWindow(nint hwnd,bool enable);
    public PointerParking(Action restore) : base("Keyboard pointer",Layered)
    {
        this.restore=restore;
        // Consume precision pan messages on the parking window before Windows promotes them to wheel events.
        try { RegisterTouchpadCapableWindow(handle,true); } catch(EntryPointNotFoundException) { }
    }
    public PointerSpeed Speed { get; }=new();
    long lastMove;
    double fractionX,fractionY;
    public void ObserveNormalPointer(float x,float y,bool valid,long now)
    {
        if(valid && GetCursorPos(out var cursor))Speed.Observe(x,y,cursor.X,cursor.Y,now);
        else Speed.EndSample();
    }
    public void SetLeftButton(bool down)
    {
        if(leftDown==down || (down && (!Active || !pointing)))return;
        KillTimer(handle,2);
        ShowWindow(handle,0);
        if(SendInput(1,[new MouseInput{Flags=down?2u:4u}],Marshal.SizeOf<MouseInput>())==1)
            leftDown=down;
        SetTimer(handle,2,45,0);
    }
    public void Scroll(int steps)
    {
        if(!Active || leftDown || steps==0)return;
        ShowWindow(handle,0);
        SendInput(1,[new MouseInput{Flags=0x800,Data=steps*120}],Marshal.SizeOf<MouseInput>());
        SetTimer(handle,2,45,0);
    }
    void SendLeftClick()
    {
        ShowWindow(handle,0);
        SendInput(2,[new MouseInput{Flags=2},new MouseInput{Flags=4}],Marshal.SizeOf<MouseInput>());
        SetTimer(handle,2,60,0);
    }
    int visibilityCalls;

    void RestoreCursor()
    {
        while(visibilityCalls>0){ShowCursor(true);visibilityCalls--;}
        SetCursor(LoadCursorW(0,32512));
    }
    void HideCursor()
    {
        if(visibilityCalls==0)do {visibilityCalls++;} while(ShowCursor(false)>=0);
        SetCursor(0);
    }
    public void SetPointing(bool value,bool tap)
    {
        if(!Active || value==pointing)return;
        pointing=value;
        KillTimer(handle,1);
        if(value)
        {
            KillTimer(handle,2);
            ShowWindow(handle,8);
            fractionX=fractionY=0;
            lastMove=Environment.TickCount64;
            RestoreCursor();
        }
        else
        {
            HideCursor();
            // Keep the parking window under the native two-finger release first.
            if(tap)SetTimer(handle,1,60,0);
        }
    }

    public void MovePointer(float dx,float dy)
    {
        if(!Active || !pointing || !GetCursorPos(out var p))return;
        long now=Environment.TickCount64;
        double speed=Math.Sqrt(dx*dx+dy*dy)*1000/Math.Clamp(now-lastMove,4,100);
        lastMove=now;
        double pixelsPerMm=Speed.Gain(speed);
        fractionX+=dx*pixelsPerMm;
        fractionY+=dy*pixelsPerMm;
        int x=(int)fractionX,y=(int)fractionY;
        fractionX-=x;fractionY-=y;
        if(x==0 && y==0)return;
        // ClipCursor moves the system cursor inside this new one-pixel rectangle.
        int left=GetSystemMetrics(76),top=GetSystemMetrics(77);
        p.X=Math.Clamp(p.X+x,left,left+GetSystemMetrics(78)-1);
        p.Y=Math.Clamp(p.Y+y,top,top+GetSystemMetrics(79)-1);
        SetWindowPos(handle,-1,p.X,p.Y,1,1,0x10);
        ClipCursor(new Rect{Left=p.X,Top=p.Y,Right=p.X+1,Bottom=p.Y+1});
    }
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
            HideCursor();
            return true;
        }
        ShowWindow(handle,0);
        UnregisterHotKey(handle,1);
        return false;
    }
    public void Release()
    {
        if(!Active) return;
        SetLeftButton(false);
        Active=pointing=false;
        KillTimer(handle,1);KillTimer(handle,2);
        ReleaseClip(0);
        RestoreCursor();
        ShowWindow(handle,0);
        UnregisterHotKey(handle,1);
    }
    protected override nint Message(nint w,uint m,nint a,nint b)
    {
        if(m is 0x245 or 0x246 or 0x247 or 0x249 or 0x24A or 0x24E or 0x24F)return 0;
        if(m==0x84)return 1;
        if(m==0x20){SetCursor(pointing?LoadCursorW(0,32512):0);return 1;}
        if(m==0x113 && (a==1 || a==2))
        {
            KillTimer(w,(nuint)a);
            if(!Active)return 0;
            if(a==1)
            {
                if(!pointing)SendLeftClick();
            }
            else ShowWindow(w,8);
            return 0;
        }
        if(m==0x312){restore();return 0;}
        if(m>=0x200 && m<=0x20E) return 0;
        return base.Message(w,m,a,b);
    }
    public override void Dispose(){Release();base.Dispose();}
}
