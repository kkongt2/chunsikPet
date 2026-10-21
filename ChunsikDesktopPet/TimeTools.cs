using System;
using System.Diagnostics;
using System.Globalization;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ChunsikDesktop {
    // These companions have their own windows and never enter the reminder pet pool.
    public sealed class TimeToolWindow : Window {
        static TimeToolWindow countdownWindow, stopwatchWindow;
        readonly bool countdown;
        readonly Stopwatch elapsed = new Stopwatch();
        readonly Stopwatch animation = Stopwatch.StartNew();
        readonly DispatcherTimer refresh = new DispatcherTimer();
        readonly TextBlock readout = new TextBlock();
        readonly TextBlock status = new TextBlock();
        readonly Image pet = new Image();
        readonly RotateTransform tilt = new RotateTransform();
        readonly StackPanel durationPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        readonly TextBox hours = new TextBox(), minutes = new TextBox(), seconds = new TextBox();
        readonly ListBox laps = new ListBox();
        readonly Button start, reset, lap;
        TimeSpan duration = TimeSpan.FromMinutes(5), lastLap;
        bool started, completed;
        int lapCount;
        public static void OpenTimer() { Open(true); }
        public static void OpenStopwatch() { Open(false); }
        static void Open(bool countdown) {
            TimeToolWindow existing = countdown ? countdownWindow : stopwatchWindow;
            if (existing != null) { existing.Show(); existing.Activate(); return; }
            TimeToolWindow win = new TimeToolWindow(countdown);
            if (countdown) countdownWindow = win; else stopwatchWindow = win;
            win.Show();
        }
        public static void StopAll() {
            if (countdownWindow != null) countdownWindow.Close();
            if (stopwatchWindow != null) stopwatchWindow.Close();
        }
        static Button ActionButton(string text) {
            Button button = new Button { Content = text, Margin = new Thickness(3), Padding = new Thickness(12, 7, 12, 7), Cursor = Cursors.Hand, Foreground = Brushes.DarkSlateBlue };
            button.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'><Border x:Name='b' CornerRadius='12' Background='#EAF3FF' BorderBrush='#C9DBF1' BorderThickness='1' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Background' Value='#D7E9FF'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.4'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='b' Property='BorderBrush' Value='#0064A8'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
            return button;
        }
        TimeToolWindow(bool isCountdown) {
            countdown = isCountdown; Title = countdown ? "춘식이 타이머" : "춘식이 스톱워치";
            Width = 340; SizeToContent = SizeToContent.Height; WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize; AllowsTransparency = true; Background = Brushes.Transparent;
            Topmost = true; ShowInTaskbar = false;
            StackPanel root = new StackPanel { Margin = new Thickness(6) }; Content = root;
            StackPanel content = new StackPanel { Margin = new Thickness(16) };
            Border card = new Border { Background = new SolidColorBrush(Color.FromRgb(255, 253, 243)), BorderBrush = new SolidColorBrush(Color.FromRgb(112, 88, 63)), BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(20), Child = content }; root.Children.Add(card);
            Grid header = new Grid(); header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); content.Children.Add(header);
            TextBlock heading = new TextBlock { Text = countdown ? "타이머 담당 춘식이" : "스톱워치 담당 춘식이", FontSize = 17, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.SizeAll, ToolTip = "끌어서 위치 이동" };
            header.Children.Add(heading); heading.MouseLeftButtonDown += MoveWindow;
            Button close = ActionButton("×"); close.ToolTip = "측정을 종료하고 전담 춘식이 닫기"; close.Click += delegate { Close(); }; Grid.SetColumn(close, 1); header.Children.Add(close);
            readout.FontSize = 36; readout.FontWeight = FontWeights.Bold; readout.FontFamily = new FontFamily("Consolas"); readout.HorizontalAlignment = HorizontalAlignment.Center; readout.Margin = new Thickness(0, 12, 0, 6); content.Children.Add(readout);
            status.Text = "시작을 눌러주세요"; status.Foreground = Brushes.SlateGray; status.HorizontalAlignment = HorizontalAlignment.Center; status.Margin = new Thickness(0, 0, 0, 12); content.Children.Add(status);
            if (countdown) {
                AddDuration(hours, "시간", "0"); AddDuration(minutes, "분", "5"); AddDuration(seconds, "초", "0"); content.Children.Add(durationPanel);
                content.Children.Add(new TextBlock { Text = "최대 99시간 59분 59초", FontSize = 10, Foreground = Brushes.SlateGray, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 8) });
            }
            StackPanel controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center }; content.Children.Add(controls);
            start = ActionButton("시작"); reset = ActionButton("초기화"); lap = ActionButton("랩"); controls.Children.Add(start); controls.Children.Add(reset);
            start.Click += delegate { Toggle(); }; reset.Click += delegate { Reset(); };
            if (!countdown) {
                controls.Children.Add(lap); lap.Click += delegate { AddLap(); };
                laps.Height = 112; laps.Margin = new Thickness(0, 10, 0, 0); laps.BorderThickness = new Thickness(0); laps.Background = Brushes.Transparent; laps.FontFamily = new FontFamily("Consolas"); content.Children.Add(laps);
            }
            TextBlock tail = new TextBlock { Text = "▼", FontSize = 24, Foreground = new SolidColorBrush(Color.FromRgb(112, 88, 63)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, -7, 0, -6) }; root.Children.Add(tail);
            pet.Source = PetWindow.ToolPose(false); pet.Height = 108; pet.Stretch = Stretch.Uniform; pet.HorizontalAlignment = HorizontalAlignment.Center; pet.RenderTransformOrigin = new Point(0.5, 1); pet.RenderTransform = tilt; pet.Cursor = Cursors.SizeAll; pet.ToolTip = "전담 춘식이를 끌어 위치를 바꿀 수 있어요"; pet.MouseLeftButtonDown += MoveWindow; root.Children.Add(pet);
            TextBlock badge = new TextBlock { Text = countdown ? "TIMER" : "STOPWATCH", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Brushes.DarkSlateBlue, HorizontalAlignment = HorizontalAlignment.Center }; root.Children.Add(new Border { Background = Brushes.White, CornerRadius = new CornerRadius(7), Padding = new Thickness(7, 2, 7, 2), HorizontalAlignment = HorizontalAlignment.Center, Child = badge });
            Loaded += delegate {
                Rect area = SystemParameters.WorkArea;
                Left = Math.Max(area.Left, area.Right - Width - (countdown ? 20 : 380));
                Top = Math.Max(area.Top, area.Bottom - ActualHeight - 12);
            };
            refresh.Interval = TimeSpan.FromMilliseconds(50); refresh.Tick += delegate { UpdateDisplay(); }; refresh.Start();
            Closed += delegate { refresh.Stop(); elapsed.Stop(); animation.Stop(); if (countdown) countdownWindow = null; else stopwatchWindow = null; };
            UpdateDisplay();
        }
        void MoveWindow(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) { DragMove(); e.Handled = true; }
        }
        void AddDuration(TextBox box, string unit, string initial) {
            box.Text = initial; box.Width = 40; box.Padding = new Thickness(5); box.VerticalContentAlignment = VerticalAlignment.Center;
            System.Windows.Automation.AutomationProperties.SetName(box, "타이머 " + unit);
            durationPanel.Children.Add(box); durationPanel.Children.Add(new TextBlock { Text = unit, Margin = new Thickness(3, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
            box.TextChanged += delegate { if (!started) UpdateDisplay(); };
        }
        bool ReadDuration(out TimeSpan value) {
            value = TimeSpan.Zero; int h, m, s;
            if (!Int32.TryParse(hours.Text, out h) || !Int32.TryParse(minutes.Text, out m) || !Int32.TryParse(seconds.Text, out s) || h < 0 || h > 99 || m < 0 || m > 59 || s < 0 || s > 59) return false;
            value = TimeSpan.FromSeconds(h * 3600 + m * 60 + s); return value > TimeSpan.Zero;
        }
        void Toggle() {
            // Refresh completion first so a delayed UI tick cannot pause an expired timer.
            UpdateDisplay();
            if (elapsed.IsRunning) { elapsed.Stop(); status.Text = "잠깐 쉬는 중"; }
            else {
                if (completed) Reset();
                if (!started && countdown && !ReadDuration(out duration)) { status.Text = "1초 이상, 올바른 시간을 입력해 주세요"; return; }
                started = true; elapsed.Start(); durationPanel.IsEnabled = false;
                status.Text = countdown ? "시간을 지켜보고 있어요" : "함께 시간을 재고 있어요";
            }
            UpdateDisplay();
        }
        void Reset() {
            elapsed.Reset(); started = false; completed = false; lastLap = TimeSpan.Zero; lapCount = 0; laps.Items.Clear(); durationPanel.IsEnabled = true;
            status.Text = "시작을 눌러주세요"; UpdateDisplay();
        }
        void AddLap() {
            if (!elapsed.IsRunning) return;
            if (lapCount >= 200) { status.Text = "랩은 최대 200개까지 기록할 수 있어요"; return; }
            TimeSpan total = elapsed.Elapsed; lapCount++;
            laps.Items.Insert(0, String.Format(CultureInfo.InvariantCulture, "{0:000}  {1}  (+{2})", lapCount, FormatTime(total, true), FormatTime(total - lastLap, true))); lastLap = total;
        }
        static string FormatTime(TimeSpan value, bool fractions) {
            if (value < TimeSpan.Zero) value = TimeSpan.Zero;
            string result = ((long)value.TotalHours).ToString("00", CultureInfo.InvariantCulture) + ":" + value.Minutes.ToString("00") + ":" + value.Seconds.ToString("00");
            return fractions ? result + "." + (value.Milliseconds / 10).ToString("00") : result;
        }
        void UpdateDisplay() {
            if (start == null) return; // Duration fields fire events during construction.
            TimeSpan shown = elapsed.Elapsed;
            if (countdown) {
                if (!started) { TimeSpan input; shown = ReadDuration(out input) ? input : TimeSpan.Zero; }
                else {
                    shown = duration - elapsed.Elapsed;
                    if (!completed && shown <= TimeSpan.Zero) {
                        elapsed.Stop(); completed = true; status.Text = "시간이 다 됐어요! 수고했어요 ♥";
                        try { SystemSounds.Exclamation.Play(); } catch { }
                    }
                    shown = TimeSpan.FromSeconds(Math.Max(0, Math.Ceiling(shown.TotalSeconds)));
                }
            }
            readout.Text = FormatTime(shown, !countdown);
            start.Content = completed ? "다시 시작" : elapsed.IsRunning ? "일시정지" : started ? "재개" : "시작";
            lap.IsEnabled = elapsed.IsRunning && lapCount < 200;
            double phase = animation.Elapsed.TotalSeconds;
            bool wave = completed || (elapsed.IsRunning && (int)(phase / 1.2) % 4 == 0);
            pet.Source = PetWindow.ToolPose(wave); tilt.Angle = (elapsed.IsRunning || completed) ? Math.Sin(phase * (completed ? 6 : 2)) * (completed ? 7 : 2) : 0;
        }
    }
}
