using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace AmtPtpControlPanel.WinUI3;

internal sealed record AppRelease(Version Version, Uri Download, string Digest, long Size);

internal static class AppUpdates
{
    const string Repository = "vax1ne0/magic-trackpad-setup";
    const long MaxBytes = 256 * 1024 * 1024;
    static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(10) };
    internal static readonly Version Current = new(typeof(AppUpdates).Assembly.GetName().Version!.ToString(3));

    [System.Runtime.InteropServices.DllImport("user32")]
    static extern bool AllowSetForegroundWindow(int processId);

    static AppUpdates() => Client.DefaultRequestHeaders.UserAgent.ParseAdd("MagicTrackpad/" + Current);

    internal static async Task<AppRelease?> Check(CancellationToken token)
    {
        using var response = await Client.GetAsync($"https://api.github.com/repos/{Repository}/releases/latest", token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(token));
        return ReadRelease(json.RootElement, Current);
    }

    internal static AppRelease? ReadRelease(JsonElement root, Version current)
    {
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return null;
        string tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!tag.StartsWith('v') || !Version.TryParse(tag[1..], out var version) || version.Build < 0 ||
            version.Revision >= 0 || tag != "v" + version) throw new InvalidDataException();
        if (version <= current) return null;
        string name = $"Magic-Trackpad-Setup-{version}-x64.msi";
        var url = new Uri($"https://github.com/{Repository}/releases/download/{tag}/{name}");
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            if (asset.GetProperty("name").GetString() != name) continue;
            string digest = asset.GetProperty("digest").GetString() ?? "";
            long size = asset.GetProperty("size").GetInt64();
            if (asset.GetProperty("browser_download_url").GetString() != url.AbsoluteUri ||
                size <= 0 || size > MaxBytes || !digest.StartsWith("sha256:", StringComparison.Ordinal) ||
                digest.Length != 71 || !digest[7..].All(Uri.IsHexDigit)) throw new InvalidDataException();
            return new AppRelease(version, url, digest[7..], size);
        }
        throw new InvalidDataException();
    }

    internal static async Task<string> Download(AppRelease release, string cache, CancellationToken token, Action<long>? reportBytes = null)
    {
        string directory = Path.Combine(cache, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, "update.msi");
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromMinutes(15));
            token = deadline.Token;
            using var response = await Client.GetAsync(release.Download, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var source = await response.Content.ReadAsStreamAsync(token);
            await using var target = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            long total = 0;
            int count;
            while ((count = await source.ReadAsync(buffer, token)) != 0)
            {
                total += count;
                if (total > release.Size) throw new InvalidDataException();
                await target.WriteAsync(buffer.AsMemory(0, count), token);
                hash.AppendData(buffer, 0, count);
                reportBytes?.Invoke(total);
            }
            if (total != release.Size || !Convert.ToHexString(hash.GetHashAndReset()).Equals(release.Digest,
                    StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException();
            return file;
        }
        catch { Discard(file); throw; }
    }

    internal static void Discard(string file)
    {
        try { File.Delete(file); Directory.Delete(Path.GetDirectoryName(file)!); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    internal static void Schedule(string root, string file, AppRelease release)
    {
        string directory = Path.GetDirectoryName(file)!;
        string temporary = Path.Combine(directory, "pending.tmp");
        File.WriteAllLines(temporary, [Path.GetFileName(directory), release.Version.ToString(), release.Digest]);
        File.Move(temporary, Path.Combine(root, "Updates", "pending.txt"), true);
    }

    internal static bool TryStartPending(string root, bool background)
    {
        string cache = Path.Combine(root, "Updates");
        string marker = Path.Combine(cache, "pending.txt");
        if (!File.Exists(marker)) return false;
        // Renaming claims this download before a second launcher can start the same MSI.
        string claim = Path.Combine(cache, Guid.NewGuid().ToString("N") + ".claim");
        try { File.Move(marker, claim); }
        catch (FileNotFoundException) { return false; }
        try
        {
            string[] data = File.ReadAllLines(claim);
            if (data.Length != 3 || !Guid.TryParseExact(data[0], "N", out _) ||
                !Version.TryParse(data[1], out var version) ||
                data[2].Length != 64 || !data[2].All(Uri.IsHexDigit)) return false;
            string file = Path.Combine(cache, data[0], "update.msi");
            if (!File.Exists(file)) return false;
            if (version <= Current) { Discard(file); return false; }
            Restart(root, file, data[2], background, true);
            return true;
        }
        catch
        {
            if (!File.Exists(marker)) File.Move(claim, marker);
            throw;
        }
        finally { File.Delete(claim); }
    }

    internal static void Restart(string root, string file, string digest, bool background = false, bool startTray = false)
    {
        // Read the fixed handoff before MSI replaces the app. Paths travel as data.
        using var stream = typeof(AppUpdates).Assembly.GetManifestResourceStream("UpdateAndRestart.ps1")!;
        using var reader = new StreamReader(stream);
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"))
        {
            UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Environment.SystemDirectory
        };
        foreach (var option in new[] { "-NoProfile", "-NonInteractive", "-Command", reader.ReadToEnd() })
            start.ArgumentList.Add(option);
        start.Environment["PSModulePath"] = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\Modules");
        start.Environment["TRACKPAD_UPDATE_ROOT"] = root;
        start.Environment["TRACKPAD_UPDATE_FILE"] = file;
        start.Environment["TRACKPAD_UPDATE_SHA"] = digest;
        start.Environment["TRACKPAD_PANEL_PID"] = Environment.ProcessId.ToString();
        start.Environment["TRACKPAD_BACKGROUND"] = background ? "1" : "0";
        start.Environment["TRACKPAD_START_TRAY"] = startTray ? "1" : "0";
        using var helper=Process.Start(start)!;
        if(!background)AllowSetForegroundWindow(helper.Id);
    }
}
