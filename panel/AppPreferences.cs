using Microsoft.Win32;

internal static class AppPreferences
{
    const string Key = @"HKEY_CURRENT_USER\Software\AmtPtpControlPanel";

    internal static bool Enabled(string name, bool fallback)
    {
        return ReadInt(name,fallback ? 1 : 0)!=0;
    }

    internal static bool BackgroundInputEnabled()
    {
        return Enabled("StickerEnabled",false) || Enabled("EdgeSliders",false);
    }

    internal static int ReadInt(string name, int fallback)
    {
        try
        {
            var value = Registry.GetValue(Key, name, null);
            return value is int ? (int)value : fallback;
        }
        catch (System.Security.SecurityException) { return fallback; }
        catch (System.UnauthorizedAccessException) { return fallback; }
        catch (System.IO.IOException) { return fallback; }
    }

    internal static void Set(string name, bool value)
    {
        Set(name,value ? 1 : 0);
    }

    internal static void Set(string name, int value)
    {
        Registry.SetValue(Key,name,value,RegistryValueKind.DWord);
    }

    internal static string ReadText(string name, string fallback)
    {
        try { return Registry.GetValue(Key, name, fallback) as string ?? fallback; }
        catch (System.Security.SecurityException) { return fallback; }
        catch (System.UnauthorizedAccessException) { return fallback; }
        catch (System.IO.IOException) { return fallback; }
    }

    internal static void Set(string name, string value)
    {
        Registry.SetValue(Key, name, value, RegistryValueKind.String);
    }

    internal const string LowBatteryAlert = "LowBatteryAlert";
    internal const string IndependentRelease = "IndependentRelease";
}
