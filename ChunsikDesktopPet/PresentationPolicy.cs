using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
namespace ChunsikDesktop {
    public static class PresentationPolicy {
        [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint process);
        static readonly string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChunsikDesktopPet", "fullscreen.txt");
        static DateTime until; static bool indefinite; static DispatcherTimer timer;
        static readonly Dictionary<Window, double> hidden = new Dictionary<Window, double>();
        public static bool FullscreenEnabled { get; private set; }
        public static bool IsSuppressed { get; private set; }
        public static string Status { get { return indefinite ? "직접 해제할 때까지" : until > DateTime.Now ? until.ToString("HH:mm") + "까지" : IsSuppressed ? "전체 화면" : "꺼짐"; } }
        public static void SetOpacity(Window w, double opacity) {
            if (hidden.ContainsKey(w)) { hidden[w] = opacity; w.Opacity = 0; } else w.Opacity = opacity;
        }
        static void SaveQuiet() {
            AlertState state = AlertStateStore.Current.State; state.QuietIndefinite = indefinite;
            state.QuietUntilUtc = until == DateTime.MinValue ? DateTime.MinValue : until.ToUniversalTime();
            if (!AlertStateStore.Current.Save()) MessageBox.Show(AlertStateStore.Current.Warning, "방해 금지 저장");
        }
        public static void Start() {
            AlertState state = AlertStateStore.Current.State;
            indefinite = state.QuietIndefinite;
            until = state.QuietUntilUtc > DateTime.UtcNow ? state.QuietUntilUtc.ToLocalTime() : DateTime.MinValue;
            IsSuppressed = indefinite || until > DateTime.Now;
            if (AlertStateStore.Current.Warning != null) MessageBox.Show(AlertStateStore.Current.Warning, "알림 재개 정보");
            try { FullscreenEnabled = File.Exists(path) && File.ReadAllText(path).Trim() == "on"; } catch { }
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) }; timer.Tick += delegate { Refresh(); }; timer.Start();
        }
        public static void Set(int minutes) { indefinite = minutes < 0; until = minutes > 0 ? DateTime.Now.AddMinutes(minutes) : DateTime.MinValue; SaveQuiet(); Refresh(); }
        public static void ToggleFullscreen() {
            bool value = !FullscreenEnabled;
            try { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, value ? "on" : "off"); FullscreenEnabled = value; Refresh(); }
            catch (Exception e) { MessageBox.Show("전체 화면 설정 저장 실패: " + e.Message); }
        }
        static bool Fullscreen() {
            IntPtr hwnd = GetForegroundWindow(); uint pid; GetWindowThreadProcessId(hwnd, out pid);
            if (pid == Process.GetCurrentProcess().Id) return false;
            RECT r; if (!GetWindowRect(hwnd, out r)) return false;
            System.Drawing.Rectangle b = System.Windows.Forms.Screen.FromHandle(hwnd).Bounds;
            return Math.Abs(r.Left-b.Left) <= 2 && Math.Abs(r.Top-b.Top) <= 2 && Math.Abs(r.Right-b.Right) <= 2 && Math.Abs(r.Bottom-b.Bottom) <= 2;
        }
        static void Refresh() {
            if (Application.Current == null) return;
            if (!indefinite && until != DateTime.MinValue && until <= DateTime.Now) { until = DateTime.MinValue; SaveQuiet(); }
            IsSuppressed = indefinite || until > DateTime.Now || (FullscreenEnabled && Fullscreen());
            foreach (Window w in Application.Current.Windows) {
                if (!(w is PetWindow || w is TodoNote || w is NoticeWindow || w is TimeToolWindow || w is UndoToast)) continue;
                if (IsSuppressed) { if (!hidden.ContainsKey(w)) hidden[w] = w.Opacity; w.Opacity = 0; w.IsHitTestVisible = false; }
                else if (hidden.ContainsKey(w)) { w.Opacity = hidden[w]; w.IsHitTestVisible = true; hidden.Remove(w); }
            }
            foreach (Window w in new List<Window>(hidden.Keys)) { bool live = false; foreach (Window open in Application.Current.Windows) if (open == w) live = true; if (!live) hidden.Remove(w); }
        }
    }
}
