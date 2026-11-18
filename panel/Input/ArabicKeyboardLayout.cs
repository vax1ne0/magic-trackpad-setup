using System.Runtime.InteropServices;
using System.Text;

internal static class ArabicKeyboardLayout
{
    [DllImport("user32")] static extern nint GetForegroundWindow();
    [DllImport("user32")] static extern uint GetWindowThreadProcessId(nint window, nint processId);
    [DllImport("user32")] static extern nint GetKeyboardLayout(uint thread);
    [DllImport("user32")] static extern uint MapVirtualKeyExW(uint code, uint mapType, nint layout);
    [DllImport("user32", CharSet = CharSet.Unicode)]
    static extern int ToUnicodeEx(uint key, uint scan, byte[] state, StringBuilder result, int capacity, uint flags, nint layout);

    public static nint ForForegroundWindow()
    {
        uint thread = GetWindowThreadProcessId(GetForegroundWindow(), 0);
        nint layout = thread == 0 ? 0 : GetKeyboardLayout(thread);
        return (layout.ToInt64() & 0x3ff) == 1 ? layout : 0; // PRIMARYLANGID == LANG_ARABIC
    }

    // Windows can pair the Arabic input language with a substituted US layout.
    // Use Arabic (101) for that profile without changing the system keyboard.
    const string StandardKeys = "`QWERTYUIOP[]ASDFGHJKL;'ZXCVBNM,./";
    static readonly string[] StandardLetters = "ذ ض ص ث ق ف غ ع ه خ ح ج د ش س ي ب ل ا ت ن م ك ط ئ ء ؤ ر لا ى ة و ز ظ".Split(' ');
    static readonly string[] StandardShift = "ّ َ ً ُ ٌ لإ إ ‘ ÷ × ؛ < > ِ ٍ ] [ لأ أ ـ ، / : \" ~ ْ } { لآ آ ’ , . ؟".Split(' ');

    public static string? Letter(string name, nint layout, bool shift = false)
    {
        if (layout == 0 || name.Length != 1) return null;
        if (((layout.ToInt64() >> 16) & 0xffff) == 0x0409)
        {
            int position = StandardKeys.IndexOf(name[0]);
            return position < 0 ? null : (shift ? StandardShift : StandardLetters)[position];
        }
        // Physical positions on the sticker, indexed from the Q, A and Z scan codes.
        char ch = name[0];
        int index = "QWERTYUIOP[]".IndexOf(ch);
        int scan = 0x10 + index;
        if (index < 0) {
            index = "ASDFGHJKL;'`".IndexOf(ch);
            scan = 0x1e + index;
        }
        if (index < 0) {
            index = "ZXCVBNM,./".IndexOf(ch);
            scan = 0x2c + index;
        }
        if (ch == '\\') scan = 0x2b;
        else if (index < 0) return null;

        var state = new byte[256];
        var buffer = new StringBuilder(8);
        uint vk = MapVirtualKeyExW((uint)scan, 3, layout);
        // Flag 4 lets us read the layout without altering Windows' dead-key state.
        string Translate() {
            int count = ToUnicodeEx(vk, (uint)scan, state, buffer, buffer.Capacity, 4, layout);
            return count > 0 ? buffer.ToString(0, count) : string.Empty;
        }
        string letter = Translate();
        if (!letter.Any(c => c is >= '\u0600' and <= '\u06ff' && char.IsLetter(c))) return null;
        if (!shift) return letter;
        state[0x10] = 0x80;
        return Translate();
    }
}
