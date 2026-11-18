using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

internal sealed class StickerKey
{
    public RectangleF Bounds;
    public string Label, Name;
    public static List<StickerKey> Load()
    {
        var keys=new List<StickerKey>();
        using(var stream=typeof(StickerKey).Assembly.GetManifestResourceStream("Keyboard.svg"))
        {
            StickerKey key=null;
            foreach(var e in XDocument.Load(stream).Root.Elements())
            {
                if(e.Name.LocalName=="rect")
                {
                    Func<string,float> f=n=>float.Parse((string)e.Attribute(n),CultureInfo.InvariantCulture);
                    key=new StickerKey {Bounds=new RectangleF(f("x"),f("y"),f("width"),f("height"))};
                    keys.Add(key);
                }
                else if(e.Name.LocalName=="text")
                {
                    key.Label=key.Label==null?e.Value:key.Label+" "+e.Value;
                    key.Name=key.Label.StartsWith("MODE")?"MODE":e.Value.Trim();
                }
            }
        }
        return keys.Where(k=>k.Name!=null).ToList();
    }
}
internal sealed class Surface
{
    public readonly List<StickerKey> Keys=StickerKey.Load();
    public StickerKey Hover;
    public bool Keyboard=true, TapMode;
    public Action<string> Emit;
    public Action ModeChanged;
    public Action<bool> GuardFourFingerSwipe;
    readonly Dictionary<uint,PointF> origin=new Dictionary<uint,PointF>();
    int peak;
    bool button, clicked, invalid, moved, gestureDone, modeDone;
    long began;
    PointF fiveStart;
    const float VolumeStartMm = 12, VolumeStepMm = 10;
    float volumeAnchorY;
    bool adjustingVolume;

    void ContinueVolume(float y)
    {
        int steps = (int)((y - volumeAnchorY) / VolumeStepMm);
        for (int i = 0; i < Math.Abs(steps); i++)
            Emit(steps < 0 ? "Volume up" : "Volume down");
        volumeAnchorY += steps * VolumeStepMm;
    }
    StickerKey first;
    public void Reset()
    {
        if(peak==5 && GuardFourFingerSwipe!=null) GuardFourFingerSwipe(false);
        adjustingVolume=false; origin.Clear(); peak=0; button=clicked=invalid=moved=gestureDone=modeDone=false; Hover=first=null;
    }
    public void Frame(byte[] data,int offset,long now,bool allowKeys=true,bool allowGestures=true)
    {
        bool down=(data[offset+111]&1)!=0, edge=down&&!button;
        button=down; clicked|=down;
        int count=0, strongest=-1,second=-1;
        float xsum=0,ysum=0;
        StickerKey candidate=null;
        for(int i=0;i<5;i++)
        {
            int p=offset+1+i*21;
            if((data[p]&2)==0)continue;
            if((data[p]&1)==0)invalid=true;
            float x=BitConverter.ToUInt16(data,p+5)*160f/7612;
            float y=BitConverter.ToUInt16(data,p+7)*114.9f/5065;
            var pt=new PointF(x,y);
            uint id=BitConverter.ToUInt32(data,p+1);
            PointF start;
            if(!origin.TryGetValue(id,out start))origin[id]=pt;
            else if(Math.Abs(x-start.X)>4 || Math.Abs(y-start.Y)>4)moved=true;
            var hit=Keys.Find(k=>k.Bounds.Contains(pt));
            int pressure=BitConverter.ToUInt16(data,p+13);
            if(pressure>strongest){second=strongest;strongest=pressure;candidate=hit;}
            else second=Math.Max(second,pressure);
            count++; xsum+=x;ysum+=y;
        }
        if(count==0)
        {
            if(!invalid && !modeDone && !gestureDone && now-began<=420 && !moved)
            {
                if(allowGestures && peak==5 && origin.Count==5)Emit("Play / pause");
                else if(allowKeys && Keyboard && TapMode && !clicked && peak==1 && origin.Count==1 && first!=null && first.Name!="MODE")Emit(first.Name);
            }
            Reset();return;
        }
        if(peak==0){began=now;first=candidate;}
        Hover=count==1 || strongest>second?candidate:null;
        if(count==5 && peak<5)
        {
            fiveStart=new PointF(xsum/5,ysum/5);
            if(allowGestures && !invalid && GuardFourFingerSwipe!=null) GuardFourFingerSwipe(true);
        }
        peak=Math.Max(peak,count);
        if(invalid || modeDone)return;
        if(adjustingVolume)
        {
            if(count==5 && origin.Count==5) ContinueVolume(ysum/5);
            else adjustingVolume=false;
            return;
        }
        if(gestureDone)return;
        if(allowKeys && count==1 && peak==1 && !moved && first!=null && first.Name=="MODE" && Hover==first && (edge || now-began>=650))
        {Keyboard=!Keyboard;modeDone=true;ModeChanged();return;}
        if(allowGestures && count==5 && origin.Count==5)
        {
            float dx=xsum/5-fiveStart.X,dy=ysum/5-fiveStart.Y;
            if(Math.Abs(dx)>=12 && Math.Abs(dx)>1.4*Math.Abs(dy)){gestureDone=true;Emit(dx>0?"Next track":"Previous track");}
            else if(Math.Abs(dy)>=VolumeStartMm && Math.Abs(dy)>1.4*Math.Abs(dx))
            {
                gestureDone=true;
                adjustingVolume=true;
                volumeAnchorY=fiveStart.Y+Math.Sign(dy)*VolumeStartMm;
                Emit(dy>0?"Volume down":"Volume up");
                ContinueVolume(ysum/5);
            }
        }
        if(allowKeys && Keyboard && !TapMode && edge && peak<5 && Hover!=null && Hover.Name!="MODE")Emit(Hover.Name);
    }
}
