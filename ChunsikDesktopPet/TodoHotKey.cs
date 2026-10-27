using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace ChunsikDesktop {
    public sealed class TodoHotKey : IDisposable {
        public static TodoHotKey Current { get; private set; }
        public static event Action Changed;
        public static string Label { get { return Format(Current == null ? 7U : Current.modifiers, Current == null ? 0x5AU : Current.key); } }
        static readonly string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChunsikDesktopPet", "hotkey.txt");
        const int FirstId = 0x4353, SecondId = 0x4354, WmHotKey = 0x0312;
        uint modifiers = 7, key = 0x5A;
        int activeId = FirstId;
        readonly HwndSource source;
        readonly Action toggle;
        bool disposed;
        public bool Registered { get; private set; }
        [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        public static bool Valid(uint mods, uint code) {
            return (mods & 3) != 0 && (mods & ~7U) == 0 && ((code >= 0x41 && code <= 0x5A) || (code >= 0x30 && code <= 0x39) || (code >= 0x70 && code <= 0x7A));
        }
        static string KeyLabel(uint code) { return code >= 0x70 ? "F" + (code - 0x6F) : ((char)code).ToString(); }
        static string Format(uint mods, uint code) { return ((mods & 2) != 0 ? "Ctrl + " : "") + ((mods & 1) != 0 ? "Alt + " : "") + ((mods & 4) != 0 ? "Shift + " : "") + KeyLabel(code); }
        public TodoHotKey(Action action) {
            toggle = action;
            try {
                string[] parts = File.ReadAllText(path).Trim().Split(','); uint m, k;
                if (parts.Length == 2 && UInt32.TryParse(parts[0], out m) && UInt32.TryParse(parts[1], out k) && Valid(m, k)) { modifiers = m; key = k; }
            } catch { }
            HwndSourceParameters parameters = new HwndSourceParameters("Chunsik post-it hotkey");
            parameters.ParentWindow = new IntPtr(-3); parameters.WindowStyle = 0;
            source = new HwndSource(parameters); source.AddHook(WndProc);
            Registered = RegisterHotKey(source.Handle, activeId, modifiers | 0x4000, key);
            Current = this;
        }
        public bool TryChange(uint mods, uint code, out string message) {
            if (disposed || !Valid(mods, code)) { message = "Ctrl 또는 Alt와 문자·숫자·F1~F11을 조합해 주세요."; return false; }
            if (Registered && mods == modifiers && code == key) { message = "현재 단축키가 정상 등록되어 있어요: " + Label; return true; }
            int nextId = activeId == FirstId ? SecondId : FirstId;
            // Reserve first: a conflict or failed save leaves the old registration intact.
            if (!RegisterHotKey(source.Handle, nextId, mods | 0x4000, code)) { message = "다른 앱에서 사용 중이거나 Windows에서 등록할 수 없어요. 기존 단축키를 유지합니다."; return false; }
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temp = path + ".tmp";
                try {
                    File.WriteAllText(temp, mods + "," + code);
                    if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
                } finally { if (File.Exists(temp)) File.Delete(temp); }
            } catch (Exception e) { UnregisterHotKey(source.Handle, nextId); message = "저장 실패 · 기존 단축키 유지: " + e.Message; return false; }
            if (Registered) UnregisterHotKey(source.Handle, activeId);
            activeId = nextId; modifiers = mods; key = code; Registered = true;
            if (Changed != null) Changed(); message = "등록·저장 완료: " + Label; return true;
        }
        public static FrameworkElement BuildSettings() {
            StackPanel panel = new StackPanel { Margin = new Thickness(0, 6, 0, 12) };
            panel.Children.Add(new TextBlock { Text = "포스트잇 전체 숨김 단축키", FontWeight = FontWeights.SemiBold });
            WrapPanel row = new WrapPanel(); panel.Children.Add(row);
            uint mods = Current == null ? 7U : Current.modifiers;
            CheckBox ctrl = new CheckBox { Content = "Ctrl", IsChecked = (mods & 2) != 0, Margin = new Thickness(6) };
            CheckBox alt = new CheckBox { Content = "Alt", IsChecked = (mods & 1) != 0, Margin = new Thickness(6) };
            CheckBox shift = new CheckBox { Content = "Shift", IsChecked = (mods & 4) != 0, Margin = new Thickness(6) };
            row.Children.Add(ctrl); row.Children.Add(alt); row.Children.Add(shift);
            ComboBox keys = new ComboBox { Width = 70, Margin = new Thickness(6) };
            for (uint code = 0x30; code <= 0x7A; code++) if (Valid(2, code)) {
                ComboBoxItem item = new ComboBoxItem { Content = KeyLabel(code), Tag = code }; keys.Items.Add(item);
                if (code == (Current == null ? 0x5AU : Current.key)) keys.SelectedItem = item;
            }
            row.Children.Add(keys);
            TextBlock status = new TextBlock { Text = "현재: " + Label + (Current != null && Current.Registered ? " · 사용 가능" : " · 등록되지 않음"), Foreground = Brushes.SlateGray, TextWrapping = TextWrapping.Wrap };
            Button apply = new Button { Content = "충돌 확인 · 적용", Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(6) };
            apply.Click += delegate {
                string message = "단축키 서비스가 아직 준비되지 않았어요.";
                uint value = (ctrl.IsChecked == true ? 2U : 0) | (alt.IsChecked == true ? 1U : 0) | (shift.IsChecked == true ? 4U : 0);
                bool ok = Current != null && keys.SelectedItem != null && Current.TryChange(value, (uint)((ComboBoxItem)keys.SelectedItem).Tag, out message);
                status.Text = message; status.Foreground = ok ? Brushes.DarkGreen : Brushes.Firebrick;
            };
            row.Children.Add(apply); panel.Children.Add(status); return panel;
        }
        IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled) {
            if (!disposed && Registered && message == WmHotKey && wParam.ToInt32() == activeId) {
                handled = true;
                source.Dispatcher.BeginInvoke(new Action(delegate { if (!disposed) toggle(); }));
            }
            return IntPtr.Zero;
        }
        public void Dispose() {
            if (disposed) return; disposed = true;
            if (Registered) UnregisterHotKey(source.Handle, activeId);
            Registered = false; source.RemoveHook(WndProc); source.Dispose();
            if (Current == this) Current = null;
        }
    }
}
