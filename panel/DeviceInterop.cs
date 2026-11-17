using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

internal static class TrackpadDevice
{
    const uint GetBattery = 0x222004;
    const uint Reload = 0x222000;

    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)]
    static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security,
        uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", EntryPoint="DeviceIoControl")]
    static extern bool ReadControl(SafeFileHandle handle, uint code, IntPtr input, uint inputSize,
        out uint value, uint outputSize, out uint returned, IntPtr overlapped);

    static bool Bluetooth(uint command, out uint value)
    {
        value = 0;
        using (var handle = CreateFile(@"\\.\AmtPtpControlDeviceUm", 0xC0000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero))
        {
            uint count;
            uint size = command == GetBattery ? 4u : 0u;
            return !handle.IsInvalid && ReadControl(handle, command, IntPtr.Zero, 0, out value,
                size, out count, IntPtr.Zero) && count == size;
        }
    }

    static bool Usb(byte[] request, out byte value)
    {
        value = 0;
        try
        {
            using (var pipe = new NamedPipeClientStream(".", "AmtPtpControlPanelUsbUmInterface",
                PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.Identification))
            {
                pipe.Connect(300);
                if (!pipe.WriteAsync(request, 0, request.Length).Wait(1000)) return false;
                byte[] reply = new byte[2];
                for (int offset = 0; offset < reply.Length;)
                {
                    var read = pipe.ReadAsync(reply, offset, reply.Length-offset);
                    if (!read.Wait(1000) || read.Result == 0) return false;
                    offset += read.Result;
                }
                value = reply[1];
                return reply[0] == 0;
            }
        }
        catch (TimeoutException) { return false; }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        catch (AggregateException ex) {
            if (!(ex.GetBaseException() is IOException) && !(ex.GetBaseException() is UnauthorizedAccessException)) throw;
            return false;
        }
    }

    [DllImport("kernel32.dll", EntryPoint="DeviceIoControl")]
    static extern bool WriteControl(SafeFileHandle handle, uint code, ref int feedback, uint size,
        IntPtr output, uint outputSize, out uint returned, IntPtr overlapped);

    static bool PulseBluetooth(SafeFileHandle handle, int feedback)
    {
        uint returned;
        return WriteControl(handle, 0x0022A008, ref feedback, 4, IntPtr.Zero, 0, out returned, IntPtr.Zero);
    }

    static bool PulseUsb(int feedback)
    {
        byte ignored;
        return Usb(new byte[] { 2, (byte)feedback, (byte)(feedback >> 8), (byte)(feedback >> 16) }, out ignored);
    }

    internal static bool Preview(int press, int release)
    {
        using (var handle = CreateFile(@"\\.\AmtPtpHapticDeviceUm", 0x40000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero))
        {
            if (!handle.IsInvalid && PulseBluetooth(handle, press))
            {
                System.Threading.Thread.Sleep(140);
                return PulseBluetooth(handle, release);
            }
        }
        if (!PulseUsb(press)) return false;
        System.Threading.Thread.Sleep(140);
        return PulseUsb(release);
    }

    internal static string Read(out int percent)
    {
        uint wireless;
        byte wired;
        if (Bluetooth(GetBattery, out wireless) && wireless <= 100) { percent=(int)wireless; return "Bluetooth"; }
        if (Usb(new byte[] { 1 }, out wired) && wired <= 100) { percent=wired; return "USB"; }
        percent=-1;
        return "Unavailable";
    }
    internal static void ReloadSettings()
    {
        byte wired;
        uint wireless;
        Usb(new byte[] { 0 }, out wired);
        Bluetooth(Reload, out wireless);
    }
}
