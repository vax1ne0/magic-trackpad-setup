#nullable disable
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
    public static byte ModifierCode(string name) => name switch
    {
        "Shift" => 16, "Ctrl" => 17, "Alt" => 18, "Win" => 91, _ => 0
    };
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
    public const float WidthMm=160f, DepthMm=114.9f;
    public readonly List<StickerKey> Keys=StickerKey.Load();
    public StickerKey Hover, ModifierContact;
    public bool HasContacts {get;private set;}
    public bool Keyboard=true, ModeEnabled=true, TapMode;
    readonly StickerKey mode;
    StickerKey chordTap;
    long chordStart;
    bool chordMoved;
    public Surface() { mode=Keys.Single(k=>k.Name=="MODE"); }
    public Action<string> Emit;
    public Action ModeChanged;
    public bool Positioning, TwoFingerPointer;
    public bool PointingOrScrolling => Pointing || scrollContact;
    public bool Pointing { get; private set; }
    public Action<bool,bool> PointerChanged;
    public Action<float,float> PointerMoved;
    public Action<bool> PointerButton;
    public bool EdgeSliders;
    public enum EdgeControl { Brightness, Volume }
    public Action<EdgeControl,int> EdgeLevel;
    public Action<bool> EdgeContactChanged;
    EdgeControl? edgeControl;
    void SetEdgeContact(EdgeControl? side)
    {
        if(edgeControl==side)return;
        edgeControl=side;
        EdgeContactChanged?.Invoke(side.HasValue);
    }

    float edgeStartY;
    int lastEdgeLevel = -1;
    const float EdgeWidthMm = 4, EdgeTravelMm = 5, ActiveEdgeWidthMm = 7;
    const float EndMarginMm = 1.5f;

    void UpdateEdge(int count, float x, float y)
    {
        if(!EdgeSliders || Keyboard || Positioning || modeDone || clicked || invalid || count!=1 || origin.Count!=1)
        {
            SetEdgeContact(null);
            return;
        }
        float width=lastEdgeLevel>=0 && peak!=0 ? ActiveEdgeWidthMm : EdgeWidthMm;
        EdgeControl? side=x<=width ? EdgeControl.Brightness :
            x>=WidthMm-width ? EdgeControl.Volume : null;
        if(peak==0)
        {
            SetEdgeContact(side);
            edgeStartY=y;
            lastEdgeLevel=-1;
        }
        if(side!=edgeControl)SetEdgeContact(null);
        if(!edgeControl.HasValue || (lastEdgeLevel<0 && Math.Abs(y-edgeStartY)<EdgeTravelMm))return;
        // Leave a small margin at the physical rim so both endpoints are reachable.
        float percent=Math.Clamp(100*(DepthMm-EndMarginMm-y)/(DepthMm-2*EndMarginMm),0,100);
        // Hysteresis prevents a stationary fingertip from toggling adjacent integers.
        if(lastEdgeLevel>=0 && Math.Abs(percent-lastEdgeLevel)<0.75f)return;
        int level=(int)Math.Round(percent);
        if(level==lastEdgeLevel)return;
        lastEdgeLevel=level;
        EdgeLevel?.Invoke(edgeControl.Value,level);
    }

    public Action<int> PointerScroll;
    bool scrollContact,scrollMoved,scrollAnchorValid;
    PointF scrollAnchor;
    public Action<float,float,bool,long> NormalPointerSample;
    bool pointerClickArmed;
    PointF pointerCenter;
    long pointerStart;
    bool pointerTap, pointerPair;
    const int PointerSettleMs = 80;
    public const int PositionHoldMs=700;
    public Action<bool> CornerTimer;
    long? cornerDown;

    void CancelCorner()
    {
        if(!cornerDown.HasValue)return;
        cornerDown=null;
        CornerTimer?.Invoke(false);
    }

    public void CheckCornerHold(long now)
    {
        if(cornerDown.HasValue && now-cornerDown.Value>=PositionHoldMs)
        {
            CancelCorner();
            modeDone=true;
            Positioning=true;
            ModeChanged();
        }
    }
    public Action<bool> RepeatChanged;
    StickerKey heldKey;

    void SetHeldKey(StickerKey key)
    {
        if (heldKey == key) return;
        heldKey = key;
        RepeatChanged?.Invoke(key != null);
    }
    public Action<bool> GuardFourFingerSwipe;
    readonly Dictionary<uint,PointF> origin=new Dictionary<uint,PointF>();
    int peak;
    bool button, clicked, invalid, moved, gestureDone, modeDone;
    long began;
    PointF fiveStart;
    const float VolumeStartMm = 12, VolumeStepMm = 10;
    float volumeAnchorY;
    bool adjustingVolume;
    public enum Gesture { Tap, Up, Down, Left, Right }
    public readonly Dictionary<Gesture,string> Actions = new()
    {
        [Gesture.Tap]="Play / pause", [Gesture.Up]="Volume up", [Gesture.Down]="Volume down",
        [Gesture.Left]="Previous track", [Gesture.Right]="Next track"
    };

    void RunGesture(Gesture gesture)
    {
        var action = Actions[gesture];
        if (action != "Nothing") Emit(action);
    }

    void ContinueVolume(float y)
    {
        int steps = (int)((y - volumeAnchorY) / VolumeStepMm);
        var gesture = steps < 0 ? Gesture.Up : Gesture.Down;
        if (Actions[gesture] is "Volume up" or "Volume down")
            for (int i = 0; i < Math.Abs(steps); i++) RunGesture(gesture);
        volumeAnchorY += steps * VolumeStepMm;
    }
    StickerKey first;
    public void Reset(bool keepModeContact=false)
    {
        SetEdgeContact(null);
        SetPointerButton(false);
        bool hadPointer=PointingOrScrolling;
        Pointing=scrollContact=scrollMoved=scrollAnchorValid=false;
        if(hadPointer)PointerChanged?.Invoke(false,false);
        if(!keepModeContact)CancelCorner();
        ModifierContact=null; HasContacts=false; chordTap=null;
        SetHeldKey(null);
        if(peak==5 && GuardFourFingerSwipe!=null) GuardFourFingerSwipe(false);
        adjustingVolume=false; origin.Clear(); peak=0; button=clicked=invalid=moved=gestureDone=false; modeDone=keepModeContact && modeDone; Hover=first=null;
    }
    void SetPointerButton(bool down)
    {
        if(pointerClickArmed==down)return;
        pointerClickArmed=down;
        PointerButton?.Invoke(down);
    }

    public void Frame(byte[] data,int offset,long now,bool allowKeys=true,bool allowGestures=true)
    {
        bool down=(data[offset+111]&1)!=0, edge=down&&!button;
        button=down; clicked|=down;
        int count=0, strongest=-1,second=-1;
        float xsum=0,ysum=0;
        StickerKey candidate=null, otherKey=null;
        int modifierCount=0;
        StickerKey modifierKey=null;
        for(int i=0;i<5;i++)
        {
            int p=offset+1+i*21;
            if((data[p]&2)==0)continue;
            if((data[p]&1)==0)invalid=true;
            float x=BitConverter.ToUInt16(data,p+5)*WidthMm/7612;
            float y=BitConverter.ToUInt16(data,p+7)*DepthMm/5065;
            var pt=new PointF(x,y);
            uint id=BitConverter.ToUInt32(data,p+1);
            PointF start;
            if(!origin.TryGetValue(id,out start))origin[id]=pt;
            else if(Math.Abs(x-start.X)>4 || Math.Abs(y-start.Y)>4)moved=true;
            var hit=x>=mode.Bounds.Left && y<=mode.Bounds.Bottom ? mode : Keys.Find(k=>k.Bounds.Contains(pt));
            if(StickerKey.ModifierCode(hit?.Name)!=0){modifierCount++;modifierKey=hit;}
            else otherKey=hit;
            int pressure=BitConverter.ToUInt16(data,p+13);
            if(pressure>strongest){second=strongest;strongest=pressure;candidate=hit;}
            else second=Math.Max(second,pressure);
            count++; xsum+=x;ysum+=y;
        }
        UpdateEdge(count,xsum,ysum);
        NormalPointerSample?.Invoke(xsum,ysum,TwoFingerPointer && !Keyboard && !Positioning &&
            count==1 && !invalid && !down,now);
        bool onCorner=count==1 && candidate==mode;
        if(cornerDown.HasValue)
        {
            if(!ModeEnabled || invalid || moved || (count!=0 && !onCorner))CancelCorner();
            else
            {
                CheckCornerHold(now);
                if(cornerDown.HasValue && (!down || count==0))
                {
                    CancelCorner();
                    modeDone=true;
                    Keyboard=!Keyboard;
                    ModeChanged();
                }
            }
        }
        if(!Pointing && ModeEnabled && !modeDone && !invalid && !moved && onCorner && peak<=1 &&
           (peak==0 || first==mode) && edge && !cornerDown.HasValue)
        {
            if(Positioning)
            {
                // Consume this press through release, including a long hold.
                modeDone=true;
                Positioning=false;
                Keyboard=true;
                ModeChanged();
            }
            else
            {
                cornerDown=now;
                CornerTimer?.Invoke(true);
            }
        }
        if(Positioning)
        {
            Hover=ModifierContact=chordTap=null;
            SetHeldKey(null);
            if(count==0)Reset();
            return;
        }
        // Three fingers landing together can arrive as a brief two-contact report.
        if(Pointing && pointerPair && TwoFingerPointer && Keyboard && ModeEnabled && count==3 &&
           (now-pointerStart>=PointerSettleMs || !pointerTap))
        {
            SetPointerButton(false);
            // Keep the cursor visible from pointing through the last scrolling contact.
            Pointing=false;
            scrollContact=true;
            Hover=ModifierContact=chordTap=null;
            SetHeldKey(null);
        }
        if(Pointing && count>=3)
        {
            SetPointerButton(false);
            Pointing=false;
            PointerChanged?.Invoke(false,false);
        }
        if(scrollContact)
        {
            HasContacts=count>0;
            if(count==3 && !invalid && !down)
            {
                var center=new PointF(xsum/3,ysum/3);
                if(!scrollAnchorValid){scrollAnchor=center;scrollAnchorValid=true;}
                float dy=center.Y-scrollAnchor.Y;
                int steps=(int)(dy/4);
                if(steps!=0 && (scrollMoved || Math.Abs(dy)>Math.Abs(center.X-scrollAnchor.X)))
                {
                    scrollMoved=true;
                    PointerScroll?.Invoke(-steps);
                    scrollAnchor.Y+=steps*4;
                }
            }
            else scrollAnchorValid=false;
            if(count==0){Reset();return;}
            // Let a five-finger gesture form before the first scroll step.
            if(count<5 || scrollMoved)return;
            scrollContact=false;
            PointerChanged?.Invoke(false,false);
        }
        // A modifier held before the second finger keeps its typing shortcut.
        bool modifierHeld=StickerKey.ModifierCode(first?.Name)!=0 && now-began>=110;
        if(!Pointing && TwoFingerPointer && Keyboard && ModeEnabled && count==2 && peak<2 &&
            !modeDone && (!clicked || (edge && peak==0)) && !invalid && !(modifierCount==1 && modifierHeld))
        {
            Pointing=true;
            pointerTap=true;
            pointerClickArmed=false;
            pointerPair=false;
            pointerStart=now;
            SetHeldKey(null);
            Hover=ModifierContact=chordTap=null;
            PointerChanged?.Invoke(true,false);
        }
        if(Pointing)
        {
            HasContacts=count>0;
            pointerTap &= !clicked && !invalid && !moved && count<=2 && origin.Count<=2;
            if(invalid || count!=2 || !down)SetPointerButton(false);
            else if(edge)SetPointerButton(true);
            if(count==2)
            {
                var center=new PointF(xsum/2,ysum/2);
                if(pointerPair)PointerMoved?.Invoke(center.X-pointerCenter.X,center.Y-pointerCenter.Y);
                pointerCenter=center;
            }
            pointerPair=count==2;
            if(count==0)
            {
                Pointing=false;
                PointerChanged?.Invoke(false,pointerTap && now-pointerStart<=280);
                Reset();
            }
            return;
        }
        bool chord = count==2 && modifierCount==1 && otherKey!=null && otherKey!=mode && peak<=2;
        HasContacts=count>0;
        var previousModifier=ModifierContact;
        ModifierContact=chord?modifierKey:null;
        if(chord) candidate=otherKey;
        if(!down || (!chord && count!=1) || candidate!=heldKey || invalid || !allowKeys || !Keyboard || TapMode || previousModifier!=ModifierContact)
            SetHeldKey(null);

        if(TapMode && Keyboard && allowKeys && !invalid && !modeDone)
        {
            if(chord && previousModifier!=ModifierContact)
            {
                chordStart=now;
                chordTap=otherKey;
                chordMoved=false;
            }
            if(chord && (chordTap!=otherKey || down))chordMoved=true;
            if(!chord && previousModifier!=null && chordTap!=null && !chordMoved && now-chordStart<=420 &&
               (count==0 || (count==1 && modifierCount==1)))
            {
                ModifierContact=previousModifier;
                Emit(chordTap.Name);
                ModifierContact=null;
            }
        }
        if(!chord)chordTap=null;
        if(count==0)
        {
            if(!invalid && !modeDone && !gestureDone && now-began<=420 && !moved)
            {
                if(allowGestures && peak==5 && origin.Count==5)RunGesture(Gesture.Tap);
                else if(allowKeys && Keyboard && TapMode && !clicked && peak==1 && origin.Count==1 && first!=null && first.Name!="MODE")Emit(first.Name);
            }
            Reset();return;
        }
        if(peak==0){began=now;first=candidate;}
        Hover=chord || count==1 || strongest>second?candidate:null;
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
        if(allowGestures && count==5 && origin.Count==5)
        {
            float dx=xsum/5-fiveStart.X,dy=ysum/5-fiveStart.Y;
            if(Math.Abs(dx)>=12 && Math.Abs(dx)>1.4*Math.Abs(dy)){gestureDone=true;RunGesture(dx>0?Gesture.Right:Gesture.Left);}
            else if(Math.Abs(dy)>=VolumeStartMm && Math.Abs(dy)>1.4*Math.Abs(dx))
            {
                gestureDone=true;
                var gesture=dy>0?Gesture.Down:Gesture.Up;
                adjustingVolume=Actions[gesture] is "Volume up" or "Volume down";
                volumeAnchorY=fiveStart.Y+Math.Sign(dy)*VolumeStartMm;
                RunGesture(gesture);
                if(adjustingVolume) ContinueVolume(ysum/5);
            }
        }
        if(allowKeys && Keyboard && (!TapMode || chord) && edge && peak<=2 && Hover!=null && Hover!=mode && (peak==1 || StickerKey.ModifierCode(Hover.Name)==0))
        {
            Emit(Hover.Name);
            if (!TapMode && (peak==1 || chord)) SetHeldKey(Hover);
        }
    }
}
