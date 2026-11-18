using Microsoft.Win32;

namespace AmtPtpControlPanel.WinUI3;

// Match one-finger cursor travel over short windows instead of comparing individual
// reports, which arrive asynchronously with Windows cursor updates.
internal sealed class PointerSpeed
{
    readonly double[] gain;
    static readonly double[] speeds=[30,100,250]; // mm/s
    bool sampling;
    double startX,startY;
    int cursorX,cursorY;
    long started;

    public PointerSpeed()
    {
        const string key=@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\PrecisionTouchPad";
        int setting=Registry.GetValue(key,"CursorSpeed",10) as int? ?? 10;
        double scale=Math.Clamp(setting,1,20)/10.0;
        gain=[14*scale,24*scale,36*scale];
    }
    public void EndSample()=>sampling=false;

    public void Observe(double x,double y,int px,int py,long now)
    {
        long elapsed=now-started;
        if(sampling && elapsed<80)return;
        if(sampling && elapsed<=180)
        {
            double dx=x-startX,dy=y-startY, distance=Math.Sqrt(dx*dx+dy*dy);
            double sx=px-cursorX,sy=py-cursorY,pixels=Math.Sqrt(sx*sx+sy*sy);
            // Discard stops, edge clipping, warps and unrelated mouse motion.
            if(distance>=2 && dx*sx+dy*sy>0.94*distance*pixels)
            {
                double ratio=pixels/distance, speed=1000*distance/elapsed;
                if(ratio>=2 && ratio<=120)
                {
                    int bin=speed<65?0:speed<175?1:2;
                    gain[bin]+=0.25*(ratio-gain[bin]);
                }
            }
        }
        sampling=true;startX=x;startY=y;cursorX=px;cursorY=py;started=now;
    }
    public double Gain(double mmPerSecond)
    {
        int i=mmPerSecond<=speeds[1]?0:1;
        double blend=Math.Clamp((mmPerSecond-speeds[i])/(speeds[i+1]-speeds[i]),0,1);
        return gain[i]+blend*(gain[i+1]-gain[i]);
    }
}
