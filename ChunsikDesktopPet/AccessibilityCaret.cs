using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace ChunsikDesktop {
    // A single background MTA worker. A stalled provider cannot block animation or spawn more workers.
    internal static class AccessibilityCaret {
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
                    status = "MSAA 조회 중"; wake.Set();
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
                Rect found = Rect.Empty; string source = "MSAA 커서 없음";
                try { found = MsaaCaret.Read(window, out source); }
                catch (Exception error) { source = "MSAA 사용 불가: " + error.GetType().Name; }
                double now = clock.Elapsed.TotalSeconds;
                bool active = GetForegroundWindow() == window;
                lock (gate) {
                    if (token == generation && active) {
                        bool timely = now - began <= 0.75;
                        result = timely ? found : Rect.Empty; resultAt = now; resultWindow = window;
                        status = timely ? source : "MSAA 응답 지연"; lastResult = status;
                        nextRequest = now + (result.IsEmpty ? 1.0 : 0.25);
                    }
                    busy = false;
                }
            }
        }
        // Validate physical pixels; never protect an entire document/window.
        internal static Rect Accept(Rect candidate, Rect owner) {
            if (candidate.IsEmpty || owner.IsEmpty || Double.IsNaN(candidate.X) || Double.IsNaN(candidate.Y) ||
                Double.IsInfinity(candidate.X) || Double.IsInfinity(candidate.Y) ||
                candidate.Width < 0 || candidate.Height <= 0 || Double.IsInfinity(candidate.Width) || Double.IsInfinity(candidate.Height)) return Rect.Empty;
            if (candidate.Height > 120 || candidate.Width > 64) return Rect.Empty;
            Rect value = new Rect(candidate.X, candidate.Y, Math.Max(1, candidate.Width), candidate.Height);
            value.Intersect(owner);
            return value.IsEmpty || value.Width <= 0 || value.Height <= 0 ? Rect.Empty : value;
        }
    }
}
