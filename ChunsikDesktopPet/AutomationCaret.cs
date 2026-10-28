using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Text;

namespace ChunsikDesktop {
    // A single background MTA worker. A stalled provider cannot block animation or spawn more workers.
    internal static class AutomationCaret {
        static readonly object gate = new object();
        static readonly AutoResetEvent wake = new AutoResetEvent(false);
        static readonly Stopwatch clock = Stopwatch.StartNew();
        static bool started, busy;
        static int generation, requestGeneration;
        static IntPtr requestWindow, resultWindow;
        static double resultAt, nextRequest;
        static Rect result = Rect.Empty;
        static string status = "접근성 감지 대기", lastResult = "조회 전";
        public static string Status { get { lock (gate) return status + " · 최근 결과: " + lastResult; } }
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr window, uint flags);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
        [StructLayout(LayoutKind.Sequential)] struct NativeRect { public int Left, Top, Right, Bottom; }

        public static void Clear() {
            lock (gate) { generation++; result = Rect.Empty; resultWindow = IntPtr.Zero; nextRequest = 0; }
        }
        public static Rect Sample(IntPtr window) {
            lock (gate) {
                double now = clock.Elapsed.TotalSeconds;
                Rect current = window == resultWindow && now - resultAt < 0.6 ? result : Rect.Empty;
                if (!busy && now >= nextRequest) {
                    busy = true; requestWindow = window; requestGeneration = generation;
                    if (!started) {
                        Thread worker = new Thread(Work) { IsBackground = true, Name = "Chunsik accessibility caret" };
                        worker.SetApartmentState(ApartmentState.MTA); worker.Start(); started = true;
                    }
                    status = "MSAA/UIA 조회 중"; wake.Set();
                }
                return current;
            }
        }
        static void Work() {
            while (true) {
                wake.WaitOne();
                IntPtr window; int token;
                lock (gate) { window = requestWindow; token = requestGeneration; }
                double began = clock.Elapsed.TotalSeconds;
                Rect found = Rect.Empty; string source = "UIA 위치 없음";
                try { found = MsaaCaret.Read(window, out source); }
                catch (Exception error) { source = "MSAA 사용 불가: " + error.GetType().Name; }
                if (found.IsEmpty && GetForegroundWindow() == window) {
                    string msaaStatus = source;
                    try { found = Read(window, out source); }
                    catch (Exception error) { source = "UIA 사용 불가: " + error.GetType().Name; }
                    source = msaaStatus + " → " + source;
                }
                double now = clock.Elapsed.TotalSeconds;
                bool active = GetForegroundWindow() == window;
                lock (gate) {
                    if (token == generation && active) {
                        bool timely = now - began <= 0.75;
                        result = timely ? found : Rect.Empty; resultAt = now; resultWindow = window;
                        status = timely ? source : "MSAA/UIA 응답 지연"; lastResult = status;
                        nextRequest = now + (result.IsEmpty ? 1.0 : 0.25);
                    }
                    busy = false;
                }
            }
        }
        // Validate UIA physical pixels; never protect an entire document/window as an input fallback.
        internal static Rect Accept(Rect candidate, Rect owner, bool field) {
            if (candidate.IsEmpty || owner.IsEmpty || Double.IsNaN(candidate.X) || Double.IsNaN(candidate.Y) ||
                Double.IsInfinity(candidate.X) || Double.IsInfinity(candidate.Y) ||
                candidate.Width < 0 || candidate.Height <= 0 || Double.IsInfinity(candidate.Width) || Double.IsInfinity(candidate.Height)) return Rect.Empty;
            if (candidate.Height > (field ? Math.Min(220, owner.Height * 0.35) : 120) ||
                candidate.Width > (field ? Math.Min(1000, owner.Width * 0.9) : 500)) return Rect.Empty;
            Rect value = new Rect(candidate.X, candidate.Y, Math.Max(1, candidate.Width), candidate.Height);
            value.Intersect(owner);
            return value.IsEmpty || value.Width <= 0 || value.Height <= 0 ? Rect.Empty : value;
        }
        static bool BelongsTo(AutomationElement element, IntPtr window) {
            // WebView2's renderer can have a different PID. Validate HWND ancestry instead.
            for (int i = 0; element != null && i < 12; i++) {
                int handle = element.Current.NativeWindowHandle;
                if (handle != 0) return GetAncestor(new IntPtr(handle), 2) == window;
                element = TreeWalker.RawViewWalker.GetParent(element);
            }
            return false;
        }
        static Rect RangeBounds(TextPatternRange range, Rect owner) {
            Rect[] boxes = range.GetBoundingRectangles();
            if (boxes == null || boxes.Length != 1) return Rect.Empty;
            return Accept(boxes[0], owner, false);
        }
        static Rect Read(IntPtr window, out string source) {
            source = "UIA 위치 없음";
            if (GetForegroundWindow() != window) return Rect.Empty;
            AutomationElement focus = AutomationElement.FocusedElement;
            if (focus == null || !focus.Current.HasKeyboardFocus || focus.Current.IsOffscreen ||
                focus.Current.IsPassword || !focus.Current.IsEnabled || !BelongsTo(focus, window)) return Rect.Empty;
            NativeRect native;
            if (!GetWindowRect(window, out native) || native.Right <= native.Left || native.Bottom <= native.Top) return Rect.Empty;
            Rect owner = new Rect(native.Left, native.Top, native.Right - native.Left, native.Bottom - native.Top);
            object pattern;
            TextPattern text = focus.TryGetCurrentPattern(TextPattern.Pattern, out pattern) ? (TextPattern)pattern : null;
            bool editable = false;
            if (focus.TryGetCurrentPattern(ValuePattern.Pattern, out pattern)) editable = !((ValuePattern)pattern).Current.IsReadOnly;
            else if (text != null) {
                object readOnly = text.DocumentRange.GetAttributeValue(TextPattern.IsReadOnlyAttribute);
                editable = readOnly is bool && !(bool)readOnly;
            }
            if (!editable) return Rect.Empty;
            Rect found = Rect.Empty;
            if (text != null && text.SupportedTextSelection != SupportedTextSelection.None) {
                TextPatternRange[] ranges = text.GetSelection();
                // Framework UIA exposes the insertion point as a degenerate selection (no GetText call).
                if (ranges != null && ranges.Length == 1 &&
                    ranges[0].CompareEndpoints(TextPatternRangeEndpoint.Start, ranges[0], TextPatternRangeEndpoint.End) == 0) {
                    TextPatternRange range = ranges[0].Clone();
                    found = RangeBounds(range, owner);
                    if (found.IsEmpty) {
                        range.ExpandToEnclosingUnit(TextUnit.Character);
                        found = RangeBounds(range, owner);
                    }
                    if (found.IsEmpty) {
                        range = ranges[0].Clone(); range.Move(TextUnit.Character, -1);
                        range.ExpandToEnclosingUnit(TextUnit.Character);
                        found = RangeBounds(range, owner);
                    }
                    if (found.IsEmpty) {
                        range = ranges[0].Clone(); range.ExpandToEnclosingUnit(TextUnit.Line);
                        found = RangeBounds(range, owner);
                    }
                    if (!found.IsEmpty) source = "UIA 커서/인접 문자";
                }
            }
            if (found.IsEmpty) {
                found = Accept(focus.Current.BoundingRectangle, owner, true);
                if (!found.IsEmpty) source = "UIA 입력창";
            }
            if (GetForegroundWindow() != window || !focus.Current.HasKeyboardFocus ||
                !Automation.Compare(focus, AutomationElement.FocusedElement)) return Rect.Empty;
            return found;
        }
    }
}
