using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
namespace ChunsikDesktop {
    public static class NativeNoteInput {
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
        public static bool IsClickThrough(Window window) {
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            return hwnd != IntPtr.Zero && (GetWindowLongPtr(hwnd, -20).ToInt64() & 0x20) != 0;
        }
        public static void SetClickThrough(Window window, bool enabled) {
            IntPtr hwnd = new WindowInteropHelper(window).Handle; if (hwnd == IntPtr.Zero) return;
            long old = GetWindowLongPtr(hwnd, -20).ToInt64();
            long value = enabled ? old | 0x20 : old & ~0x20;
            if (old == value) return;
            // On a layered WPF HWND this passes input to other applications, not only our UI thread.
            SetWindowLongPtr(hwnd, -20, new IntPtr(value));
            if (IsClickThrough(window) != enabled) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }
    // The body HWND is entirely click-through in ghost mode. Only this small separate
    // owned HWND receives input, so we do not rely on HTTRANSPARENT across UI threads.
    public sealed class GhostNoteHeader : Window {
        readonly Window note;
        bool aligning;
        public GhostNoteHeader(Window owner, Action edit) {
            note = owner;
            Title = "포스트잇 편집 헤더";
            Width = 180; Height = 24; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true; Background = Brushes.Transparent; Topmost = true;
            ShowActivated = false; ShowInTaskbar = false;
            Button button = new Button { Content = "🐾 메모 수정은 여기 꾹!", FontSize = 11,
                Background = new SolidColorBrush(Color.FromRgb(255, 232, 148)), Foreground = Brushes.SaddleBrown,
                BorderBrush = Brushes.Tan, Padding = new Thickness(3, 1, 3, 1),
                ToolTip = "클릭하면 선명하게 편집해요. 저장/취소하거나 다른 창을 누르면 다시 클릭이 통과해요." };
            button.Click += delegate { edit(); }; Content = button;
        }
        public void Align(FrameworkElement anchor, double width) {
            if (aligning || !note.IsVisible || !anchor.IsLoaded) return;
            aligning = true;
            try {
                if (Owner == null) Owner = note;
                Point position = anchor.PointToScreen(new Point(0, 0));
                if (Math.Abs(Width - width) > 0.5) Width = width;
                if (!IsVisible) {
                    Left = note.Left; Top = note.Top; Show();
                }
                Rect bounds = ScreenLayout.BoundsPixels(this);
                if (bounds.IsEmpty || Math.Abs(bounds.Left - position.X) > 0.5 || Math.Abs(bounds.Top - position.Y) > 0.5)
                    ScreenLayout.MovePixels(this, position.X, position.Y);
            } finally { aligning = false; }
        }
    }
}
