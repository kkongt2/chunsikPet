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
    // Small restore tab owned by the minimized note; the visible note uses normal WPF controls.
    public sealed class GhostNoteHeader : Window {
        readonly Window note;
        bool aligning;
        public GhostNoteHeader(Window owner, FrameworkElement control) {
            note = owner; Title = "포스트잇 조작 버튼";
            Width = 24; Height = 24; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true; Background = Brushes.Transparent; Topmost = true;
            ShowActivated = false; ShowInTaskbar = false; Content = control;
        }
        public void Align(FrameworkElement anchor, double width, double height = 24) {
            if (aligning || !note.IsVisible || !anchor.IsLoaded) return;
            aligning = true;
            try {
                if (Owner == null) Owner = note;
                Point position = anchor.PointToScreen(new Point(0, 0));
                if (Math.Abs(Width - width) > 0.5) Width = width;
                if (Math.Abs(Height - height) > 0.5) Height = height;
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
