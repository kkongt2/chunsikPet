using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace ChunsikDesktop {
    public static class Diagnostics {
        static readonly string ErrorPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChunsikDesktopPet", "last-error.txt");
        static string lastError;
        static bool installed;
        const int Limit = 16000;
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
        [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(POINT point, uint flags);
        [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
        public static string Version { get { return typeof(Diagnostics).Assembly.GetName().Version.ToString(3); } }
        public static string Sanitize(string value) {
            string result = value ?? "";
            foreach (string folder in new string[] { Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar) })
                if (!String.IsNullOrEmpty(folder)) result = result.Replace(folder, "[local path]");
            return result.Length <= Limit ? result : result.Substring(0, Limit) + "\n[truncated]";
        }
        public static void Record(string context, Exception error) {
            try {
                lastError = Sanitize(DateTimeOffset.Now.ToString("o") + " · v" + Version + " · " + context + "\n" + error);
                Directory.CreateDirectory(Path.GetDirectoryName(ErrorPath));
                File.WriteAllText(ErrorPath, lastError, Encoding.UTF8);
            } catch { } // Diagnostics must never replace the original failure.
        }
        public static void Install() {
            if (installed) return; installed = true;
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e) { Record("Unhandled exception", e.ExceptionObject as Exception); };
        }
        public static void Attach(Application app) {
            app.DispatcherUnhandledException += delegate(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e) { Record("UI exception", e.Exception); };
            // Do not mark unexpected exceptions handled or continue in an unknown state.
        }
        static string LastError() {
            if (lastError != null) return lastError;
            try { if (File.Exists(ErrorPath)) { using (StreamReader reader = File.OpenText(ErrorPath)) { char[] chars = new char[Limit]; return Sanitize(new string(chars, 0, reader.ReadBlock(chars, 0, chars.Length))); } } } catch { }
            return "기록된 오류 없음";
        }
        public static string BuildReport() {
            StringBuilder report = new StringBuilder();
            report.AppendLine("Chunsik Desktop Pet · v" + Version);
            report.AppendLine("작성 시각: " + DateTimeOffset.Now.ToString("o"));
            report.AppendLine("OS: " + Environment.OSVersion + " · " + (Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit"));
            report.AppendLine("CLR: " + Environment.Version + " · Process: " + (Environment.Is64BitProcess ? "64-bit" : "32-bit"));
            report.AppendLine("방해 금지: " + PresentationPolicy.Status + " · 춘식이 수: " + PetWindow.Count);
            report.AppendLine("포스트잇 단축키: " + TodoHotKey.Label + " · " + (TodoHotKey.Current != null && TodoHotKey.Current.Registered ? "등록됨" : "등록 안 됨"));
            report.AppendLine("최근 입력 위치 감지: " + (InputAvoidance.DetectionStatus ?? "조회 전"));
            report.AppendLine("모니터 (물리 픽셀 좌표):");
            try {
                foreach (System.Windows.Forms.Screen screen in System.Windows.Forms.Screen.AllScreens) {
                    var b = screen.Bounds; var w = screen.WorkingArea;
                    string dpi = "조회 불가";
                    try {
                        uint x, y; IntPtr handle = MonitorFromPoint(new POINT { X = b.Left + b.Width / 2, Y = b.Top + b.Height / 2 }, 2);
                        if (GetDpiForMonitor(handle, 0, out x, out y) == 0) dpi = x + "×" + y + " (" + Math.Round(x * 100.0 / 96) + "%)";
                    } catch (DllNotFoundException) { } catch (EntryPointNotFoundException) { }
                    report.AppendLine(screen.DeviceName + (screen.Primary ? " [primary]" : "") + " Bounds=" + b + " Work=" + w + " DPI=" + dpi);
                }
            } catch (Exception error) { report.AppendLine("모니터 조회 실패: " + error.GetType().Name); }
            report.AppendLine("최근 오류 (이전 실행 포함):"); report.AppendLine(LastError());
            return report.ToString();
        }
        public static void Open() {
            Window win = new Window { Title = "오류 진단 정보", Width = 760, Height = 540, MinWidth = 420, MinHeight = 280, WindowStartupLocation = WindowStartupLocation.CenterScreen };
            DockPanel root = new DockPanel { Margin = new Thickness(16) }; win.Content = root;
            TextBlock hint = new TextBlock { Text = "아래 내용을 확인한 뒤 복사해 주세요. 메모·알림 본문은 수집하지 않습니다. 오류 메시지에 포함된 정보는 공유 전에 확인해 주세요.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
            DockPanel.SetDock(hint, Dock.Top); root.Children.Add(hint);
            StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) }; DockPanel.SetDock(actions, Dock.Bottom); root.Children.Add(actions);
            TextBox text = new TextBox { Text = BuildReport(), IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
            TextBlock status = new TextBlock { Margin = new Thickness(12, 5, 0, 0) };
            Button copy = new Button { Content = "진단 정보 복사", Padding = new Thickness(12, 6, 12, 6) };
            copy.Click += delegate { try { Clipboard.SetText(text.Text); status.Text = "복사했어요"; } catch (Exception e) { status.Text = "복사 실패 · 내용을 선택해 Ctrl+C로 복사해 주세요"; Record("Clipboard", e); } };
            actions.Children.Add(copy); actions.Children.Add(status); root.Children.Add(text); win.ShowDialog();
        }
        public static void ShowError(string title, Exception error) {
            Record(title, error);
            Window win = new Window { Title = title, Width = 500, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterScreen };
            StackPanel panel = new StackPanel { Margin = new Thickness(20) }; win.Content = panel;
            panel.Children.Add(new TextBlock { Text = title + "\n" + Sanitize(error.Message), TextWrapping = TextWrapping.Wrap, MaxHeight = 200 });
            Button details = new Button { Content = "진단 정보 보기 · 복사", Margin = new Thickness(0, 14, 0, 8), Padding = new Thickness(10) }; details.Click += delegate { Open(); }; panel.Children.Add(details);
            Button close = new Button { Content = "닫기", Padding = new Thickness(10) }; close.Click += delegate { win.Close(); }; panel.Children.Add(close); win.ShowDialog();
        }
    }
}
