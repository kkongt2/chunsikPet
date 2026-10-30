using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ChunsikDesktop;
internal static class CuteMotionTests {
    static readonly BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly BindingFlags shared = BindingFlags.Static | BindingFlags.NonPublic;
    static void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL cute motion: " + name); }
    static object Call(PetWindow pet, string method, params object[] args) { return typeof(PetWindow).GetMethod(method, instance).Invoke(pet, args); }
    static object Get(PetWindow pet, string name) { return typeof(PetWindow).GetField(name, instance).GetValue(pet); }
    public static void Run() {
        object originals = typeof(PetWindow).GetField("frames", shared).GetValue(null);
        typeof(PetWindow).GetMethod("LoadCuteMotions", shared).Invoke(null, new object[] { Path.GetFullPath("ChunsikDesktopPet") });
        Check(Object.ReferenceEquals(originals, typeof(PetWindow).GetField("frames", shared).GetValue(null)), "loader preserves base walking frames");
        var sheets = (Dictionary<int, BitmapSource[]>)typeof(PetWindow).GetField("cuteSheets", shared).GetValue(null);
        Check(sheets.Count == 3, "all three embedded sheets decode");
        foreach (BitmapSource[] frames in sheets.Values) { Check(frames.Length == 8, "eight frames"); foreach (BitmapSource frame in frames) Check(frame.IsFrozen && frame.PixelWidth > 0 && frame.PixelHeight > 0, "frames decoded and frozen"); }
        Check(PetWindow.CuteFrameAt(0, 4) == 0 && PetWindow.CuteFrameAt(2, 4) == 4 && PetWindow.CuteFrameAt(4, 4) == 7, "frame order and endpoint bounded");
        var pool = (List<PetWindow>)typeof(PetWindow).GetField("Pets", shared).GetValue(null);
        PetWindow placeholder = new PetWindow(true); pool.Add(placeholder);
        PetWindow pet = new PetWindow();
        try {
            pet.Show(); Application.Current.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(delegate {}));
            ((DispatcherTimer)Get(pet, "timer")).Stop();
            for (int kind = 1; kind <= 3; kind++) {
                Call(pet, "StartCute", kind); double start = (double)Get(pet, "cuteStarted");
                Check((bool)Call(pet, "AnimateCute", start + 0.1), "motion starts");
                Check(!(bool)Call(pet, "AnimateCute", start + 10) && (int)Get(pet, "cuteKind") == 0, "motion ends once");
            }
            Call(pet, "StartCute", 1); Call(pet, "SetFrame", 7);
            Check((int)Get(pet, "cuteKind") == 0, "interaction pose interrupts motion");
            Check((bool)Call(pet, "DozeBeforeSleep", 0.0), "first idle episode starts doze");
            double doze = (double)Get(pet, "cuteStarted");
            Check(!(bool)Call(pet, "DozeBeforeSleep", doze + 5) && !(bool)Call(pet, "DozeBeforeSleep", doze + 6), "doze does not repeat before sleep");
            PetWindow.WelcomeAll(); Check((bool)Get(pet, "welcomePending"), "recovery queues welcome");
        } finally { pet.Close(); pool.Remove(placeholder); placeholder.Close(); }
        Console.WriteLine("PASS: three sprite sheets, frame order, completion, interruption and doze/welcome lifecycle");
    }
}
