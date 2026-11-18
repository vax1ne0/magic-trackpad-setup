using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal sealed class KeyBoardView : Control
{
    readonly Surface pad;
    public KeyBoardView(Surface pad){this.pad=pad;DoubleBuffered=true;TabStop=false;}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        float scale=Math.Min(ClientSize.Width/160f,ClientSize.Height/114.9f);
        if(scale<=0)return;
        float left=(ClientSize.Width-160*scale)/2,top=(ClientSize.Height-114.9f*scale)/2;
        foreach(var key in pad.Keys)
        {
            var b=key.Bounds;
            var r=Rectangle.Round(new RectangleF(left+b.X*scale,top+b.Y*scale,b.Width*scale,b.Height*scale));
            bool lit=key==pad.Hover;
            e.Graphics.FillRectangle(lit?Brushes.DodgerBlue:Brushes.WhiteSmoke,r);
            e.Graphics.DrawRectangle(Pens.DarkGray,r);
            TextRenderer.DrawText(e.Graphics,key.Label,Font,r,lit?Color.White:Color.Black,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);
        }
    }
}
internal sealed class KeyOutput
{
    [StructLayout(LayoutKind.Explicit,Size=40)] struct Stroke
    {
        [FieldOffset(0)] public uint type;
        [FieldOffset(8)] public ushort vk;
        [FieldOffset(10)] public ushort scan;
        [FieldOffset(12)] public uint flags;
    }
    [DllImport("user32",SetLastError=true)] static extern uint SendInput(uint count,Stroke[] input,int size);
    [DllImport("user32")] static extern short GetKeyState(int key);
    readonly List<byte> modifiers=new List<byte>();
    readonly Dictionary<string,byte> special=new Dictionary<string,byte> {
        {"Esc",27},{"Backspace",8},{"Enter",13},{"Tab",9},{"Space",32},{"Caps",20},{"Home",36},{"End",35},
        {"PgUp",33},{"PgDn",34},{"Delete",46},{"Left",37},{"Up",38},{"Right",39},{"Down",40},
        {"Play / pause",179},{"Next track",176},{"Previous track",177},{"Volume up",175},{"Volume down",174}
    };
    public void Reset(){modifiers.Clear();}
    static Stroke Key(byte vk,bool up)
    {
        bool extended=(vk>=33 && vk<=40)||vk==46||vk==91||vk>=174;
        return new Stroke{type=1,vk=vk,flags=(uint)((up?2:0)|(extended?1:0))};
    }
    public string Send(string name)
    {
        byte mod=(byte)(name=="Shift"?16:name=="Ctrl"?17:name=="Alt"?18:name=="Win"?91:0);
        if(mod!=0)
        {
            if(modifiers.Remove(mod))return name+" off";
            modifiers.Add(mod);return name+" ready for next key";
        }
        var strokes=new List<Stroke>();
        byte vk;
        bool media=special.TryGetValue(name,out vk)&&vk>=174;
        var active=media?new List<byte>():new List<byte>(modifiers);
        if(!media)modifiers.Clear();
        bool shift=active.Contains(16);
        if(name.Length==1 && !active.Exists(m=>m!=16))
        {
            const string lower="`1234567890-=[]\\;',./";
            const string upper="~!@#$%^&*()_+{}|:\"<>?";
            char ch=name[0];
            int index=lower.IndexOf(ch);
            if(char.IsLetter(ch))ch=(shift^((GetKeyState(20)&1)!=0))?char.ToUpperInvariant(ch):char.ToLowerInvariant(ch);
            else if(shift && index>=0)ch=upper[index];
            strokes.Add(new Stroke{type=1,scan=ch,flags=4});strokes.Add(new Stroke{type=1,scan=ch,flags=6});
        }
        else
        {
            if(!special.TryGetValue(name,out vk)){
                if(name.Length!=1)return "Unknown key";
                char ch=name[0];
                const string punctuation="`-=[]\\;',./";
                byte[] codes={192,189,187,219,221,220,186,222,188,190,191};
                int index=punctuation.IndexOf(ch);vk=index<0?(byte)ch:codes[index];
            }
            foreach(byte m in active)strokes.Add(Key(m,false));
            strokes.Add(Key(vk,false));strokes.Add(Key(vk,true));
            active.Reverse();foreach(byte m in active)strokes.Add(Key(m,true));
        }
        var input=strokes.ToArray();
        return SendInput((uint)input.Length,input,Marshal.SizeOf(typeof(Stroke)))==input.Length?name:"Windows declined key input";
    }
}
