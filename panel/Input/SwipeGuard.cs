using System.Runtime.InteropServices;
namespace AmtPtpControlPanel.WinUI3;

// Optional Windows.UI.Input.TouchpadGesturesController ABI, for Windows builds
// newer than the minimum SDK. Slots 0–5 belong to IInspectable.
internal sealed class SwipeGuard : IDisposable
{
    [DllImport("combase")] static extern int WindowsCreateString([MarshalAs(UnmanagedType.LPWStr)] string text,int length,out nint handle);
    [DllImport("combase")] static extern int WindowsDeleteString(nint handle);
    [DllImport("combase")] static extern int RoGetActivationFactory(nint name,in Guid iid,out nint factory);
    delegate int GetBool(nint self,out byte value);
    delegate int Create(nint self,out nint instance);
    delegate int Enable(nint self,byte value);
    delegate int SetKinds(nint self,uint flags);
    static T Method<T>(nint instance,int slot) where T:Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance),slot*IntPtr.Size));
    nint controller;
    readonly Enable enable;
    SwipeGuard(nint ptr) { controller=ptr;enable=Method<Enable>(ptr,7); }
    public static SwipeGuard? Open()
    {
        const string name="Windows.UI.Input.TouchpadGesturesController";
        nint factory=0,instance=0,guard=0;
        if(WindowsCreateString(name,name.Length,out var text)<0)return null;
        try {
            if(RoGetActivationFactory(text,new Guid("207ef171-1a73-51cd-a694-8840e09dbafa"),out factory)<0 ||
               Method<GetBool>(factory,6)(factory,out var supported)<0 || supported==0 ||
               Method<Create>(factory,7)(factory,out instance)<0)return null;
            var iid=new Guid("28c13cdd-e068-549f-89c6-1a440c6fc327");
            if(Marshal.QueryInterface(instance,in iid,out guard)<0 ||
               Method<Enable>(guard,7)(guard,0)<0 || Method<SetKinds>(guard,9)(guard,6)<0)return null;
            var result=new SwipeGuard(guard);guard=0;return result;
        } finally {
            if(guard!=0)Marshal.Release(guard);
            if(instance!=0)Marshal.Release(instance);
            if(factory!=0)Marshal.Release(factory);
            WindowsDeleteString(text);
        }
    }
    public void Set(bool value) { if(controller!=0)enable(controller,(byte)(value?1:0)); }
    public void Dispose(){if(controller!=0){Set(false);Marshal.Release(controller);controller=0;}}
}
