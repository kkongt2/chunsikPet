using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ChunsikDesktop {
    public sealed class TrayController : IDisposable {
        readonly System.Windows.Forms.NotifyIcon tray;
        readonly System.Windows.Forms.ContextMenuStrip menu;
        readonly System.Drawing.Icon icon;
        readonly TodoHotKey todoHotKey;
        bool disposed;
        Window about;
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool DestroyIcon(IntPtr icon);

        static System.Drawing.Icon MakeIcon(BitmapSource source) {
            // Frame 4 is a tightly trimmed standing sprite. Use its head for a legible tray icon.
            BitmapSource face = new CroppedBitmap(source, new Int32Rect(0, 0, source.PixelWidth, (int)(source.PixelHeight * 0.69)));
            PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(face));
            using (MemoryStream stream = new MemoryStream()) {
                encoder.Save(stream); stream.Position = 0;
                using (System.Drawing.Bitmap original = new System.Drawing.Bitmap(stream))
                using (System.Drawing.Bitmap scaled = new System.Drawing.Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb)) {
                    using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(scaled)) {
                        graphics.Clear(System.Drawing.Color.Transparent);
                        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        float ratio = Math.Min(32f / original.Width, 32f / original.Height);
                        float width = original.Width * ratio, height = original.Height * ratio;
                        graphics.DrawImage(original, (32-width)/2, (32-height)/2, width, height);
                    }
                    IntPtr handle = scaled.GetHicon();
                    try {
                        using (System.Drawing.Icon borrowed = System.Drawing.Icon.FromHandle(handle))
                            return (System.Drawing.Icon)borrowed.Clone();
                    } finally { DestroyIcon(handle); }
                }
            }
        }
        void Dispatch(Action action) {
            if (disposed) return;
            Application.Current.Dispatcher.BeginInvoke(new Action(delegate { if (!disposed) action(); }));
        }
        System.Windows.Forms.ToolStripMenuItem Item(string label, Action action) {
            System.Windows.Forms.ToolStripMenuItem result = new System.Windows.Forms.ToolStripMenuItem(label);
            result.Click += delegate { Dispatch(action); }; return result;
        }
        public TrayController() {
            SettingsHub.About = ShowAbout;
            icon = MakeIcon(PetWindow.TrayImage);
            menu = new System.Windows.Forms.ContextMenuStrip();
            System.Windows.Forms.ToolStripMenuItem quiet = new System.Windows.Forms.ToolStripMenuItem("방해 금지");
            quiet.DropDownItems.Add(Item("30분 동안", delegate { PresentationPolicy.Set(30); }));
            quiet.DropDownItems.Add(Item("1시간 동안", delegate { PresentationPolicy.Set(60); }));
            quiet.DropDownItems.Add(Item("직접 해제할 때까지", delegate { PresentationPolicy.Set(-1); }));
            quiet.DropDownItems.Add(Item("방해 금지 해제", delegate { PresentationPolicy.Set(0); }));
            System.Windows.Forms.ToolStripMenuItem fullscreen = Item("전체 화면에서 자동 숨김", PresentationPolicy.ToggleFullscreen); quiet.DropDownItems.Add(fullscreen);
            menu.Items.Add(quiet);
            menu.Items.Add(Item("설정 열기", delegate { SettingsHub.Open("캐릭터"); }));
            menu.Items.Add(Item("춘식이 목록 · 모니터 관리", delegate { SettingsHub.Open("캐릭터"); }));
            menu.Items.Add(Item("마우스가 있는 모니터로 모두 모으기", PetWindow.GatherAtCursor));
            menu.Items.Add(Item("알림 · 메시지 설정", delegate { ReminderService.Current.OpenSettings(); }));
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add(Item("포스트잇 · 할 일 등록/관리", delegate { TodoService.Current.OpenSettings(null); }));
            System.Windows.Forms.ToolStripMenuItem notes = Item("포스트잇 전체 숨기기", delegate { TodoService.Current.ToggleAllVisibility(); });
            notes.ShortcutKeyDisplayString = TodoHotKey.Label;
            menu.Items.Add(notes);
            menu.Items.Add(Item("타이머 · 전담 춘식이", TimeToolWindow.OpenTimer));
            menu.Items.Add(Item("스톱워치 · 전담 춘식이", TimeToolWindow.OpenStopwatch));
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            System.Windows.Forms.ToolStripMenuItem sizes = new System.Windows.Forms.ToolStripMenuItem("크기 (모두)");
            sizes.DropDownItems.Add(Item("작게 (50)", delegate { PetWindow.ResizeAll(50); }));
            sizes.DropDownItems.Add(Item("보통 (80)", delegate { PetWindow.ResizeAll(80); }));
            sizes.DropDownItems.Add(Item("크게 (120)", delegate { PetWindow.ResizeAll(120); }));
            sizes.DropDownItems.Add(Item("더 크게 (180)", delegate { PetWindow.ResizeAll(180); }));
            sizes.DropDownItems.Add(Item("엄청 크게 (270)", delegate { PetWindow.ResizeAll(270); }));
            sizes.DropDownItems.Add(Item("진격의 춘식이 (500)", delegate { PetWindow.ResizeAll(500); }));
            menu.Items.Add(sizes);
            System.Windows.Forms.ToolStripMenuItem speeds = new System.Windows.Forms.ToolStripMenuItem("걷는 속도 (모두)");
            speeds.DropDownItems.Add(Item("느리게", delegate { PetWindow.SpeedAll(22); }));
            speeds.DropDownItems.Add(Item("보통", delegate { PetWindow.SpeedAll(44); }));
            speeds.DropDownItems.Add(Item("빠르게", delegate { PetWindow.SpeedAll(80); }));
            menu.Items.Add(speeds);
            System.Windows.Forms.ToolStripMenuItem transparency = new System.Windows.Forms.ToolStripMenuItem("투명도 (돌아다니는 춘식이)");
            foreach (int amount in new int[] { 0, 25, 50, 75, 90 }) {
                int percent = amount;
                transparency.DropDownItems.Add(Item(percent == 0 ? "0% · 선명하게" : percent + "%", delegate { PetPreferences.Current.SetTransparency(percent); }));
            }
            menu.Items.Add(transparency);
            System.Windows.Forms.ToolStripMenuItem avoid = Item("마우스 + 키입력 피하기", delegate { PetPreferences.Current.SetCursorAvoidance(!PetPreferences.Current.Options.AvoidCursor); }); menu.Items.Add(avoid);
            System.Windows.Forms.ToolStripMenuItem pause = Item("모두 잠깐 쉬기", delegate { PetWindow.PauseAll(!PetWindow.AllPaused); });
            menu.Items.Add(pause);
            System.Windows.Forms.ToolStripMenuItem add = Item("춘식이 한 마리 추가", delegate { PetPreferences.Current.ChangeCount(PetWindow.Count + 1); });
            menu.Items.Add(add);
            menu.Items.Add(Item("알림 이력 · 놓친 알림", ReminderHistory.Open));
            menu.Items.Add(Item("테스트 알림", delegate { ReminderService.Current.Test(); }));
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add(Item("설정 백업 내보내기", SettingsBackup.Export));
            menu.Items.Add(Item("설정 백업 복원", SettingsBackup.Import));
            menu.Items.Add(Item("프로그램 정보", ShowAbout));
            menu.Items.Add(Item("춘식이 모두 종료", delegate { PetWindow.Quit(); }));
            menu.Opening += delegate {
                notes.Text = TodoService.Current.AllHidden ? "포스트잇 전체 보이기" : "포스트잇 전체 숨기기";
                quiet.Text = "방해 금지 · " + PresentationPolicy.Status; fullscreen.Checked = PresentationPolicy.FullscreenEnabled;
                avoid.Checked = PetPreferences.Current.Options.AvoidCursor;
                avoid.Text = "마우스 + 키입력 피하기 · " + (avoid.Checked ? "ON" : "OFF");
                foreach (System.Windows.Forms.ToolStripMenuItem choice in transparency.DropDownItems) choice.Checked = choice.Text.StartsWith(PetPreferences.Current.Options.TransparencyPercent + "%", StringComparison.Ordinal);
                pause.Text = PetWindow.AllPaused ? "모두 다시 걷기" : "모두 잠깐 쉬기";
                add.Enabled = PetWindow.Count < 20;
            };
            tray = new System.Windows.Forms.NotifyIcon { Icon = icon, Text = "춘식이 · 우클릭으로 설정", ContextMenuStrip = menu, Visible = true };
            tray.MouseDoubleClick += delegate(object sender, System.Windows.Forms.MouseEventArgs e) {
                if (e.Button == System.Windows.Forms.MouseButtons.Left) Dispatch(delegate { PetPreferences.Current.OpenSettings(); });
            };
            todoHotKey = new TodoHotKey(delegate { TodoService.Current.ToggleAllVisibility(); });
            if (!todoHotKey.Registered) {
                notes.ShortcutKeyDisplayString = TodoHotKey.Label + " (사용 불가)";
                tray.ShowBalloonTip(6000, "포스트잇 단축키 등록 실패", TodoHotKey.Label + " 단축키를 등록하지 못했어요. 다른 프로그램이 사용 중일 수 있어요. 트레이 메뉴의 전체 숨기기/보이기를 이용해 주세요.", System.Windows.Forms.ToolTipIcon.Warning);
            }
        }
        void ShowAbout() {
            if (about != null) { SettingsHub.Open("프로그램"); return; }
            Window win = new Window {
                Title = "프로그램 정보", Width = 460, SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(246, 249, 253))
            };
            about = win; win.Closed += delegate { about = null; };
            System.Windows.Controls.StackPanel content = new System.Windows.Controls.StackPanel { Margin = new Thickness(28) };
            win.Content = content;
            content.Children.Add(new System.Windows.Controls.Image { Source = PetWindow.TrayImage, Height = 78, Margin = new Thickness(0, 0, 0, 14) });
            content.Children.Add(new System.Windows.Controls.TextBlock {
                Text = "춘식이 데스크톱 펫", FontSize = 22, FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 100, 168))
            });
            System.Windows.Controls.StackPanel card = new System.Windows.Controls.StackPanel { Margin = new Thickness(18) };
            card.Children.Add(new System.Windows.Controls.TextBlock { Text = "만든이", FontSize = 12, Foreground = System.Windows.Media.Brushes.SlateGray, HorizontalAlignment = HorizontalAlignment.Center });
            card.Children.Add(new System.Windows.Controls.TextBlock {
                Text = "kkongt2.kang", ToolTip = "뜽하머슴", FontSize = 18, FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Center
            });
            card.Children.Add(new System.Windows.Controls.TextBlock {
                Text = "버전", FontSize = 12, Foreground = System.Windows.Media.Brushes.SlateGray,
                Margin = new Thickness(0, 20, 0, 0), HorizontalAlignment = HorizontalAlignment.Center
            });
            card.Children.Add(new System.Windows.Controls.TextBlock {
                Text = "v" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3), FontSize = 16, FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Center
            });
            content.Children.Add(new System.Windows.Controls.Border {
                Child = card, Background = System.Windows.Media.Brushes.White, CornerRadius = new CornerRadius(14),
                Margin = new Thickness(0, 20, 0, 18)
            });
            System.Windows.Controls.Button close = new System.Windows.Controls.Button {
                Content = "닫기", Padding = new Thickness(24, 7, 24, 7), HorizontalAlignment = HorizontalAlignment.Center
            };
            close.Click += delegate { SettingsHub.Close(); }; content.Children.Add(close);
            System.Windows.Controls.Button export = new System.Windows.Controls.Button { Content = "설정 백업 내보내기", Padding = new Thickness(12), Margin = new Thickness(0, 4, 0, 4) }; export.Click += delegate { SettingsBackup.Export(); }; content.Children.Add(export);
            System.Windows.Controls.Button import = new System.Windows.Controls.Button { Content = "설정 복원", Padding = new Thickness(12) }; import.Click += delegate { SettingsBackup.Import(); }; content.Children.Add(import);
            SettingsHub.Attach(win, "프로그램", null);
        }
        public void Dispose() {
            if (disposed) return; disposed = true;
            todoHotKey.Dispose();
            if (about != null) about.Close();
            tray.Visible = false; tray.Dispose(); menu.Dispose(); icon.Dispose();
        }
    }
}

