using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using Accessibility;

namespace ChunsikDesktop {
    internal static class MsaaCaret {
        const uint ObjidCaret = unchecked((uint)-8);
        const int ChildSelf = 0, RoleCaret = 7;
        const int Invisible = 0x8000, Offscreen = 0x10000, Unavailable = 1;
        static readonly Guid AccessibleId = new Guid("618736E0-3C3D-11CF-810C-00AA00389B71");
        [StructLayout(LayoutKind.Sequential)] struct NativeRect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] struct GuiInfo {
            public uint Size, Flags;
            public IntPtr Active, Focus, Capture, MenuOwner, MoveSize, Caret;
            public NativeRect Bounds;
        }
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll")] static extern bool GetGUIThreadInfo(uint thread, ref GuiInfo info);
        [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr window, uint flags);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr window, StringBuilder name, int count);
        [DllImport("user32.dll")] static extern uint GetDpiForSystem();
        [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
        [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
        [DllImport("oleacc.dll", ExactSpelling = true)]
        static extern int AccessibleObjectFromWindow(IntPtr window, uint objectId, ref Guid iid,
            [MarshalAs(UnmanagedType.Interface)] out IAccessible accessible);

        static bool FocusInfo(IntPtr foreground, out GuiInfo info) {
            uint process; uint thread = GetWindowThreadProcessId(foreground, out process);
            info = new GuiInfo { Size = (uint)Marshal.SizeOf(typeof(GuiInfo)) };
            return thread != 0 && GetGUIThreadInfo(thread, ref info) && info.Focus != IntPtr.Zero &&
                (info.Flags & 0x1E) == 0 && GetAncestor(info.Focus, 2) == foreground;
        }
        internal static bool IsChromiumClass(string name) {
            return name != null && name.StartsWith("Chrome_", StringComparison.Ordinal);
        }
        static bool IsChromiumWindow(IntPtr window) {
            StringBuilder name = new StringBuilder(256);
            return GetClassName(window, name, name.Capacity) > 0 && IsChromiumClass(name.ToString());
        }
        static double PhysicalScale(IntPtr window, bool chromium) {
            if (!chromium) return 1;
            try {
                uint system = GetDpiForSystem(), x, y;
                // Use the real HWND monitor, never the already inflated accLocation point.
                if (system != 0 && GetDpiForMonitor(MonitorFromWindow(window, 2), 0, out x, out y) == 0 && x != 0)
                    return (double)x / system;
            } catch (EntryPointNotFoundException) { }
              catch (DllNotFoundException) { }
            return 1;
        }
        internal static Rect ReadObject(IAccessible accessible, Rect owner, double physicalScale = 1) {
            // OBJID_CARET is a separate object, not an editable control. Never ask for Name/Value/Text.
            object role = accessible.get_accRole(ChildSelf), state = accessible.get_accState(ChildSelf);
            if (!(role is int) || (int)role != RoleCaret || !(state is int) ||
                ((int)state & (Invisible | Offscreen | Unavailable)) != 0) return Rect.Empty;
            int x, y, width, height;
            accessible.accLocation(out x, out y, out width, out height, ChildSelf);
            if (width < 0 || height <= 0 || physicalScale <= 0 || Double.IsNaN(physicalScale) || Double.IsInfinity(physicalScale)) return Rect.Empty;
            // Chromium OBJID_CARET can return screen coordinates multiplied by systemDpi / monitorDpi.
            // Undo that factor for BOTH screen position and size before clipping or size validation.
            // Other providers use scale 1; there is no ClientToScreen or monitor-origin translation.
            return AccessibilityCaret.Accept(new Rect(x * physicalScale, y * physicalScale,
                width * physicalScale, height * physicalScale), owner);
        }
        internal static Rect Read(IntPtr foreground, out string source) {
            source = "MSAA 커서 없음";
            GuiInfo before;
            if (GetForegroundWindow() != foreground || !FocusInfo(foreground, out before)) return Rect.Empty;
            NativeRect bounds;
            if (!GetWindowRect(foreground, out bounds) || bounds.Right <= bounds.Left || bounds.Bottom <= bounds.Top) return Rect.Empty;
            Rect owner = new Rect(bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);
            // Chromium/WebView2 can expose the caret on the renderer HWND or a hosting ancestor.
            List<IntPtr> candidates = new List<IntPtr>();
            IntPtr at = before.Focus;
            for (int i = 0; at != IntPtr.Zero && i < 8; i++) {
                if (GetAncestor(at, 2) != foreground) break;
                if (!candidates.Contains(at)) candidates.Add(at);
                if (at == foreground) break;
                at = GetAncestor(at, 1);
            }
            if (before.Caret != IntPtr.Zero && GetAncestor(before.Caret, 2) == foreground && !candidates.Contains(before.Caret))
                candidates.Insert(0, before.Caret);
            if (!candidates.Contains(foreground)) candidates.Add(foreground);
            bool chromium = false;
            foreach (IntPtr candidate in candidates) chromium |= IsChromiumWindow(candidate);
            double scale = PhysicalScale(before.Focus, chromium);
            foreach (IntPtr candidate in candidates) {
                IAccessible accessible = null;
                try {
                    Guid iid = AccessibleId;
                    int hr = AccessibleObjectFromWindow(candidate, ObjidCaret, ref iid, out accessible);
                    if (hr < 0 || accessible == null) continue;
                    Rect found = ReadObject(accessible, owner, scale);
                    if (found.IsEmpty) continue;
                    GuiInfo after;
                    if (GetForegroundWindow() != foreground || !FocusInfo(foreground, out after) || before.Focus != after.Focus) return Rect.Empty;
                    NativeRect currentBounds;
                    if (!GetWindowRect(foreground, out currentBounds) || !bounds.Equals(currentBounds) ||
                        PhysicalScale(before.Focus, chromium) != scale) return Rect.Empty;
                    source = candidate == before.Focus ? "MSAA OBJID_CARET (포커스 창)" : "MSAA OBJID_CARET (호스트 창)";
                    if (scale != 1) source += " · Chromium DPI 보정 ×" + scale.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                    return found;
                } catch (COMException) { source = "MSAA 공급자 오류"; }
                  catch (InvalidCastException) { source = "MSAA 객체 형식 불일치"; }
                finally { if (accessible != null && Marshal.IsComObject(accessible)) Marshal.ReleaseComObject(accessible); }
            }
            return Rect.Empty;
        }
    }
}
