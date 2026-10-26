using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace ChunsikDesktop {
    // A message-only HWND keeps the hotkey alive even when every note is hidden.
    public sealed class TodoHotKey : IDisposable {
        public const string Label = "Ctrl + Alt + Shift + Z";
        const int Id = 0x4353;
        const int WmHotKey = 0x0312;
        const uint Modifiers = 0x0001 | 0x0002 | 0x0004 | 0x4000; // Alt, Ctrl, Shift, NOREPEAT
        readonly HwndSource source;
        readonly Action toggle;
        bool disposed;
        public bool Registered { get; private set; }
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        public TodoHotKey(Action action) {
            toggle = action;
            HwndSourceParameters parameters = new HwndSourceParameters("Chunsik post-it hotkey");
            parameters.ParentWindow = new IntPtr(-3); // HWND_MESSAGE
            parameters.WindowStyle = 0;
            source = new HwndSource(parameters);
            source.AddHook(WndProc);
            Registered = RegisterHotKey(source.Handle, Id, Modifiers, 0x5A);
        }
        IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled) {
            if (!disposed && Registered && message == WmHotKey && wParam.ToInt32() == Id) {
                handled = true;
                // Leave the native message before touching WPF windows or showing dialogs.
                source.Dispatcher.BeginInvoke(new Action(delegate { if (!disposed) toggle(); }));
            }
            return IntPtr.Zero;
        }
        public void Dispose() {
            if (disposed) return;
            disposed = true;
            if (Registered) UnregisterHotKey(source.Handle, Id);
            Registered = false;
            source.RemoveHook(WndProc);
            source.Dispose();
        }
    }
}
