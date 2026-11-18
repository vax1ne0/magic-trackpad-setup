using System.Management;
using System.Threading.Channels;
using NAudio.CoreAudioApi;

namespace AmtPtpControlPanel.WinUI3;

internal sealed class EdgeLevels : IDisposable
{
    // Slow display firmware must not block input or build up a queue of old levels.
    readonly Dictionary<Surface.EdgeControl,Channel<int>> levels = new();
    readonly CancellationTokenSource lifetime = new();

    public EdgeLevels(Action<string> status)
    {
        foreach(var control in Enum.GetValues<Surface.EdgeControl>())
        {
            var channel=Channel.CreateBounded<int>(new BoundedChannelOptions(1)
                { SingleReader=true, SingleWriter=true, FullMode=BoundedChannelFullMode.DropOldest });
            levels.Add(control,channel);
            _=Task.Run(()=>ApplyLevels(control,channel.Reader,status,lifetime.Token));
        }
    }
    public void Set(Surface.EdgeControl control,int level)=>levels[control].Writer.TryWrite(level);

    static async Task ApplyLevels(Surface.EdgeControl control,ChannelReader<int> reader,Action<string> status,CancellationToken token)
    {
        try
        {
            await foreach(int level in reader.ReadAllAsync(token))
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    if(control==Surface.EdgeControl.Volume)
                    {
                        using var devices=new MMDeviceEnumerator();
                        using var output=devices.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia);
                        output.AudioEndpointVolume.MasterVolumeLevelScalar=level/100f;
                    }
                    else SetBrightness(level);
                    status($"{control} {level}%");
                }
                catch(Exception ex) when(ex is ManagementException or System.Runtime.InteropServices.COMException
                    or UnauthorizedAccessException or InvalidOperationException)
                {
                    status(control==Surface.EdgeControl.Volume ? "Volume is unavailable for this audio output." :
                        "Windows could not adjust brightness on this display.");
                }
                await Task.Delay(65,token);
            }
        }
        catch(OperationCanceledException) when(token.IsCancellationRequested) { }
    }

    static void SetBrightness(int level)
    {
        using var query=new ManagementObjectSearcher(@"root\WMI","SELECT * FROM WmiMonitorBrightnessMethods WHERE Active=TRUE");
        using var displays=query.Get();
        foreach(ManagementObject display in displays)
        {
            using(display)
            using(var args=display.GetMethodParameters("WmiSetBrightness"))
            {
                args["Timeout"]=0u;
                args["Brightness"]=(byte)level;
                using var result=display.InvokeMethod("WmiSetBrightness",args,null);
                return;
            }
        }
        throw new InvalidOperationException();
    }

    public void Dispose()
    {
        lifetime.Cancel();
        foreach(var channel in levels.Values)channel.Writer.TryComplete();
    }
}
