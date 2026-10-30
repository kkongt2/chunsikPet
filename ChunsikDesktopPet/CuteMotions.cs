using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace ChunsikDesktop {
    public partial class PetWindow {
        static readonly Dictionary<int, BitmapSource[]> cuteSheets = new Dictionary<int, BitmapSource[]>();
        static readonly Dictionary<int, Size> cuteSizes = new Dictionary<int, Size>();
        int cuteKind;
        double cuteStarted, nextSnack = 30 + Rng.NextDouble() * 60;
        bool dozeDone, welcomePending;
        static void LoadCuteMotions(string folder) {
            BitmapSource[] original = frames; double originalW = maxW, originalH = maxH;
            try {
                string[] names = { "chunsik-snack.png", "chunsik-doze.png", "chunsik-welcome.png" };
                for (int i = 0; i < names.Length; i++) {
                    LoadFrames(Path.Combine(folder, names[i]));
                    cuteSheets[i + 1] = frames; cuteSizes[i + 1] = new Size(maxW, maxH);
                }
            } finally { frames = original; maxW = originalW; maxH = originalH; }
        }
        public static int CuteFrameAt(double elapsed, double duration) {
            return Math.Max(0, Math.Min(7, (int)(Math.Max(0, elapsed) * 8 / duration)));
        }
        void StartCute(int kind) {
            if (giant || IsTodoCompanion || !cuteSheets.ContainsKey(kind)) return;
            EndCute(); CancelCue(); cuteKind = kind; cuteStarted = clock.Elapsed.TotalSeconds;
        }
        void EndCute() {
            if (cuteKind == 0) return;
            cuteKind = 0; nextSnack = clock.Elapsed.TotalSeconds + 60 + Rng.NextDouble() * 120;
            flip.ScaleX = flip.ScaleY = 1; squirm.Angle = 0; hop.X = hop.Y = 0;
            SetFrame(paused ? 6 : 4);
        }
        bool AnimateCute(double now) {
            if (cuteKind == 0) return false;
            double duration = cuteKind == 1 ? 4.8 : cuteKind == 2 ? 3.2 : 2.8;
            double elapsed = now - cuteStarted;
            if (elapsed >= duration) { EndCute(); return false; }
            BitmapSource sprite = cuteSheets[cuteKind][CuteFrameAt(elapsed, duration)];
            Size extent = cuteSizes[cuteKind];
            double scale = Math.Min(size / extent.Height, Math.Max(1, Width - 8) / extent.Width);
            picture.Source = sprite; picture.Width = sprite.PixelWidth * scale; picture.Height = sprite.PixelHeight * scale;
            flip.ScaleX = flip.ScaleY = 1; squirm.Angle = 0; hop.X = hop.Y = 0;
            return true;
        }
        bool DozeBeforeSleep(double now) {
            if (!dozeDone && !sleeping) { EndCute(); StartCute(2); dozeDone = true; }
            return cuteKind == 2 && AnimateCute(now);
        }
        public static void WelcomeAll() {
            foreach (PetWindow pet in Pets) if (!pet.giant) pet.welcomePending = true;
        }
        bool IdleCute(double now) {
            if (welcomePending) { welcomePending = false; StartCute(3); }
            if (cuteKind != 0) return AnimateCute(now);
            if ((paused || state == 2) && now >= nextSnack) { StartCute(1); return AnimateCute(now); }
            return false;
        }
        void AddCuteMenu(ContextMenu menu) {
            MenuItem motions = new MenuItem { Header = "귀여운 동작" }; menu.Items.Add(motions);
            string[] names = { "고구마 냠냠", "졸다가 꾸벅", "어서 와 손 흔들기" };
            for (int i = 0; i < names.Length; i++) {
                int kind = i + 1;
                motions.Items.Add(Item(names[i], delegate {
                    Wake(); EndReaction(); EndPayday(); StopNoticeAnimation(); StopFall(false); StartCute(kind);
                }));
            }
            menu.Opened += delegate { motions.IsEnabled = !giant; };
        }
    }
}
