using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using ChunsikDesktop;
internal static class AccessibilityCaretTests {
    static readonly Type type = typeof(InputAvoidance).Assembly.GetType("ChunsikDesktop.AccessibilityCaret");
    static object Call(string name, params object[] args) { return type.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, args); }
    static void Set(string name, object value) { type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value); }
    static object Get(string name) { return type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null); }
    static void Check(bool ok, string message) { if (!ok) throw new Exception("FAIL accessibility: " + message); }
    public static void Run() {
        Rect owner = new Rect(-1920, 0, 1920, 1080);
        Rect caret = new Rect(-1000, 900, 0, 24);
        Rect accepted = (Rect)Call("Accept", caret, owner);
        Check(accepted.Width == 1 && accepted.X == -1000, "zero-width caret and negative monitor coordinates");
        Check(((Rect)Call("Accept", owner, owner)).IsEmpty, "whole page rejected");
        Check(((Rect)Call("Accept", new Rect(-1600, 700, 600, 100), owner)).IsEmpty, "input field is not a caret");
        Check(((Rect)Call("Accept", new Rect(-1600, 100, 600, 700), owner)).IsEmpty, "large editor rejected");
        Check(((Rect)Call("Accept", new Rect(20, 20, 20, 20), owner)).IsEmpty, "other monitor rejected");
        Check(((Rect)Call("Accept", new Rect(-1000, 900, 800, 24), owner)).IsEmpty, "oversized line rejected");
        Check(((Rect)Call("Accept", new Rect(-1000, 900, 20, 0), owner)).IsEmpty, "empty height rejected");
        // Simulate an outstanding blocked provider: samples must remain non-blocking, and Clear invalidates results.
        Set("busy", true);
        try {
            Stopwatch clock = (Stopwatch)Get("clock");
            Set("result", accepted); Set("resultWindow", new IntPtr(123)); Set("resultAt", clock.Elapsed.TotalSeconds);
            Check((Rect)Call("Sample", new IntPtr(123)) == accepted, "fresh matching result reused");
            Check(((Rect)Call("Sample", new IntPtr(124))).IsEmpty, "different foreground cannot reuse result");
            Set("resultAt", clock.Elapsed.TotalSeconds - 1);
            Check(((Rect)Call("Sample", new IntPtr(123))).IsEmpty, "stale result discarded while provider blocked");
            int before = (int)Get("generation");
            Call("Clear");
            Check((int)Get("generation") > before && ((Rect)Get("result")).IsEmpty, "clear invalidates pending generation and cached result");
            Check((bool)Get("busy"), "clear does not start another blocked worker");
        } finally { Set("busy", false); Call("Clear"); }
        Console.WriteLine("PASS: accessibility geometry, stale foreground, expiry and bounded worker state");
    }
}
