using Microsoft.Win32;

internal static class AppPreferences
{
    const string Key = @"HKEY_CURRENT_USER\Software\AmtPtpControlPanel";

    internal static bool Enabled(string name, bool fallback)
    {
        try
        {
            var value = Registry.GetValue(Key, name, null);
            return value is int ? (int)value != 0 : fallback;
        }
        catch (System.Security.SecurityException) { return fallback; }
        catch (System.UnauthorizedAccessException) { return fallback; }
        catch (System.IO.IOException) { return fallback; }
    }

    internal static void Set(string name, bool value)
    {
        Registry.SetValue(Key, name, value ? 1 : 0, RegistryValueKind.DWord);
    }

    internal const string LowBatteryAlert = "LowBatteryAlert";
    internal const string IndependentRelease = "IndependentRelease";
}
