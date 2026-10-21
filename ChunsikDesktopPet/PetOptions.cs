using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Serialization;

namespace ChunsikDesktop {
    public class PetOptions {
        public int Size { get; set; }
        public int Speed { get; set; }
        public int TransparencyPercent { get; set; }
        public bool AvoidCursor { get; set; }
        public bool SleepEnabled { get; set; }
        public int IdleMinutes { get; set; }
        public int PetCount { get; set; }
        public int PaydayDay { get; set; }
        public bool PaydayEnabled { get; set; }
        public PetOptions() { Size = 80; Speed = 44; SleepEnabled = true; IdleMinutes = 5; PetCount = 1; PaydayDay = 20; PaydayEnabled = true; }
        public PetOptions Copy() { return (PetOptions)MemberwiseClone(); }
    }
    public sealed class PetPreferences {
        public static PetPreferences Current;
        public PetOptions Options { get; private set; }
        public event Action OptionsChanged;
        public bool IsIdle { get; private set; }
        public double InputIdleSeconds { get; private set; }
        static readonly string PathName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChunsikDesktopPet", "pets.xml");
        static readonly XmlSerializer Serializer = new XmlSerializer(typeof(PetOptions));
        readonly DispatcherTimer timer = new DispatcherTimer();
        Window editor;
        public bool SettingsOpen { get { return editor != null; } }
        [StructLayout(LayoutKind.Sequential)] struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetLastInputInfo(ref LASTINPUTINFO info);
        public PetPreferences() {
            Options = Load(); RefreshIdle();
            timer.Interval = TimeSpan.FromMilliseconds(500); timer.Tick += delegate { RefreshIdle(); }; timer.Start();
        }
        public static bool Valid(PetOptions value) {
            return value != null && value.Size >= 50 && value.Size <= 500 && value.Speed >= 10 && value.Speed <= 200 && value.IdleMinutes >= 1 && value.IdleMinutes <= 1440 && value.PetCount >= 1 && value.PetCount <= 20 && value.PaydayDay >= 1 && value.PaydayDay <= 31 && value.TransparencyPercent >= 0 && value.TransparencyPercent <= 90;
        }
        static PetOptions Read(string path) {
            using (FileStream stream = File.OpenRead(path)) {
                PetOptions result = (PetOptions)Serializer.Deserialize(stream);
                if (!Valid(result)) throw new InvalidDataException("Invalid pet settings.");
                return result;
            }
        }
        PetOptions Load() {
            if (!File.Exists(PathName)) return new PetOptions();
            try { return Read(PathName); }
            catch {
                try { return Read(PathName + ".bak"); }
                catch {
                    MessageBox.Show("캐릭터 설정을 읽을 수 없어 기본값(1마리, 5분 후 수면)으로 시작해요. 설정에서 다시 저장해 주세요.", "춘식이 설정");
                    return new PetOptions();
                }
            }
        }
        public void RefreshIdle() {
            LASTINPUTINFO info = new LASTINPUTINFO(); info.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
            if (!GetLastInputInfo(ref info)) { IsIdle = false; return; }
            uint elapsed = unchecked((uint)Environment.TickCount - info.dwTime);
            InputIdleSeconds = elapsed / 1000.0;
            IsIdle = Options.SleepEnabled && elapsed >= (uint)Options.IdleMinutes * 60000U;
        }
        bool Apply(PetOptions value, PetWindow remove = null) {
            if (!Valid(value)) return false;
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(PathName));
                string temp = PathName + ".tmp";
                try {
                    using (FileStream stream = File.Create(temp)) Serializer.Serialize(stream, value);
                    if (File.Exists(PathName)) File.Replace(temp, PathName, PathName + ".bak", true);
                    else File.Move(temp, PathName);
                } finally { if (File.Exists(temp)) File.Delete(temp); }
            } catch (Exception ex) {
                MessageBox.Show("설정을 저장하지 못했어요.\n" + ex.Message, "춘식이 설정"); return false;
            }
            Options = value.Copy(); RefreshIdle();
            if (remove != null && PetWindow.Count > value.PetCount) remove.Close();
            PetWindow.SetCount(value.PetCount); PetWindow.ApplyAppearance(); if (OptionsChanged != null) OptionsChanged(); return true;
        }
        public void SetSize(int size) { PetOptions value = Options.Copy(); value.Size = size; Apply(value); }
        public void SetSpeed(int speed) { PetOptions value = Options.Copy(); value.Speed = speed; Apply(value); }
        public void SetTransparency(int percent) { PetOptions value = Options.Copy(); value.TransparencyPercent = percent; Apply(value); }
        public void SetCursorAvoidance(bool enabled) { PetOptions value = Options.Copy(); value.AvoidCursor = enabled; Apply(value); }
        public void ChangeCount(int count, PetWindow remove = null) {
            if (count < 1) { PetWindow.Quit(); return; }
            if (count > 20) { MessageBox.Show("최대 20마리까지 표시할 수 있어요.", "춘식이 설정"); return; }
            PetOptions value = Options.Copy(); value.PetCount = count; Apply(value, remove);
        }
        public void OpenSettings() {
            if (editor != null) { SettingsHub.Open("캐릭터"); return; }
            Window win = new Window { Title = "춘식이 설정 — 표시 · 움직임 · 수면", Width = 550, Height = Math.Min(760, SystemParameters.WorkArea.Height - 40), ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterScreen };
            editor = win; win.Closed += delegate { editor = null; };
            StackPanel root = new StackPanel { Margin = new Thickness(22) }; win.Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            root.Children.Add(new TextBlock { Text = "크기·속도는 모든 일반 춘식이에 공통 적용하고 다음 실행에도 기억해요.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) });
            ComboBox size = new ComboBox { ItemsSource = new int[] { 50, 80, 120, 180, 270, 500 }, SelectedItem = Options.Size, Margin = new Thickness(0, 0, 0, 10), ToolTip = "모든 춘식이 크기" }; root.Children.Add(size);
            ComboBox speed = new ComboBox { ItemsSource = new int[] { 22, 44, 80 }, SelectedItem = Options.Speed, Margin = new Thickness(0, 0, 0, 14), ToolTip = "걷는 속도 · 22 느리게 / 44 보통 / 80 빠르게" }; root.Children.Add(speed);
            CheckBox enabled = new CheckBox { Content = "키보드·마우스 입력이 없으면 자동으로 잠들기", IsChecked = Options.SleepEnabled, Margin = new Thickness(0, 0, 0, 14) }; root.Children.Add(enabled);
            StackPanel idleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
            idleRow.Children.Add(new TextBlock { Text = "입력 없이 ", VerticalAlignment = VerticalAlignment.Center });
            TextBox minutes = new TextBox { Text = Options.IdleMinutes.ToString(), Width = 65, Padding = new Thickness(5) }; idleRow.Children.Add(minutes);
            idleRow.Children.Add(new TextBlock { Text = " 분 지나면 수면 (1~1440분)", VerticalAlignment = VerticalAlignment.Center }); root.Children.Add(idleRow);
            TextBlock countLabel = new TextBlock { Margin = new Thickness(0, 0, 0, 10) }; root.Children.Add(countLabel);
            Slider count = new Slider { Minimum = 1, Maximum = 20, TickFrequency = 1, IsSnapToTickEnabled = true, TickPlacement = System.Windows.Controls.Primitives.TickPlacement.BottomRight, Value = Options.PetCount };
            countLabel.Text = "캐릭터 수: " + Options.PetCount + "마리";
            count.ValueChanged += delegate { countLabel.Text = "캐릭터 수: " + (int)Math.Round(count.Value) + "마리"; }; root.Children.Add(count);
            root.Children.Add(new TextBlock { Text = "다른 프로그램에서 입력해도 깨어나요.\n변경하면 자동 저장·적용되고 다음 실행에도 유지됩니다.\n잠든 중에도 시간 알림 말풍선은 표시됩니다.", Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 16, 0, 16) });
            Border appearanceCard = new Border { Background = new SolidColorBrush(Color.FromRgb(242, 247, 253)), CornerRadius = new CornerRadius(12), Padding = new Thickness(14), Margin = new Thickness(0, 0, 0, 16) };
            StackPanel appearance = new StackPanel(); appearanceCard.Child = appearance; root.Children.Add(appearanceCard);
            TextBlock transparencyLabel = new TextBlock { Text = "돌아다니는 춘식이 투명도: " + Options.TransparencyPercent + "%", FontWeight = FontWeights.SemiBold }; appearance.Children.Add(transparencyLabel);
            Slider transparency = new Slider { Minimum = 0, Maximum = 90, TickFrequency = 5, IsSnapToTickEnabled = true, Value = Options.TransparencyPercent, Margin = new Thickness(0, 10, 0, 5) }; appearance.Children.Add(transparency);
            transparency.ValueChanged += delegate { transparencyLabel.Text = "돌아다니는 춘식이 투명도: " + (int)Math.Round(transparency.Value) + "%"; };
            appearance.Children.Add(new TextBlock { Text = "0%: 선명하게 · 90%: 아주 연하게", Foreground = Brushes.SlateGray, FontSize = 11, Margin = new Thickness(0, 0, 0, 14) });
            System.Windows.Controls.Primitives.ToggleButton avoid = new System.Windows.Controls.Primitives.ToggleButton { Content = "마우스가 가까워지면 빠르게 피하기", IsChecked = Options.AvoidCursor, Padding = new Thickness(12, 8, 12, 8), HorizontalContentAlignment = HorizontalAlignment.Left };
            avoid.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ToggleButton'><Border x:Name='box' CornerRadius='10' Background='#E4EAF1' Padding='{TemplateBinding Padding}'><DockPanel><TextBlock x:Name='state' Text='OFF' FontWeight='Bold' Margin='0,0,12,0'/><ContentPresenter/></DockPanel></Border><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='box' Property='Background' Value='#CDEBDE'/><Setter TargetName='state' Property='Text' Value='ON'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='box' Property='BorderBrush' Value='#0064A8'/><Setter TargetName='box' Property='BorderThickness' Value='1'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
            appearance.Children.Add(avoid);
            appearance.Children.Add(new TextBlock { Text = "기본 OFF · 일반 춘식이에만 적용해요.\n드래그·클릭 반응·알림 표시 중에는 피하지 않아요.", Foreground = Brushes.SlateGray, FontSize = 11, Margin = new Thickness(0, 8, 0, 0) });
            Border paydayCard = new Border { Background = new SolidColorBrush(Color.FromRgb(239, 247, 255)), CornerRadius = new CornerRadius(12), Padding = new Thickness(14), Margin = new Thickness(0, 0, 0, 16) };
            StackPanel paydayPanel = new StackPanel(); paydayCard.Child = paydayPanel; root.Children.Add(paydayCard);
            CheckBox paydayEnabled = new CheckBox { Content = "월급날 실행 시 지폐 뿌리기", IsChecked = Options.PaydayEnabled, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) }; paydayPanel.Children.Add(paydayEnabled);
            StackPanel paydayRow = new StackPanel { Orientation = Orientation.Horizontal };
            paydayRow.Children.Add(new TextBlock { Text = "매달 ", VerticalAlignment = VerticalAlignment.Center });
            ComboBox payday = new ComboBox { Width = 65 };
            for (int day = 1; day <= 31; day++) payday.Items.Add(day);
            payday.SelectedItem = Options.PaydayDay; paydayRow.Children.Add(payday);
            paydayRow.Children.Add(new TextBlock { Text = " 일", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0, 12, 0) });
            Button preview = new Button { Content = "지폐 모션 미리보기", Padding = new Thickness(10, 4, 10, 4) };
            preview.Click += delegate { PetWindow.PreviewPayday(); }; paydayRow.Children.Add(preview); paydayPanel.Children.Add(paydayRow);
            TextBlock paydayDates = new TextBlock { Text = PaydayCalendar.Describe(DateTime.Today, Options.PaydayDay), Foreground = new SolidColorBrush(Color.FromRgb(0, 100, 168)), Margin = new Thickness(0, 10, 0, 8) }; paydayPanel.Children.Add(paydayDates);
            payday.SelectionChanged += delegate { if (payday.SelectedItem != null) paydayDates.Text = PaydayCalendar.Describe(DateTime.Today, (int)payday.SelectedItem); };
            paydayPanel.Children.Add(new TextBlock { Text = "토·일·공휴일이면 직전 평일로 앞당겨요. 없는 날짜는 말일 기준이에요.\n공휴일·대체공휴일 달력: 2040년까지 내장 / 임시공휴일 제외\n월급날에는 실행할 때마다 12초간 재생돼요. 인터넷은 사용하지 않아요.", FontSize = 11, Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap });
            TextBlock saveStatus = new TextBlock { Text = "변경사항은 자동으로 저장됩니다.", Foreground = Brushes.SlateGray, TextWrapping = TextWrapping.Wrap }; root.Children.Add(saveStatus);
            bool refreshing = false;
            DispatcherTimer debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            Func<bool> apply = delegate {
                debounce.Stop(); int delay;
                if (!Int32.TryParse(minutes.Text.Trim(), out delay) || delay < 1 || delay > 1440) {
                    saveStatus.Text = "수면 대기 시간은 1~1440분의 정수로 입력해 주세요. 아직 저장되지 않았어요."; saveStatus.Foreground = Brushes.Firebrick; return false;
                }
                PetOptions value = new PetOptions { Size = size.SelectedItem == null ? Options.Size : (int)size.SelectedItem, Speed = speed.SelectedItem == null ? Options.Speed : (int)speed.SelectedItem, TransparencyPercent = (int)Math.Round(transparency.Value), AvoidCursor = avoid.IsChecked == true, SleepEnabled = enabled.IsChecked == true, IdleMinutes = delay, PetCount = (int)Math.Round(count.Value), PaydayDay = (int)payday.SelectedItem, PaydayEnabled = paydayEnabled.IsChecked == true };
                bool ok = Apply(value); saveStatus.Text = ok ? "모든 변경사항 저장됨" : "저장하지 못했어요. 다시 시도해 주세요."; saveStatus.Foreground = ok ? Brushes.SlateGray : Brushes.Firebrick; return ok;
            };
            Action sync = delegate {
                refreshing = true;
                size.SelectedItem = Options.Size; speed.SelectedItem = Options.Speed; count.Value = Options.PetCount;
                transparency.Value = Options.TransparencyPercent; avoid.IsChecked = Options.AvoidCursor;
                enabled.IsChecked = Options.SleepEnabled; minutes.Text = Options.IdleMinutes.ToString();
                payday.SelectedItem = Options.PaydayDay; paydayEnabled.IsChecked = Options.PaydayEnabled;
                refreshing = false;
            };
            Action changed = delegate { if (refreshing) return; saveStatus.Text = "변경사항 저장 중…"; debounce.Stop(); debounce.Start(); };
            debounce.Tick += delegate { apply(); };
            size.SelectionChanged += delegate { changed(); }; speed.SelectionChanged += delegate { changed(); };
            count.ValueChanged += delegate { changed(); };
            transparency.ValueChanged += delegate {
                if (!refreshing) PetWindow.PreviewTransparency((int)Math.Round(transparency.Value)); changed();
            };
            avoid.Checked += delegate { changed(); }; avoid.Unchecked += delegate { changed(); };
            enabled.Checked += delegate { changed(); }; enabled.Unchecked += delegate { changed(); };
            minutes.TextChanged += delegate { changed(); }; payday.SelectionChanged += delegate { changed(); };
            paydayEnabled.Checked += delegate { changed(); }; paydayEnabled.Unchecked += delegate { changed(); };
            OptionsChanged += sync;
            win.Closed += delegate { debounce.Stop(); OptionsChanged -= sync; };
            SettingsHub.Attach(win, "캐릭터", apply);
        }
        public void Stop() { timer.Stop(); if (editor != null) editor.Close(); }
    }
}
