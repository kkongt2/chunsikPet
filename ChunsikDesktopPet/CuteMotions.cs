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
                string[] names = { "chunsik-snack.png", "chunsik-doze.png", "chunsik-welcome.png", "chunsik-look.png", "chunsik-roll.png", "chunsik-focus.png", "chunsik-heart.png", "chunsik-umbrella.png", "chunsik-clean.png", "chunsik-balloon.png" };
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
            double duration = cuteKind == 1 ? 4.8 : cuteKind == 2 ? 3.2 : cuteKind >= 4 ? 4.8 : 2.8;
            double elapsed = now - cuteStarted;
            if (elapsed >= duration) { EndCute(); return false; }
            BitmapSource sprite = cuteSheets[cuteKind][CuteFrameAt(elapsed, duration)];
            Size extent = cuteSizes[cuteKind];
            double scale = Math.Min(size / extent.Height, Math.Max(1, Width - 8) / extent.Width);
            picture.Source = sprite; picture.Width = sprite.PixelWidth * scale; picture.Height = sprite.PixelHeight * scale;
            flip.ScaleX = flip.ScaleY = 1; squirm.Angle = 0; hop.X = hop.Y = 0;
            return true;
        }
        public static BitmapSource FocusPose(double seconds) {
            BitmapSource[] sheet;
            if (!cuteSheets.TryGetValue(6, out sheet)) return ToolPose(false);
            int[] writing = { 2, 3, 4, 5, 6, 5, 4, 3 };
            return sheet[writing[(int)(Math.Max(0, seconds) % 4.8 / 0.6)]];
        }
        void ShowBalloonFrame(int index) {
            BitmapSource sprite = cuteSheets[10][index]; Size extent = cuteSizes[10];
            double scale = Math.Min((Height - 6) / extent.Height, (Width - 8) / extent.Width);
            picture.Source = sprite; picture.Width = sprite.PixelWidth * scale; picture.Height = sprite.PixelHeight * scale;
            flip.ScaleX = flip.ScaleY = 1; squirm.Angle = 0; hop.X = hop.Y = 0;
        }
        void AnimateBalloonFall(double now, double dt) {
            double elapsed = Math.Max(0, now - fallStart);
            if (landedAt < 0) {
                ShowBalloonFrame(2 + (int)(elapsed % 2.4 / 0.6));
                Left = Clamp(fallCenter - Width / 2 + Math.Sin(elapsed * 1.7) * size * 0.12, fallArea.Left, fallArea.Right - Width);
                double target = fallArea.Bottom - Height;
                Top = Math.Min(target, Top + (35 + Math.Min(size * 0.18, 45)) * Math.Max(0, dt));
                if (Top >= target) landedAt = now;
            } else {
                ShowBalloonFrame(7);
                if (now - landedAt >= 0.4) StopFall(true);
            }
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
            if ((paused || state == 2) && now >= nextSnack) { int[] idle = { 1, 1, 4, 4, 5, 7, 9 }; StartCute(idle[Rng.Next(idle.Length)]); return AnimateCute(now); }
            return false;
        }
        void AddCuteMenu(ContextMenu menu) {
            MenuItem motions = new MenuItem { Header = "귀여운 동작" }; menu.Items.Add(motions);
            string[] names = { "고구마 냠냠", "졸다가 꾸벅", "어서 와 손 흔들기", "빼꼼 두리번", "발라당 뒹굴", "집중하기", "하트 보내기", "우산 쓰기", "청소하기", "풍선 둥둥" };
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
