using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Screen = System.Windows.Forms.Screen;

namespace ChunsikDesktop {
    // Pure pixel geometry, shared by restore/move and covered without physical monitors.
    public static class PlacementGeometry {
        public static double Fraction(double position, double start, double travel) {
            return travel <= 0 ? 0 : Math.Max(0, Math.Min(1, (position - start) / travel));
        }
        public static Point Position(Rect work, Size size, double x, double y) {
            return new Point(work.Left + Math.Max(0, work.Width - size.Width) * Math.Max(0, Math.Min(1, x)),
                work.Top + Math.Max(0, work.Height - size.Height) * Math.Max(0, Math.Min(1, y)));
        }
    }

    public partial class PetWindow {
        bool movingMonitor;
        int monitorMoveGeneration;
        readonly Border managementHighlight = new Border {
            BorderBrush = Brushes.DeepSkyBlue, BorderThickness = new Thickness(3), CornerRadius = new CornerRadius(14),
            Background = new SolidColorBrush(Color.FromArgb(30, 0, 180, 255)), IsHitTestVisible = false, Opacity = 0
        };
        static bool ValidSpeed(double value) { return value == 22 || value == 44 || value == 80; }
        static void SaveManagement() { if (PetPreferences.Current != null) PetPreferences.Current.SaveLayout(); }
        void SetSpeed(double value) { if (!ValidSpeed(value)) return; speed = value; SaveManagement(); }
        static string SpeedName(double value) { return value == 22 ? "느리게" : value == 80 ? "빠르게" : "보통"; }
        Screen CurrentScreen() { return Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle); }
        static Rect MonitorWork(Screen screen) {
            System.Drawing.Rectangle r = screen.WorkingArea;
            return new Rect(r.Left, r.Top, r.Width, r.Height);
        }
        static Screen FindMonitor(string device) {
            foreach (Screen screen in Screen.AllScreens) if (screen.DeviceName == device) return screen;
            return null;
        }
        static string MonitorLabel(Screen screen) {
            string id = screen.DeviceName.Replace(@"\\.\DISPLAY", "");
            return "모니터 " + id + (screen.Primary ? " (주 화면)" : "") + " · " + screen.Bounds.Width + "×" + screen.Bounds.Height;
        }
        void HighlightForManagement() {
            if (closing) return;
            managementHighlight.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(1, 0, TimeSpan.FromSeconds(1.8)) { FillBehavior = FillBehavior.Stop });
        }
        void PositionOnMonitor(Screen target, double x, double y, bool save) {
            if (target == null || closing || !IsLoaded) return;
            string device = target.DeviceName;
            int generation = ++monitorMoveGeneration;
            movingMonitor = true;
            Rect work = MonitorWork(target);
            // First enter the destination HWND/DPI space, then measure the new pixel size.
            ScreenLayout.MovePixels(this, work.Left + 1, work.Top + 1);
            Action settle = delegate {
                if (closing || generation != monitorMoveGeneration) return;
                Screen currentTarget = FindMonitor(device) ?? Screen.PrimaryScreen;
                UpdateLayout();
                Rect bounds = ScreenLayout.BoundsPixels(this);
                if (!bounds.IsEmpty) {
                    Point at = PlacementGeometry.Position(MonitorWork(currentTarget), bounds.Size, x, y);
                    ScreenLayout.MovePixels(this, at.X, at.Y);
                }
            };
            settle();
            Dispatcher.BeginInvoke(new Action(delegate {
                if (closing || generation != monitorMoveGeneration) return;
                try { settle(); }
                finally { movingMonitor = false; }
                if (save) { SaveManagement(); HighlightForManagement(); }
                if (ReminderService.Current != null) ReminderService.Current.LayoutNotices();
            }), DispatcherPriority.Loaded);
        }
        void MoveToMonitor(Screen target, double x) {
            if (closing || !IsLoaded || target == null) return;
            // Explicit relocation cancels transient physics, but keeps user mode/size/speed.
            pressed = dragging = false; ReleaseMouseCapture();
            CancelCue(); wasEvading = false; fleeUntil = 0;
            EndPayday(); StopFall(false); StopNoticeAnimation(); EndReaction();
            LayoutPet(); SetFrame(paused ? 6 : 4);
            PositionOnMonitor(target, x, 1, true);
        }
        static void GatherOnMonitor(Screen target) {
            List<PetWindow> pets = new List<PetWindow>(Pets);
            for (int i = 0; i < pets.Count; i++) pets[i].MoveToMonitor(target, (i + 1.0) / (pets.Count + 1));
        }
        public static void GatherAtCursor() { GatherOnMonitor(Screen.FromPoint(System.Windows.Forms.Cursor.Position)); }
        void AddMonitorMenu(ContextMenu menu) {
            MenuItem monitors = new MenuItem { Header = "이 춘식이 모니터 이동" };
            menu.Items.Add(monitors);
            menu.Opened += delegate {
                monitors.Items.Clear();
                foreach (Screen screen in Screen.AllScreens) {
                    string device = screen.DeviceName;
                    MenuItem item = Item(MonitorLabel(screen), delegate { MoveToMonitor(FindMonitor(device) ?? Screen.PrimaryScreen, 0.5); });
                    item.IsChecked = CurrentScreen().DeviceName == device; monitors.Items.Add(item);
                }
            };
            menu.Items.Add(Item("이 춘식이의 모니터로 모두 모으기", delegate { GatherOnMonitor(CurrentScreen()); }));
            menu.Items.Add(Item("춘식이 목록 · 모니터 관리", delegate { SettingsHub.Open("캐릭터"); }));
        }

        static Button ManagementButton(string label, Action action) {
            Button b = new Button { Content = label, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 6, 8, 0) };
            b.Click += delegate { action(); }; return b;
        }
        public static FrameworkElement BuildManagementPanel(Window owner) {
            StackPanel panel = new StackPanel();
            Border card = new Border { Child = panel, Background = new SolidColorBrush(Color.FromRgb(239, 247, 255)), CornerRadius = new CornerRadius(12), Padding = new Thickness(14), Margin = new Thickness(0, 0, 0, 20) };
            panel.Children.Add(new TextBlock { Text = "내 춘식이 · 모니터 관리", FontSize = 18, FontWeight = FontWeights.Bold });
            panel.Children.Add(new TextBlock { Text = "목록을 선택하면 해당 춘식이에 파란 테두리가 잠깐 표시돼요. 변경사항은 자동 저장돼요.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.SlateGray, Margin = new Thickness(0, 6, 0, 8) });
            ListBox list = new ListBox { MinHeight = 100, MaxHeight = 240, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            System.Windows.Automation.AutomationProperties.SetName(list, "춘식이 목록"); panel.Children.Add(list);
            Func<PetWindow> selected = delegate { ListBoxItem item = list.SelectedItem as ListBoxItem; return item == null ? null : item.Tag as PetWindow; };
            WrapPanel actions = new WrapPanel(); panel.Children.Add(actions);
            actions.Children.Add(ManagementButton("위치 확인", delegate { PetWindow p = selected(); if (p != null) p.HighlightForManagement(); }));
            Button rest = ManagementButton("걷기 / 쉬기 전환", delegate { PetWindow p = selected(); if (p != null && !p.closing) { p.SetRestMode(!p.paused); SaveManagement(); } }); actions.Children.Add(rest);
            foreach (double value in new double[] { 22, 44, 80 }) {
                double speedValue = value;
                actions.Children.Add(ManagementButton(SpeedName(value), delegate { PetWindow p = selected(); if (p != null && !p.closing) p.SetSpeed(speedValue); }));
            }
            WrapPanel move = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) }; panel.Children.Add(move);
            ComboBox monitors = new ComboBox { MinWidth = 240, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 6, 8, 0) };
            System.Windows.Automation.AutomationProperties.SetName(monitors, "이동할 모니터"); move.Children.Add(monitors);
            Func<Screen> target = delegate { ComboBoxItem item = monitors.SelectedItem as ComboBoxItem; return item == null ? null : FindMonitor((string)item.Tag); };
            move.Children.Add(ManagementButton("선택 춘식이 이동", delegate { PetWindow p = selected(); if (p != null) p.MoveToMonitor(target(), 0.5); }));
            move.Children.Add(ManagementButton("선택 모니터로 모두 모으기", delegate { GatherOnMonitor(target()); }));
            panel.Children.Add(ManagementButton("설정 창이 있는 모니터로 모두 모으기", delegate {
                Window window = SettingsHub.OwnerFor(owner);
                GatherOnMonitor(Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(window).Handle));
            }));
            panel.Children.Add(new TextBlock { Text = "모니터를 분리한 경우 주 화면으로 복원해요. 방해 금지 중에는 위치 강조도 숨겨져요.", TextWrapping = TextWrapping.Wrap, FontSize = 11, Foreground = Brushes.SlateGray, Margin = new Thickness(0, 8, 0, 0) });
            bool refreshing = false;
            list.SelectionChanged += delegate { if (!refreshing) { PetWindow p = selected(); if (p != null) p.HighlightForManagement(); } };
            Action refresh = delegate {
                refreshing = true;
                try {
                    PetWindow previous = selected();
                    bool rebuild = list.Items.Count != Pets.Count;
                    if (!rebuild) for (int i = 0; i < Pets.Count; i++) if (((ListBoxItem)list.Items[i]).Tag != Pets[i]) { rebuild = true; break; }
                    if (rebuild) {
                        list.Items.Clear();
                        foreach (PetWindow pet in Pets) list.Items.Add(new ListBoxItem { Tag = pet, Padding = new Thickness(6) });
                        foreach (ListBoxItem row in list.Items) if (row.Tag == previous) list.SelectedItem = row;
                        if (list.SelectedIndex < 0 && list.Items.Count > 0) list.SelectedIndex = 0;
                    }
                    for (int i = 0; i < Pets.Count; i++) {
                        PetWindow pet = Pets[i];
                        ((ListBoxItem)list.Items[i]).Content = "춘식이 " + (i + 1) + "  ·  " + MonitorLabel(pet.CurrentScreen()) + "  ·  크기 " + pet.size + "  ·  " + (pet.paused ? "쉬기" : "걷기") + "  ·  속도 " + SpeedName(pet.speed);
                    }
                    actions.IsEnabled = selected() != null;
                    List<string> screenKeys = new List<string>();
                    foreach (Screen screen in Screen.AllScreens) screenKeys.Add(screen.DeviceName + "|" + MonitorLabel(screen));
                    string signature = String.Join(";", screenKeys.ToArray());
                    if ((string)monitors.Tag != signature) {
                        ComboBoxItem old = monitors.SelectedItem as ComboBoxItem;
                        string oldDevice = old == null ? null : (string)old.Tag;
                        monitors.Items.Clear();
                        foreach (Screen screen in Screen.AllScreens) {
                            ComboBoxItem item = new ComboBoxItem { Content = MonitorLabel(screen), Tag = screen.DeviceName };
                            monitors.Items.Add(item); if (screen.DeviceName == oldDevice) monitors.SelectedItem = item;
                        }
                        if (monitors.SelectedIndex < 0 && monitors.Items.Count > 0) monitors.SelectedIndex = 0;
                        monitors.Tag = signature;
                    }
                } finally { refreshing = false; }
            };
            DispatcherTimer refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            refreshTimer.Tick += delegate { if (card.IsVisible) refresh(); };
            card.Loaded += delegate { refresh(); refreshTimer.Start(); };
            card.Unloaded += delegate { refreshTimer.Stop(); };
            owner.Closed += delegate { refreshTimer.Stop(); };
            refresh(); return card;
        }
    }
}
