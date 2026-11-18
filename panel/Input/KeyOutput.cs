using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
    public static bool CapsLockOn => (GetKeyState(0x14) & 1) != 0;
    readonly List<byte> modifiers=new List<byte>();
    readonly Dictionary<string,byte> special=new Dictionary<string,byte> {
        {"Esc",27},{"Backspace",8},{"Enter",13},{"Tab",9},{"Space",32},{"Caps",20},{"Home",36},{"End",35},
        {"PgUp",33},{"PgDn",34},{"Delete",46},{"Left",37},{"Up",38},{"Right",39},{"Down",40},
        {"Play / pause",179},{"Next track",176},{"Previous track",177},{"Volume up",175},{"Volume down",174}
    };
    Stroke[] repeatInput = Array.Empty<Stroke>();
    public bool CanRepeat => repeatInput.Length > 0;
    public bool Repeat() => CanRepeat && SendInput((uint)repeatInput.Length, repeatInput, Marshal.SizeOf<Stroke>()) == repeatInput.Length;
    public void Reset(){modifiers.Clear();repeatInput=Array.Empty<Stroke>();}
    static Stroke Key(byte vk,bool up)
    {
        bool extended=(vk>=33 && vk<=40)||vk==46||vk==91||vk>=174;
        return new Stroke{type=1,vk=vk,flags=(uint)((up?2:0)|(extended?1:0))};
    }
    public string Send(string name,byte heldModifier=0)
    {
        repeatInput = Array.Empty<Stroke>();
        byte mod=StickerKey.ModifierCode(name);
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
        if(!media && heldModifier!=0 && !active.Contains(heldModifier))active.Add(heldModifier);
        bool shift=active.Contains(16);
        if(name.Length==1 && !active.Exists(m=>m!=16))
        {
            const string lower="`1234567890-=[]\\;',./";
            const string upper="~!@#$%^&*()_+{}|:\"<>?";
            char ch=name[0];
            int index=lower.IndexOf(ch);
            if(char.IsLetter(ch))ch=(shift^CapsLockOn)?char.ToUpperInvariant(ch):char.ToLowerInvariant(ch);
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
        if (SendInput((uint)input.Length,input,Marshal.SizeOf<Stroke>()) != input.Length)
            return "Windows declined key input";
        if (!media && name != "Caps") repeatInput = input;
        return name;
    }
}
