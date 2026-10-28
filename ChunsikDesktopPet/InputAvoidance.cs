using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;

namespace ChunsikDesktop {
    // Geometry only: no keyboard hooks or text contents. Win32 first, bounded asynchronous UIA fallback.
    // All pets share a 4 Hz sample; UIA providers never run on the animation/UI thread.
    public static class InputAvoidance {
        static readonly Stopwatch clock = Stopwatch.StartNew();
        static readonly uint ownProcess = (uint)Process.GetCurrentProcess().Id;
        static double nextSample;
        static Rect caret = Rect.Empty;
        static IntPtr sampledWindow;
        public static string DetectionStatus { get; private set; }
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] struct GUITHREADINFO {
            public uint Size, Flags;
            public IntPtr Active, Focus, Capture, MenuOwner, MoveSize, Caret;
            public RECT Bounds;
        }
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetGUIThreadInfo(uint thread, ref GUITHREADINFO info);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool ClientToScreen(IntPtr window, ref POINT point);
        [DllImport("user32.dll")] static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);
        [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool LogicalToPhysicalPointForPerMonitorDPI(IntPtr window, ref POINT point);

        public static void Clear() { caret = Rect.Empty; nextSample = 0; sampledWindow = IntPtr.Zero; AutomationCaret.Clear(); }
        public static Rect CaretPixels() {
            PetPreferences prefs = PetPreferences.Current;
            if (prefs == null || !prefs.Options.AvoidCursor || PresentationPolicy.IsSuppressed || prefs.InputIdleSeconds > 3) {
                Clear(); return Rect.Empty;
            }
            double now = clock.Elapsed.TotalSeconds;
            IntPtr active = GetForegroundWindow();
            if (active != sampledWindow) { caret = Rect.Empty; nextSample = 0; AutomationCaret.Clear(); sampledWindow = active; }
            if (now < nextSample) return caret;
            nextSample = now + 0.25; caret = Rect.Empty;
            IntPtr foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero) return caret;
            uint process;
            uint thread = GetWindowThreadProcessId(foreground, out process);
            if (thread == 0 || process == ownProcess) return caret;
            caret = NativeCaret(foreground, thread);
            if (!caret.IsEmpty) { AutomationCaret.Clear(); DetectionStatus = "Win32"; return caret; }
            caret = AutomationCaret.Sample(foreground);
            DetectionStatus = AutomationCaret.Status;
            return caret;
        }
        static Rect NativeCaret(IntPtr foreground, uint thread) {
            GUITHREADINFO info = new GUITHREADINFO { Size = (uint)Marshal.SizeOf(typeof(GUITHREADINFO)) };
            if (!GetGUIThreadInfo(thread, ref info) || info.Caret == IntPtr.Zero || info.Focus == IntPtr.Zero || (info.Flags & 0x1E) != 0) return Rect.Empty;
            if (info.Bounds.Bottom <= info.Bounds.Top || info.Bounds.Right < info.Bounds.Left) return Rect.Empty;
            POINT first = new POINT { X = info.Bounds.Left, Y = info.Bounds.Top };
            POINT last = new POINT { X = info.Bounds.Right, Y = info.Bounds.Bottom };
            // rcCaret is expressed in the owner window's logical coordinates.
            // Temporarily match its DPI context, then normalize to physical screen pixels.
            IntPtr previous = IntPtr.Zero;
            try {
                previous = SetThreadDpiAwarenessContext(GetWindowDpiAwarenessContext(info.Caret));
                if (previous == IntPtr.Zero || !ClientToScreen(info.Caret, ref first) || !ClientToScreen(info.Caret, ref last) ||
                    !LogicalToPhysicalPointForPerMonitorDPI(info.Caret, ref first) || !LogicalToPhysicalPointForPerMonitorDPI(info.Caret, ref last)) return Rect.Empty;
            } catch (EntryPointNotFoundException) { return Rect.Empty; }
            finally { if (previous != IntPtr.Zero) SetThreadDpiAwarenessContext(previous); }
            // Discard stale focus/closed-window data; blink phase is deliberately ignored.
            if (GetForegroundWindow() != foreground || last.Y <= first.Y || last.X < first.X) return Rect.Empty;
            return new Rect(first.X, first.Y, Math.Max(1, last.X - first.X), last.Y - first.Y);
        }

        // Return a reachable horizontal position outside the protected input area.
        // If neither side fits, prefer the edge with the smaller overlap without bouncing.
        public static bool TryEscape(Rect pet, Rect workArea, Rect protectedArea, out double target) {
            target = pet.Left;
            if (pet.IsEmpty || workArea.IsEmpty || protectedArea.IsEmpty || !pet.IntersectsWith(protectedArea)) return false;
            double min = workArea.Left, max = Math.Max(min, workArea.Right - pet.Width);
            double left = protectedArea.Left - pet.Width - 2, right = protectedArea.Right + 2;
            bool leftFits = left >= min && left <= max, rightFits = right >= min && right <= max;
            if (leftFits && rightFits) target = Math.Abs(pet.Left - left) <= Math.Abs(pet.Left - right) ? left : right;
            else if (leftFits) target = left;
            else if (rightFits) target = right;
            else {
                double leftOverlap = Math.Max(0, Math.Min(min + pet.Width, protectedArea.Right) - Math.Max(min, protectedArea.Left));
                double rightOverlap = Math.Max(0, Math.Min(max + pet.Width, protectedArea.Right) - Math.Max(max, protectedArea.Left));
                target = leftOverlap < rightOverlap ? min : rightOverlap < leftOverlap ? max : pet.Left < (min + max) / 2 ? min : max;
            }
            return true;
        }
    }
}

