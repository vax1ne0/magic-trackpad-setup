using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace AmtPtpControlPanel.WinUI3;

internal static class DeviceDetails
{
    const uint Present = 0x100;

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    static extern uint CM_Get_Device_ID_List_SizeW(out uint length, string? filter, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    static extern uint CM_Get_Device_ID_ListW(string? filter, [Out] char[] buffer, uint length, uint flags);

    internal static string Read()
    {
        var lines = new List<string> { $"Magic Trackpad for Windows {AppUpdates.Current}",
            $"Windows build: {Environment.OSVersion.Version.Build}" };
        // The hardware identifiers correspond to the USB and Bluetooth entries in the bundled INF.
        // Only model names leave this method. Instance paths can contain serial numbers or addresses.
        for (int attempt = 0; attempt < 3; attempt++)
        {
            if (CM_Get_Device_ID_List_SizeW(out uint length, null, Present) != 0) break;
            var buffer = new char[length];
            uint status = CM_Get_Device_ID_ListW(null, buffer, length, Present);
            if (status == 0x1A) continue; // CR_BUFFER_SMALL when hardware changes between the two calls
            if (status != 0) break;
            foreach (var id in new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries))
            {
                const string bluetoothPrefix = @"HID\{00001124-0000-1000-8000-00805F9B34FB}_VID&0001004C_PID&";
                int boundary = id.LastIndexOf('\\');
                if (boundary < 0) continue;
                string hardware = id[..boundary].ToUpperInvariant();
                var (model, connection) = hardware switch
                {
                    @"USB\VID_05AC&PID_0324&MI_01" => ("A3120 (USB-C)", "USB"),
                    @"USB\VID_05AC&PID_0265&MI_01" => ("A1535 (Lightning)", "USB"),
                    bluetoothPrefix + "0324&COL01" => ("A3120 (USB-C)", "Bluetooth"),
                    bluetoothPrefix + "0265&COL01" => ("A1535 (Lightning)", "Bluetooth"),
                    _ => ("", "")
                };
                if (model.Length == 0) continue;
                using var device = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\" + id);
                var driverKey = device?.GetValue("Driver") as string;
                using var driver = driverKey == null ? null : Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\" + driverKey);
                string version = driver?.GetValue("DriverVersion") as string ?? "Unknown";
                lines.Add($"Magic Trackpad {model} · {connection} · Driver: {version}");
            }
            if (lines.Count == 2) lines.Add("No supported trackpad detected.");
            return string.Join(Environment.NewLine, lines);
        }
        lines.Add("Device information unavailable.");
        return string.Join(Environment.NewLine, lines);
    }
}
