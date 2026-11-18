using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

sealed class TrackpadLab : Form
{
    [StructLayout(LayoutKind.Sequential)] struct Device { public ushort page,usage; public uint flags; public IntPtr window; }
    [DllImport("user32",SetLastError=true)] static extern bool RegisterRawInputDevices(Device[] devices,uint count,uint size);
    [DllImport("user32")] static extern uint GetRawInputData(IntPtr raw,uint command,IntPtr buffer,ref uint size,uint header);
    [DllImport("user32",CharSet=CharSet.Unicode)] static extern uint GetRawInputDeviceInfo(IntPtr device,uint command,StringBuilder buffer,ref uint chars);
    readonly Surface surface=new Surface();
    readonly KeyOutput keyboard=new KeyOutput();
    readonly Label status=new Label();
    readonly TextBox scratch=new TextBox();
    readonly CheckBox outside=new CheckBox();
    readonly CheckBox backgroundGestures=new CheckBox();
    readonly KeyBoardView board;
    readonly Stopwatch clock=Stopwatch.StartNew();
    IntPtr lastDevice;
    bool supported;
    static bool IsTrackpad(IntPtr device)
    {
        uint n=0;
        GetRawInputDeviceInfo(device,0x20000007,null,ref n);
        if(n==0 || n>4096) return false;
        var name=new StringBuilder((int)n+1);
        if(GetRawInputDeviceInfo(device,0x20000007,name,ref n)==uint.MaxValue) return false;
        string path=name.ToString().ToUpperInvariant();
        return (path.Contains("VID_05AC") || path.Contains("VID&0001004C")) &&
            (path.Contains("PID_0324") || path.Contains("PID&0324") || path.Contains("PID_0265") || path.Contains("PID&0265"));
    }
    // Load the optional WinRT API at runtime so older Windows builds still run the lab.
    static Action<bool> CreateSwipeGuard()
    {
        try
        {
            var type=Type.GetType("Windows.UI.Input.TouchpadGesturesController, Windows.UI.Input, ContentType=WindowsRuntime",false);
            if(type==null || !(bool)type.GetMethod("IsSupported").Invoke(null,null))return null;
            var controller=type.GetMethod("CreateForProcess").Invoke(null,null);
            var enable=(Action<bool>)Delegate.CreateDelegate(typeof(Action<bool>),controller,type.GetMethod("put_Enabled"));
            enable(false);
            var kinds=type.GetProperty("SupportedGestures");
            kinds.SetValue(controller,Enum.Parse(kinds.PropertyType,"FourFingerManipulations, FiveFingerManipulations"),null);
            return enable;
        }
        catch(Exception error)
        {
            Debug.WriteLine(error);
            return null;
        }
    }
    public TrackpadLab()
    {
        Text="Magic Trackpad · Sticker keyboard"; ClientSize=new Size(760,680);
        MinimumSize=new Size(570,490); Font=new Font("Segoe UI",10);Padding=new Padding(12);
        board=new KeyBoardView(surface){Dock=DockStyle.Fill};
        scratch.Multiline=true;scratch.AcceptsReturn=true;scratch.AcceptsTab=true;
        scratch.Dock=DockStyle.Bottom;scratch.Height=62;
        status.Dock=DockStyle.Bottom;status.Height=42;
        var options=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true};
        var mode=new Button{Text="Keyboard mode",AutoSize=true};
        var taps=new CheckBox{Text="Tap to type",AutoSize=true};
        outside.Text="Type in other apps";outside.AutoSize=true;
        backgroundGestures.Text="Background gestures";backgroundGestures.AutoSize=true;backgroundGestures.Checked=true;
        Action reset=delegate{surface.Reset();keyboard.Reset();};
        surface.ModeChanged=delegate{keyboard.Reset();mode.Text=surface.Keyboard?"Keyboard mode":"Trackpad mode";};
        mode.Click+=delegate{reset();surface.Keyboard=!surface.Keyboard;surface.ModeChanged();scratch.Focus();};
        taps.CheckedChanged+=delegate{reset();surface.TapMode=taps.Checked;scratch.Focus();};
        outside.CheckedChanged+=delegate{reset();scratch.Focus();};
        backgroundGestures.CheckedChanged+=delegate{reset();scratch.Focus();};
        options.Controls.AddRange(new Control[]{mode,taps,outside,backgroundGestures});
        Controls.Add(board);Controls.Add(scratch);Controls.Add(status);Controls.Add(options);
        status.Text="Click a sticker key to type. Click or hold MODE to switch.";
        surface.Emit=delegate(string name){
            if(ActiveForm==this && !scratch.Focused)scratch.Focus();
            status.Text=keyboard.Send(name);
        };
        Activated+=delegate{reset();};
        Deactivate+=delegate{reset();};
        Shown+=delegate {
            var d=new Device{page=13,usage=5,flags=0x100,window=Handle};
            if(!RegisterRawInputDevices(new[]{d},1,(uint)Marshal.SizeOf(typeof(Device))))status.Text="Windows could not register touch input.";
            surface.GuardFourFingerSwipe=CreateSwipeGuard();
            if(surface.GuardFourFingerSwipe==null)status.Text+=" · Windows gesture guard unavailable";
            scratch.Focus();
        };
        FormClosed+=delegate{surface.Reset();};
    }
    protected override void WndProc(ref Message message)
    {
        if(message.Msg==255 && (Form.ActiveForm==this || outside.Checked || backgroundGestures.Checked)) Read(message.LParam);
        base.WndProc(ref message);
    }
    void Read(IntPtr raw)
    {
        uint header=(uint)(8+2*IntPtr.Size), size=0;
        GetRawInputData(raw,0x10000003,IntPtr.Zero,ref size,header);
        if(size<header+8 || size>65536) return;
        IntPtr memory=Marshal.AllocHGlobal((int)size);
        try
        {
            if(GetRawInputData(raw,0x10000003,memory,ref size,header)!=size || Marshal.ReadInt32(memory)!=2) return;
            IntPtr device=Marshal.ReadIntPtr(memory,8);
            if(device!=lastDevice){lastDevice=device;supported=IsTrackpad(device);surface.Reset();keyboard.Reset();}
            if(!supported) return;
            int h=(int)header, stride=Marshal.ReadInt32(memory,h), reports=Marshal.ReadInt32(memory,h+4);
            if(stride<112 || reports<1 || (long)stride*reports>size-h-8) return;
            byte[] data=new byte[stride*reports]; Marshal.Copy(IntPtr.Add(memory,h+8),data,0,data.Length);
            for(int offset=0;offset<data.Length;offset+=stride)
            {
                if(data[offset]!=5) continue;
                StickerKey previous=surface.Hover;
                bool foreground=ActiveForm==this;
                surface.Frame(data,offset,clock.ElapsedMilliseconds,foreground || outside.Checked,foreground || backgroundGestures.Checked);
                if(previous!=surface.Hover)board.Invalidate();
            }
        } finally { Marshal.FreeHGlobal(memory); }
    }

    [STAThread] static void Main(){Application.EnableVisualStyles(); Application.Run(new TrackpadLab());}
}
