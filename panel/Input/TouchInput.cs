using System.Runtime.InteropServices;
using System.Text;

namespace AmtPtpControlPanel.WinUI3;

internal sealed class TouchInput : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] struct Device { public ushort page,usage; public uint flags; public IntPtr window; }
    [DllImport("user32",SetLastError=true)] static extern bool RegisterRawInputDevices(Device[] devices,uint count,uint size);
    [DllImport("user32")] static extern uint GetRawInputData(IntPtr raw,uint command,IntPtr buffer,ref uint size,uint header);
    [DllImport("user32",CharSet=CharSet.Unicode)] static extern uint GetRawInputDeviceInfo(IntPtr device,uint command,StringBuilder? buffer,ref uint chars);

    delegate nint WindowProc(nint w,uint msg,nint wp,nint lp,nuint id,nuint data);
    [DllImport("comctl32")] static extern bool SetWindowSubclass(nint w,WindowProc proc,nuint id,nuint data);
    [DllImport("comctl32")] static extern bool RemoveWindowSubclass(nint w,WindowProc proc,nuint id);
    [DllImport("comctl32")] static extern nint DefSubclassProc(nint w,uint msg,nint wp,nint lp);
    readonly nint window;
    readonly WindowProc proc;
    readonly Action<byte[],int> receive;
    readonly Action reset;
    nint lastDevice;
    readonly HashSet<nint> otherDevices = new();
    public TouchInput(nint window,Action<byte[],int> receive,Action reset)
    {
        this.window=window;this.receive=receive;this.reset=reset;proc=Dispatch;
        if(!SetWindowSubclass(window,proc,731,0))throw new InvalidOperationException("Window input unavailable");
        var d=new Device{page=13,usage=5,flags=0x2100,window=window};
        if(!RegisterRawInputDevices([d],1,(uint)Marshal.SizeOf<Device>())){
            RemoveWindowSubclass(window,proc,731);
            throw new InvalidOperationException("Trackpad input unavailable");
        }
    }
    nint Dispatch(nint w,uint msg,nint wp,nint lp,nuint id,nuint data)
    {
        if(msg==255)Read(lp);
        if(msg==0x00FE && wp==2) otherDevices.Remove(lp);
        if(msg==0x00FE && wp==2 && lp==lastDevice)
        {
            lastDevice=0;
            reset();
        }
        return DefSubclassProc(w,msg,wp,lp);
    }
    public void Dispose()
    {
        RegisterRawInputDevices([new Device{page=13,usage=5,flags=1}],1,(uint)Marshal.SizeOf<Device>());
        RemoveWindowSubclass(window,proc,731);
    }
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
            if(device!=lastDevice)
            {
                if(otherDevices.Contains(device))return;
                if(!IsTrackpad(device)){otherDevices.Add(device);return;}
                lastDevice=device;
                reset();
            }
            int h=(int)header, stride=Marshal.ReadInt32(memory,h), reports=Marshal.ReadInt32(memory,h+4);
            if(stride<112 || reports<1 || (long)stride*reports>size-h-8) return;
            byte[] data=new byte[stride*reports]; Marshal.Copy(IntPtr.Add(memory,h+8),data,0,data.Length);
            for(int offset=0;offset<data.Length;offset+=stride)
            {
                if(data[offset]!=5) continue;

                receive(data,offset);
            }
        } finally { Marshal.FreeHGlobal(memory); }
    }

}
