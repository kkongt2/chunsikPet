using System;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;
using ChunsikDesktop;
internal static class MsaaCaretTests {
    sealed class FakeCaret : AccessibleObject {
        public System.Drawing.Rectangle Rectangle = new System.Drawing.Rectangle(-800, 700, 2, 24);
        public AccessibleRole ObjectRole = AccessibleRole.Caret;
        public AccessibleStates ObjectState = AccessibleStates.None;
        public override System.Drawing.Rectangle Bounds { get { return Rectangle; } }
        public override AccessibleRole Role { get { return ObjectRole; } }
        public override AccessibleStates State { get { return ObjectState; } }
        public override string Name { get { throw new Exception("Must not read name"); } set { throw new Exception("Must not write name"); } }
        public override string Value { get { throw new Exception("Must not read text"); } set { throw new Exception("Must not write text"); } }
    }
    static Rect Read(FakeCaret caret, double scale = 1, Rect? owner = null) {
        Type type = typeof(InputAvoidance).Assembly.GetType("ChunsikDesktop.MsaaCaret");
        return (Rect)type.GetMethod("ReadObject", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
            new object[] { caret, owner ?? new Rect(-1920, 0, 1920, 1080), scale });
    }
    static void Check(bool ok, string message) { if (!ok) throw new Exception("FAIL MSAA: " + message); }
    public static void Run() {
        FakeCaret caret = new FakeCaret();
        Rect found = Read(caret);
        Check(found == new Rect(-800, 700, 2, 24), "screen pixels preserved without rescaling; no text reads");
        caret.Rectangle = new System.Drawing.Rectangle(-800, 700, 0, 24);
        Check(Read(caret).Width == 1, "visible zero-width caret normalized");
        foreach (AccessibleStates state in new[] { AccessibleStates.Invisible, AccessibleStates.Offscreen, AccessibleStates.Unavailable }) {
            caret.ObjectState = state; Check(Read(caret).IsEmpty, "hidden/unavailable caret rejected");
        }
        caret.ObjectState = AccessibleStates.None; caret.ObjectRole = AccessibleRole.Text;
        Check(Read(caret).IsEmpty, "input field is not a caret");
        caret.ObjectRole = AccessibleRole.Caret;
        caret.Rectangle = new System.Drawing.Rectangle(-800, 700, 600, 24);
        Check(Read(caret).IsEmpty, "whole input rectangle rejected");
        caret.Rectangle = new System.Drawing.Rectangle(100, 700, 2, 24);
        Check(Read(caret).IsEmpty, "outside foreground rejected");
        caret.Rectangle = new System.Drawing.Rectangle(-800, 700, 2, 0);
        Check(Read(caret).IsEmpty, "empty bounds rejected");
        // 150% system / 100% secondary: inflated global coordinates must be corrected before clipping.
        caret.Rectangle = new System.Drawing.Rectangle(-2400, 1050, 3, 36);
        Check(Read(caret, 96.0 / 144) == new Rect(-1600, 700, 2, 24), "150/100 negative monitor correction before owner clipping");
        caret.Rectangle = new System.Drawing.Rectangle(4200, -300, 3, 36);
        Check(Read(caret, 96.0 / 144, new Rect(2560, -1080, 1920, 2160)) == new Rect(2800, -200, 2, 24), "right/above monitor global coordinates");
        caret.Rectangle = new System.Drawing.Rectangle(-1200, 300, 6, 150);
        Check(Read(caret, 96.0 / 144) == new Rect(-800, 200, 4, 100), "size validation happens after normalization");
        Check(Read(caret).IsEmpty, "non-Chromium oversized height still rejected");
        caret.Rectangle = new System.Drawing.Rectangle(-800, 400, 2, 24);
        Check(Read(caret, 144.0 / 96) == new Rect(-1200, 600, 3, 36), "100/150 reverse ratio");
        Check(Read(caret, 144.0 / 144) == Read(caret), "equal DPI unchanged");
        Check(Read(caret, 0).IsEmpty && Read(caret, Double.NaN).IsEmpty, "invalid scale rejected");
        caret.Rectangle = new System.Drawing.Rectangle(-1200, 300, 0, 36);
        Check(Read(caret, 96.0 / 144).Width == 1, "zero width normalized after scaling");
        Type msaa = typeof(InputAvoidance).Assembly.GetType("ChunsikDesktop.MsaaCaret");
        MethodInfo classify = msaa.GetMethod("IsChromiumClass", BindingFlags.Static | BindingFlags.NonPublic);
        Check((bool)classify.Invoke(null, new object[] { "Chrome_RenderWidgetHostHWND" }) &&
            (bool)classify.Invoke(null, new object[] { "Chrome_WidgetWin_1" }) &&
            !(bool)classify.Invoke(null, new object[] { "Notepad" }), "Chromium/WebView2 HWND scope");
        Console.WriteLine("PASS: MSAA object role/state, screen pixels, geometry and no text access");
    }
}
