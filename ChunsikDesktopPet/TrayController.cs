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
            System.Windows.Forms.ToolStripMenuItem timerItem = Item("타이머", TimeToolWindow.OpenTimer);
            System.Windows.Forms.ToolStripMenuItem stopwatchItem = Item("스톱워치", TimeToolWindow.OpenStopwatch);
            menu.Items.Add(timerItem); menu.Items.Add(stopwatchItem);
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            System.Windows.Forms.ToolStripMenuItem notes = Item("포스트잇 모두 숨기기", delegate { TodoService.Current.ToggleAllVisibility(); });
            menu.Items.Add(notes);
            menu.Items.Add(Item("포스트잇 관리…", delegate { TodoService.Current.OpenSettings(null); }));
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            System.Windows.Forms.ToolStripMenuItem gather = new System.Windows.Forms.ToolStripMenuItem("모두 모으기"); menu.Items.Add(gather);
            System.Windows.Forms.ToolStripMenuItem pause = Item("모두 쉬게 하기", delegate { PetWindow.PauseAll(!PetWindow.AllPaused); }); menu.Items.Add(pause);
            System.Windows.Forms.ToolStripMenuItem all = new System.Windows.Forms.ToolStripMenuItem("모든 춘식이 설정"); menu.Items.Add(all);
            System.Windows.Forms.ToolStripMenuItem sizes = new System.Windows.Forms.ToolStripMenuItem("크기");
            foreach (double value in new double[] { 50, 80, 120, 180, 270, 500 }) {
                double selected = value; System.Windows.Forms.ToolStripMenuItem choice = Item(PetWindow.SizeName(value), delegate { PetWindow.ResizeAll(selected); }); choice.Tag = value; sizes.DropDownItems.Add(choice);
            }
            all.DropDownItems.Add(sizes);
            System.Windows.Forms.ToolStripMenuItem speeds = new System.Windows.Forms.ToolStripMenuItem("걷는 속도");
            foreach (double value in new double[] { 22, 44, 80 }) {
                double selected = value; System.Windows.Forms.ToolStripMenuItem choice = Item(PetWindow.WalkingSpeedName(value), delegate { PetWindow.SpeedAll(selected); }); choice.Tag = value; speeds.DropDownItems.Add(choice);
            }
            all.DropDownItems.Add(speeds);
            System.Windows.Forms.ToolStripMenuItem opacity = new System.Windows.Forms.ToolStripMenuItem("선명도");
            foreach (int value in new int[] { 100, 75, 50, 25, 10 }) {
                int selected = value; System.Windows.Forms.ToolStripMenuItem choice = Item(value + "%", delegate { PetPreferences.Current.SetTransparency(100 - selected); }); choice.Tag = value; opacity.DropDownItems.Add(choice);
            }
            all.DropDownItems.Add(opacity);
            System.Windows.Forms.ToolStripMenuItem avoid = Item("마우스·입력 위치 피하기", delegate { PetPreferences.Current.SetCursorAvoidance(!PetPreferences.Current.Options.AvoidCursor); }); all.DropDownItems.Add(avoid);
            System.Windows.Forms.ToolStripMenuItem add = Item("한 마리 추가", delegate { PetPreferences.Current.ChangeCount(PetWindow.Count + 1); }); all.DropDownItems.Add(add);
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add(quiet);
            menu.Items.Add(Item("알림 이력…", ReminderHistory.Open));
            menu.Items.Add(Item("설정…", delegate { SettingsHub.Open("캐릭터"); }));
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add(Item("프로그램 종료", PetWindow.Quit));
            Action refreshMenu = delegate {
                timerItem.Text = TimeToolWindow.TimerMenuLabel;
                stopwatchItem.Text = TimeToolWindow.StopwatchMenuLabel;
                notes.ShortcutKeyDisplayString = TodoHotKey.Label + (todoHotKey != null && todoHotKey.Registered ? "" : " (사용 불가)");
                notes.Text = TodoService.Current.AllHidden ? "포스트잇 모두 보이기" : "포스트잇 모두 숨기기";
                quiet.Text = "방해 금지 · " + PresentationPolicy.Status; fullscreen.Checked = PresentationPolicy.FullscreenEnabled;
                avoid.Checked = PetPreferences.Current.Options.AvoidCursor;
                double? size = PetWindow.CommonSize, speed = PetWindow.CommonSpeed;
                sizes.Text = "크기 · " + (size.HasValue ? PetWindow.SizeName(size.Value) : "각각 다름");
                speeds.Text = "걷는 속도 · " + (speed.HasValue ? PetWindow.WalkingSpeedName(speed.Value) : "각각 다름");
                foreach (System.Windows.Forms.ToolStripMenuItem item in sizes.DropDownItems) item.Checked = size.HasValue && (double)item.Tag == size.Value;
                foreach (System.Windows.Forms.ToolStripMenuItem item in speeds.DropDownItems) item.Checked = speed.HasValue && (double)item.Tag == speed.Value;
                int clarity = 100 - PetPreferences.Current.Options.TransparencyPercent;
                opacity.Text = "선명도 · " + clarity + "%";
                foreach (System.Windows.Forms.ToolStripMenuItem item in opacity.DropDownItems) item.Checked = (int)item.Tag == clarity;
                pause.Text = PetWindow.AllPaused ? "모두 다시 걷기" : "모두 쉬게 하기";
                add.Enabled = PetWindow.Count < 20;
            };
            menu.Opening += delegate { refreshMenu(); };
            all.DropDownOpening += delegate { refreshMenu(); };
            gather.DropDownOpening += delegate {
                while (gather.DropDownItems.Count > 0) { System.Windows.Forms.ToolStripItem item = gather.DropDownItems[0]; gather.DropDownItems.RemoveAt(0); item.Dispose(); }
                foreach (System.Windows.Forms.Screen screen in System.Windows.Forms.Screen.AllScreens) {
                    string device = screen.DeviceName;
                    gather.DropDownItems.Add(Item(device.Replace(@"\\.\DISPLAY", "모니터 ") + (screen.Primary ? " (주 화면)" : ""), delegate { PetWindow.GatherToMonitor(device); }));
                }
            };
            System.Windows.Forms.Timer menuRefresh = new System.Windows.Forms.Timer { Interval = 1000 };
            menuRefresh.Tick += delegate { timerItem.Text = TimeToolWindow.TimerMenuLabel; stopwatchItem.Text = TimeToolWindow.StopwatchMenuLabel; };
            menu.Opened += delegate { menuRefresh.Start(); }; menu.Closed += delegate { menuRefresh.Stop(); }; menu.Disposed += delegate { menuRefresh.Dispose(); };
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
                Text = "v" + Diagnostics.Version, FontSize = 16, FontWeight = FontWeights.SemiBold,
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
            System.Windows.Controls.Button diagnostics = new System.Windows.Controls.Button { Content = "오류 진단 정보 보기 · 복사", Padding = new Thickness(12), Margin = new Thickness(0, 8, 0, 0) }; diagnostics.Click += delegate { Diagnostics.Open(); }; content.Children.Add(diagnostics);
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

