using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using System.Runtime.InteropServices;
namespace ChunsikDesktop {
    public static class ScreenLayout {
        [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
        public static Rect BoundsPixels(Window window) {
            RECT r;
            if (!GetWindowRect(new WindowInteropHelper(window).Handle, out r)) return Rect.Empty;
            return new Rect(r.Left, r.Top, Math.Max(0, r.Right-r.Left), Math.Max(0, r.Bottom-r.Top));
        }
        public static Rect AreaPixels(Window window) {
            System.Drawing.Rectangle r = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(window).Handle).WorkingArea;
            return new Rect(r.Left, r.Top, r.Width, r.Height);
        }
        public static void MovePixels(Window window, double x, double y) {
            // Coordinates shared by different HWNDs must stay in physical desktop pixels.
            SetWindowPos(new WindowInteropHelper(window).Handle, IntPtr.Zero, (int)Math.Round(x), (int)Math.Round(y), 0, 0, 0x0015);
        }
        static DispatcherTimer debounce;
        static readonly HashSet<Window> tracked = new HashSet<Window>();
        static bool started;
        public static Rect AreaFor(Window window) {
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            System.Drawing.Rectangle r = System.Windows.Forms.Screen.FromHandle(hwnd).WorkingArea;
            PresentationSource source = PresentationSource.FromVisual(window);
            Matrix from = source == null || source.CompositionTarget == null ? Matrix.Identity : source.CompositionTarget.TransformFromDevice;
            return new Rect(from.Transform(new Point(r.Left, r.Top)), from.Transform(new Point(r.Right, r.Bottom)));
        }
        public static void Start() {
            if (started) return; started = true;
            debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            debounce.Tick += delegate { debounce.Stop(); RepairAll(); };
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplayChanged;
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(Loaded));
        }
        static void Loaded(object sender, RoutedEventArgs args) {
            Window w = sender as Window; if (w == null || tracked.Contains(w)) return; tracked.Add(w);
            HwndSource source = HwndSource.FromHwnd(new WindowInteropHelper(w).Handle);
            HwndSourceHook hook = delegate(IntPtr hwnd, int message, IntPtr a, IntPtr b, ref bool handled) {
                // Crossing a DPI boundary is not a display topology change. WPF handles
                // the window's DPI; never cancel another pet's drag or parachute here.
                if (message == 0x02E0) w.Dispatcher.BeginInvoke(new Action(delegate {
                    if (ReminderService.Current != null) ReminderService.Current.LayoutNotices();
                }), DispatcherPriority.Loaded);
                else if (message == 0x007E || message == 0x001A) Schedule();
                return IntPtr.Zero;
            };
            if (source != null) source.AddHook(hook);
            w.Closed += delegate { tracked.Remove(w); if (source != null && !source.IsDisposed) source.RemoveHook(hook); };
            w.Dispatcher.BeginInvoke(new Action(delegate { Clamp(w); }), DispatcherPriority.Loaded);
        }
        static void DisplayChanged(object sender, EventArgs args) { if (Application.Current != null && !Application.Current.Dispatcher.HasShutdownStarted) Application.Current.Dispatcher.BeginInvoke(new Action(Schedule)); }
        static void Schedule() { if (debounce == null) return; debounce.Stop(); debounce.Start(); }
        public static void Clamp(Window w) {
            if (!w.IsLoaded || !w.IsVisible || w.WindowState != WindowState.Normal || w is NoticeWindow) return;
            Rect area = AreaFor(w); double width = w.ActualWidth, height = w.ActualHeight;
            if (!(w is PetWindow || w is TodoNote || w is NoticeWindow)) {
                w.MinWidth = Math.Min(w.MinWidth, Math.Max(100, area.Width - 20)); w.MinHeight = Math.Min(w.MinHeight, Math.Max(80, area.Height - 20));
                w.MaxWidth = Math.Max(w.MinWidth, area.Width - 20); w.MaxHeight = Math.Max(w.MinHeight, area.Height - 20);
                if (width > w.MaxWidth) w.Width = w.MaxWidth; if (height > w.MaxHeight) { w.SizeToContent = SizeToContent.Manual; w.Height = w.MaxHeight; }
                width = Math.Min(width, w.MaxWidth); height = Math.Min(height, w.MaxHeight);
            }
            if (Double.IsNaN(w.Left)) w.Left = area.Left + 10; if (Double.IsNaN(w.Top)) w.Top = area.Top + 10;
            w.Left = Math.Max(area.Left, Math.Min(area.Right - width, w.Left)); w.Top = Math.Max(area.Top, Math.Min(area.Bottom - height, w.Top));
        }
        static void RepairAll() {
            if (Application.Current == null) return;
            foreach (Window w in Application.Current.Windows) {
                PetWindow pet = w as PetWindow; if (pet != null) { pet.RecoverDisplay(); continue; }
                Clamp(w); TodoNote note = w as TodoNote; if (note != null) note.RememberRecoveredPosition();
            }
            if (ReminderService.Current != null) ReminderService.Current.LayoutNotices();
        }
        public static void Stop() { Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplayChanged; if (debounce != null) debounce.Stop(); started = false; }
    }
}
