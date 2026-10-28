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
    static Rect Read(FakeCaret caret) {
        Type type = typeof(InputAvoidance).Assembly.GetType("ChunsikDesktop.MsaaCaret");
        return (Rect)type.GetMethod("ReadObject", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
            new object[] { caret, new Rect(-1920, 0, 1920, 1080) });
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
        Console.WriteLine("PASS: MSAA object role/state, screen pixels, geometry and no text access");
    }
}
