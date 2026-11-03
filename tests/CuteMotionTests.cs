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
    static void Set(PetWindow pet, string name, object value) { typeof(PetWindow).GetField(name, instance).SetValue(pet, value); }
    public static void Run() {
        object originals = typeof(PetWindow).GetField("frames", shared).GetValue(null);
        typeof(PetWindow).GetMethod("LoadCuteMotions", shared).Invoke(null, new object[] { Path.GetFullPath("ChunsikDesktopPet") });
        Check(Object.ReferenceEquals(originals, typeof(PetWindow).GetField("frames", shared).GetValue(null)), "loader preserves base walking frames");
        var sheets = (Dictionary<int, BitmapSource[]>)typeof(PetWindow).GetField("cuteSheets", shared).GetValue(null);
        Check(sheets.Count == 10, "all ten embedded sheets decode");
        foreach (BitmapSource[] frames in sheets.Values) { Check(frames.Length == 8, "eight frames"); foreach (BitmapSource frame in frames) Check(frame.IsFrozen && frame.PixelWidth > 0 && frame.PixelHeight > 0, "frames decoded and frozen"); }
        for (int i = 0; i < 8; i++) {
            Int32Rect crop = ((CroppedBitmap)sheets[5][i]).SourceRect;
            Check(i < 4 ? crop.Y + crop.Height <= 451 : crop.Y >= 500, "roll frames exclude neighboring row fragments");
        }
        Check(((CroppedBitmap)sheets[5][0]).SourceRect.Y + sheets[5][0].PixelHeight >= 449, "roll upper feet are not clipped at midpoint");
        Check(PetWindow.CuteFrameAt(0, 4) == 0 && PetWindow.CuteFrameAt(2, 4) == 4 && PetWindow.CuteFrameAt(4, 4) == 7, "frame order and endpoint bounded");
        var pool = (List<PetWindow>)typeof(PetWindow).GetField("Pets", shared).GetValue(null);
        PetWindow placeholder = new PetWindow(true); pool.Add(placeholder);
        PetWindow pet = new PetWindow();
        try {
            pet.Show(); Application.Current.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(delegate {}));
            ((DispatcherTimer)Get(pet, "timer")).Stop();
            for (int kind = 1; kind <= 10; kind++) {
                Call(pet, "StartCute", kind); double start = (double)Get(pet, "cuteStarted");
                Check((bool)Call(pet, "AnimateCute", start + 0.1), "motion starts");
                Check(!(bool)Call(pet, "AnimateCute", start + 10) && (int)Get(pet, "cuteKind") == 0, "motion ends once");
            }
            Call(pet, "StartCute", 1); Call(pet, "SetFrame", 7);
            Check((int)Get(pet, "cuteKind") == 0, "interaction pose interrupts motion");
            Check((bool)Call(pet, "DozeBeforeSleep", 0.0), "first idle episode starts doze");
            double doze = (double)Get(pet, "cuteStarted");
            Check(!(bool)Call(pet, "DozeBeforeSleep", doze + 5) && !(bool)Call(pet, "DozeBeforeSleep", doze + 6), "doze does not repeat before sleep");
            Check(Object.ReferenceEquals(PetWindow.FocusPose(0), sheets[6][2]) && Object.ReferenceEquals(PetWindow.FocusPose(4.8), sheets[6][2]), "focus loops writing poses");
            Set(pet, "falling", true); Set(pet, "balloonFall", true); Set(pet, "wingFall", false);
            Set(pet, "fallArea", new Rect(0, 0, 1920, 1080)); Set(pet, "fallStart", 0.0); Set(pet, "landedAt", -1.0); Set(pet, "fallCenter", 500.0);
            Call(pet, "LayoutPet"); pet.Top = 10;
            Call(pet, "AnimateFall", 0.5, 0.1);
            Check(pet.Top > 10 && (bool)Get(pet, "falling"), "balloon descends");
            var image = (System.Windows.Controls.Image)Get(pet, "picture");
            Check(Object.ReferenceEquals(image.Source, sheets[10][2]), "balloon sprite replaces standard pose");
            pet.Top = 1080 - pet.Height;
            Call(pet, "AnimateFall", 1.0, 0.1); Call(pet, "AnimateFall", 1.5, 0.1);
            Check(!(bool)Get(pet, "falling") && !(bool)Get(pet, "balloonFall") && Math.Abs(pet.Top + pet.Height - 1080) < 0.01, "balloon landing restores floor position");
            Set(pet, "falling", true); Set(pet, "balloonFall", true); Call(pet, "LayoutPet"); pet.Top = 20;
            double feet = pet.Top + pet.Height; Call(pet, "StopFall", false);
            Check(!(bool)Get(pet, "balloonFall") && Math.Abs(pet.Top + pet.Height - feet) < 0.01, "grabbing balloon clears effect and preserves feet");
            PetWindow.WelcomeAll(); Check((bool)Get(pet, "welcomePending"), "recovery queues welcome");
        } finally { pet.Close(); pool.Remove(placeholder); placeholder.Close(); }
        Console.WriteLine("PASS: ten sprite sheets, focus and balloon fall, frame order, completion, interruption and doze/welcome lifecycle");
    }
}
