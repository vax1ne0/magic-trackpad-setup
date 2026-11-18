using System.Runtime.InteropServices;
using Windows.Graphics;

namespace AmtPtpControlPanel.WinUI3;

internal sealed class KeyboardOverlay : InputPopup
{
    readonly List<StickerKey> keys;
    int width, height, gripSize;
    nint monitor;
    int scalePercent;
    Rect workArea;
    int Gap => Math.Max(1,gripSize/5);
    Rect GripBounds => new Rect{Left=width+Gap, Right=width+Gap+gripSize, Top=0, Bottom=gripSize};
    int WindowWidth => positioning?GripBounds.Right:width;
    [DllImport("user32")] static extern bool GetWindowRect(nint hwnd,out Rect rect);
    readonly double fallbackScale;
    const double CornerRadiusMm=10; // Approximation of the USB-C model's outer corner.
    nint font;
    readonly uint accent,background,foreground,cap;
    StickerKey? hover, modifierContact;
    bool touching, idle=true, fadeIdle;
    int transparencyPercent;
    bool positioning, dragging;
    Point grab;


    [DllImport("user32")] static extern nint SetCapture(nint hwnd);
    [DllImport("user32")] static extern nint GetCapture();
    [DllImport("user32")] static extern bool ReleaseCapture();
    [DllImport("user32")] static extern nuint SetTimer(nint hwnd,nuint id,uint milliseconds,nint callback);
    [DllImport("user32")] static extern bool KillTimer(nint hwnd,nuint id);
    [DllImport("user32")] static extern short GetAsyncKeyState(int key);
    [DllImport("user32")] static extern nint LoadCursorW(nint module,nint id);
    [DllImport("user32")] static extern nint SetCursor(nint cursor);

    [DllImport("user32")] static extern nint SetWindowLongPtrW(nint hwnd,int index,nint value);
    [DllImport("user32")] static extern bool SetLayeredWindowAttributes(nint hwnd,uint color,byte alpha,uint flags);
    [DllImport("user32")] static extern bool InvalidateRect(nint hwnd,nint rect,bool erase);
    [DllImport("user32")] static extern bool ValidateRect(nint hwnd,nint rect);
    [DllImport("user32")] static extern nint GetDC(nint hwnd);
    [DllImport("user32")] static extern int ReleaseDC(nint hwnd,nint dc);
    [DllImport("user32")] static extern int FillRect(nint dc,in Rect rect,nint brush);
    [DllImport("user32",CharSet=CharSet.Unicode)] static extern int DrawText(nint dc,string text,int length,in Rect rect,uint flags);
    [DllImport("gdi32")] static extern nint GetStockObject(int index);
    [DllImport("gdi32")] static extern uint SetDCBrushColor(nint dc,uint color);
    [DllImport("gdi32")] static extern uint SetTextColor(nint dc,uint color);
    [DllImport("gdi32")] static extern int SetBkMode(nint dc,int mode);
    [DllImport("gdi32")] static extern nint SelectObject(nint dc,nint obj);
    [DllImport("gdi32")] static extern bool DeleteObject(nint obj);
    [DllImport("gdi32",CharSet=CharSet.Unicode)] static extern nint CreateFont(int h,int w,int angle,int orientation,int weight,uint italic,uint underline,uint strike,uint charset,uint precision,uint clip,uint quality,uint pitch,string face);
    [DllImport("user32")] static extern bool ScreenToClient(nint hwnd,ref Point point);

    [DllImport("user32")] static extern nint MonitorFromPoint(Point point,uint flags);
    [DllImport("shcore")] static extern int GetDpiForMonitor(nint monitor,int type,out uint x,out uint y);
    [StructLayout(LayoutKind.Sequential)] struct MonitorInfo
    {
        public int Size;
        public Rect Bounds,Work;
        public uint Flags;
    }
    [DllImport("user32", CharSet=CharSet.Unicode)] static extern bool GetMonitorInfo(nint monitor,ref MonitorInfo info);
    [DllImport("gdi32")] static extern nint CreateRoundRectRgn(int left,int top,int right,int bottom,int ellipseWidth,int ellipseHeight);
    [DllImport("gdi32")] static extern int CombineRgn(nint target,nint first,nint second,int mode);
    [DllImport("user32")] static extern int SetWindowRgn(nint hwnd,nint region,bool redraw);

    void SizeForDisplay(nint display,int availableWidth,int availableHeight)
    {
        monitor=display;
        const int rawDpi=2;
        double dpi=GetDpiForMonitor(display,rawDpi,out uint reported,out _)==0 && reported>0
            ? reported : 96*fallbackScale;
        double pixelsPerMm=Math.Min(dpi/25.4*scalePercent/100,Math.Min(availableWidth/(Surface.WidthMm+10),availableHeight/Surface.DepthMm));
        width=Math.Max(1,(int)Math.Round(Surface.WidthMm*pixelsPerMm));
        height=Math.Max(1,(int)Math.Round(Surface.DepthMm*pixelsPerMm));
        gripSize=Math.Max(1,(int)Math.Round(8*pixelsPerMm));
        var newFont=CreateFont(-Math.Max(1,(int)Math.Round(3.6*pixelsPerMm)),0,0,0,400,0,0,0,1,0,0,5,0,"Segoe UI");
        if(font!=0)DeleteObject(font);
        font=newFont;

        UpdateShape();
    }

    void UpdateShape()
    {
        int diameter=(int)Math.Round(2*CornerRadiusMm*width/Surface.WidthMm);
        var region=CreateRoundRectRgn(0,0,width+1,height+1,diameter,diameter);
        if(positioning && region!=0)
        {
            var r=GripBounds;
            var tab=CreateRoundRectRgn(r.Left,r.Top,r.Right+1,r.Bottom+1,gripSize/2,gripSize/2);
            if(tab!=0){CombineRgn(region,region,tab,2);DeleteObject(tab);} // RGN_OR
        }
        // Windows owns the region after SetWindowRgn succeeds.
        if(region!=0 && SetWindowRgn(handle,region,true)==0)DeleteObject(region);
    }
    public KeyboardOverlay(List<StickerKey> keys, RectInt32 area, double scale, bool dark, uint accent,int scalePercent=100,int transparency=38)
        : base("Floating keyboard",Layered|0x20)
    {
        this.keys=keys;this.accent=accent;
        background=dark?0x242424u:0xF4F4F4u;foreground=dark?0xFFFFFFu:0x171717u;
        cap=dark?0x404040u:0xFFFFFFu;
        fallbackScale=scale;
        this.scalePercent=scalePercent;
        workArea=new Rect{Left=area.X,Top=area.Y,Right=area.X+area.Width,Bottom=area.Y+area.Height};
        var center=new Point{X=area.X+area.Width/2,Y=area.Y+area.Height/2};
        SizeForDisplay(MonitorFromPoint(center,2),area.Width,area.Height);
        SetTransparency(transparency);
        SetWindowPos(handle,-1,area.X+Math.Max(0,area.Width-width-16),area.Y+Math.Max(0,area.Height-height-16),width,height,0x10);
    }
    void SetTransparency(int percent)
    {
        transparencyPercent=percent;
        UpdateOpacity();
    }
    void UpdateOpacity()
    {
        double opacity=255*(100-transparencyPercent)/100.0;
        if(fadeIdle && idle && !positioning)opacity*=0.4;
        SetLayeredWindowAttributes(handle,0,(byte)Math.Round(opacity),2);
    }
    public void SetIdleFade(bool enabled)
    {
        fadeIdle=enabled;
        UpdateOpacity();
    }
    public void SetTouching(bool value)
    {
        if(touching==value)return;
        touching=value;
        KillTimer(handle,2);
        if(value){idle=false;UpdateOpacity();}
        else SetTimer(handle,2,800,0);
    }

    public void SetAppearance(int newScale,int transparency)
    {
        SetTransparency(transparency);
        if(scalePercent==newScale)return;
        scalePercent=newScale;
        GetWindowRect(handle,out var bounds);
        SizeForDisplay(monitor,workArea.Right-workArea.Left,workArea.Bottom-workArea.Top);
        int x=Math.Clamp(bounds.Left,workArea.Left,workArea.Right-WindowWidth);
        int y=Math.Clamp(bounds.Top,workArea.Top,workArea.Bottom-height);
        SetWindowPos(handle,0,x,y,WindowWidth,height,0x14);
        InvalidateRect(handle,0,false);
    }
    public void SetVisible(bool visible)
    {
        if (!visible) EndInteraction();
        ShowWindow(handle,visible ? 8 : 0); // SW_SHOWNA keeps the typing destination focused.
    }
    public void Highlight(StickerKey? key,StickerKey? modifier=null)
    {
        if(hover==key && modifierContact==modifier)return;
        modifierContact=modifier;
        hover=key;
        InvalidateRect(handle,0,false);
    }
    public void SetPositioning(bool value)
    {
        if(positioning==value)return;
        EndInteraction();
        positioning=value;
        SetWindowLongPtrW(handle,-20,NoActivate|ToolWindow|Layered|8|(value?0:0x20));
        UpdateShape();
        GetWindowRect(handle,out var bounds);
        int x=Math.Clamp(bounds.Left,workArea.Left,Math.Max(workArea.Left,workArea.Right-WindowWidth));
        SetWindowPos(handle,-1,x,bounds.Top,WindowWidth,height,0x30);
        UpdateOpacity();
        InvalidateRect(handle,0,false);
    }
    protected override nint Message(nint w,uint m,nint a,nint b)
    {
        if(m==0x113 && a==2)
        {
            KillTimer(w,2);
            if(!touching){idle=true;UpdateOpacity();}
            return 0;
        }
        if(m==0x84) return positioning ? 1 : -1;
        if(m==0x20 && positioning && GetCursorPos(out var cursor))
        {
            ScreenToClient(w,ref cursor);
            SetCursor(LoadCursorW(0, OnGrip(cursor) ? 32646 : 32512));
            return 1;
        }
        if(m==0x201 && positioning)
        {
            grab=new Point{X=(short)((long)b & 65535),Y=(short)((long)b>>16)};
            if(!OnGrip(grab))return 0;
            dragging=true;
            SetCapture(w);
            // A non-activating popup cannot rely on receiving mouse moves
            // outside its bounds. Track the physical drag across other windows.
            SetTimer(w,1,16,0);
            return 0;
        }
        if(m==0x113 && a==1 && dragging)
        {
            if(GetAsyncKeyState(1)>=0) EndInteraction();
            else if(GetCursorPos(out var pt))
            {
                nint display=MonitorFromPoint(pt,2);
                if(display!=monitor)
                {
                    var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};
                    if(GetMonitorInfo(display,ref info))
                    {
                        workArea=info.Work;
                        int oldWidth=width, oldHeight=height;
                        SizeForDisplay(display,info.Work.Right-info.Work.Left,info.Work.Bottom-info.Work.Top);
                        grab.X=grab.X*width/oldWidth;
                        grab.Y=grab.Y*height/oldHeight;
                    }
                }
                SetWindowPos(w,0,Math.Clamp(pt.X-grab.X,workArea.Left,Math.Max(workArea.Left,workArea.Right-WindowWidth)),Math.Clamp(pt.Y-grab.Y,workArea.Top,workArea.Bottom-height),WindowWidth,height,0x14);
            }
            return 0;
        }
        if(m==0x202)
        {
            EndInteraction();
            return 0;
        }
        if(m==0x215 || m==0x1F) EndInteraction();
        if(m==0x14)return 1;
        if(m==0x0F)
        {
            var dc=GetDC(w);
            try{Paint(dc);}finally{ReleaseDC(w,dc);ValidateRect(w,0);}
            return 0;
        }
        return base.Message(w,m,a,b);
    }
    bool OnGrip(Point p) => p.X>=GripBounds.Left && p.X<GripBounds.Right && p.Y>=GripBounds.Top && p.Y<GripBounds.Bottom;
    void EndInteraction()
    {
        if(dragging)KillTimer(handle,1);
        dragging=false;
        if(GetCapture()==handle)ReleaseCapture();
    }
    void Paint(nint dc)
    {
        var brush=GetStockObject(18);
        var previous=SelectObject(dc,font);
        SetBkMode(dc,1);
        void Box(Rect rect,uint color){SetDCBrushColor(dc,color);FillRect(dc,in rect,brush);}
        void Label(Rect rect,string label,uint color,uint align){
            SetTextColor(dc,color);DrawText(dc,label,-1,in rect,0x824|align);
        }
        Box(new Rect{Right=width,Bottom=height},background);
        foreach(var key in keys)
        {
            var b=key.Bounds;
            var r=new Rect{Left=(int)(b.Left*width/Surface.WidthMm),Right=(int)(b.Right*width/Surface.WidthMm),
                Top=(int)(b.Top*height/Surface.DepthMm),Bottom=(int)(b.Bottom*height/Surface.DepthMm)};
            bool highlighted=key==hover || key==modifierContact;
            Box(r,highlighted?accent:cap);
            Label(r,key.Name=="MODE"?"MODE":key.Label,highlighted?(foreground==0xFFFFFFu?0u:0xFFFFFFu):foreground,1);
        }
        if(positioning)
        {
            var grip=GripBounds;
            Box(grip,background);
            int dot=Math.Max(2,gripSize/12);
            for(int row=-1;row<=1;row++)
                for(int col=-1;col<=1;col++)
                {
                    int x=grip.Left+gripSize/2+col*2*dot-dot/2;
                    int y=grip.Top+gripSize/2+row*2*dot-dot/2;
                    Box(new Rect{Left=x,Top=y,Right=x+dot,Bottom=y+dot},foreground);
                }
        }
        SelectObject(dc,previous);
    }
    public override void Dispose(){EndInteraction();base.Dispose();DeleteObject(font);}
}
