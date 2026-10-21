using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Serialization;

namespace ChunsikDesktop {
    public static class BubblePalette {
        public static readonly string[] Colors = { "#FFFDF2", "#FFE894", "#FFD9E8", "#D5F3DF", "#D9EAFE", "#E5DEFF" };
        static readonly string[] Names = { "크림", "노랑", "분홍", "민트", "하늘", "라벤더" };
        public static Brush BrushFor(string value) {
            foreach (string color in Colors) if (color == value) return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(Colors[0]));
        }
        public static FrameworkElement Picker(string selected, Action<string> change, int columns) {
            System.Windows.Controls.Primitives.UniformGrid grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = columns, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            Action paint = null;
            paint = delegate {
                grid.Children.Clear();
                for (int i = 0; i < Colors.Length; i++) {
                    string color = Colors[i];
                    Button button = new Button { Width = 22, Height = 22, Margin = new Thickness(2), Content = (selected ?? Colors[0]) == color ? "✓" : "", FontSize = 12, Foreground = Brushes.DarkSlateGray, Background = BrushFor(color), ToolTip = Names[i], Cursor = System.Windows.Input.Cursors.Hand };
                    button.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border CornerRadius='7' Background='{TemplateBinding Background}' BorderBrush='#8292A3' BorderThickness='1'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border></ControlTemplate>");
                    System.Windows.Automation.AutomationProperties.SetName(button, Names[i] + " 말풍선 색상");
                    button.Click += delegate { selected = color; paint(); change(color); }; grid.Children.Add(button);
                }
            }; paint(); return grid;
        }
    }
    public class TodoItem {
        public string Id { get; set; }
        public string Text { get; set; }
        public bool Done { get; set; }
        public DateTime? CompletedAt { get; set; }
        public bool Dedicated { get; set; }
        public bool Pinned { get; set; }
        public bool Peek { get; set; }
        public bool HasPosition { get; set; }
        public double NoteLeft { get; set; }
        public double NoteTop { get; set; }
        [XmlIgnore] public bool Standalone { get { return Pinned || Peek; } }
        public int PetIndex { get; set; }
        public bool Always { get; set; }
        public int EveryMinutes { get; set; }
        public int DisplaySeconds { get; set; }
        public string Color { get; set; }
        public TodoItem() { Id = Guid.NewGuid().ToString("N"); Text = ""; Pinned = true; Peek = true; Always = true; EveryMinutes = 60; DisplaySeconds = 20; Color = "#FFE894"; }
    }
    public class TodoSettings {
        public List<TodoItem> Items { get; set; }
        public TodoSettings() { Items = new List<TodoItem>(); }
    }
    public sealed class TodoService {
        public static TodoService Current;
        static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChunsikDesktopPet", "todos.xml");
        static readonly XmlSerializer Serializer = new XmlSerializer(typeof(TodoSettings));
        readonly TodoSettings settings;
        readonly Dictionary<string, Runtime> runtimes = new Dictionary<string, Runtime>();
        readonly DispatcherTimer timer = new DispatcherTimer();
        readonly Stopwatch clock = Stopwatch.StartNew();
        Window editor;
        public bool SettingsOpen { get { return editor != null; } }
        public TodoSettings Snapshot() { return settings; }
        PetWindow preferredPet;
        Action renderEditor;
        Func<bool> saveEditor;
        bool stopping;
        public bool Stopping { get { return stopping; } }
        sealed class Runtime {
            public TodoItem Item;
            public PetWindow Pet;
            public PetWindow Owned;
            public TodoNote Note;
            public bool Always;
            public int Minutes, Seconds;
            public double Next, Until;
        }
        public TodoService() {
            settings = Load();
            timer.Interval = TimeSpan.FromMilliseconds(150); timer.Tick += delegate { Tick(); }; timer.Start();
        }
        static TodoSettings Read(string path) {
            using (FileStream stream = File.OpenRead(path)) {
                TodoSettings data = (TodoSettings)Serializer.Deserialize(stream);
                if (data == null || data.Items == null) throw new InvalidDataException();
                HashSet<string> ids = new HashSet<string>();
                foreach (TodoItem item in data.Items) {
                    if (item == null) throw new InvalidDataException();
                    if (Double.IsNaN(item.NoteLeft) || Double.IsInfinity(item.NoteLeft) || Double.IsNaN(item.NoteTop) || Double.IsInfinity(item.NoteTop)) item.HasPosition = false;
                    if (String.IsNullOrEmpty(item.Id) || !ids.Add(item.Id)) { item.Id = Guid.NewGuid().ToString("N"); ids.Add(item.Id); }
                    if (item.EveryMinutes < 1 || item.EveryMinutes > 1440) item.EveryMinutes = 60;
                    if (item.DisplaySeconds < 1 || item.DisplaySeconds > 3600) item.DisplaySeconds = 20;
                    if (String.IsNullOrWhiteSpace(item.Text)) item.Done = true;
                }
                return data;
            }
        }
        static TodoSettings Load() {
            if (!File.Exists(FilePath)) return new TodoSettings();
            try { return Read(FilePath); } catch {
                try { TodoSettings backup = Read(FilePath + ".bak"); MessageBox.Show("할 일 설정을 백업에서 복원했어요.", "춘식이 할 일"); return backup; }
                catch { MessageBox.Show("할 일 파일을 읽지 못했어요. 기존 파일은 보존됩니다.", "춘식이 할 일"); try { File.Copy(FilePath, FilePath + ".unreadable-" + DateTime.Now.ToString("yyyyMMddHHmmss")); } catch { } return new TodoSettings(); }
            }
        }
        bool Save() {
            foreach (TodoItem item in settings.Items) if (String.IsNullOrWhiteSpace(item.Text) || item.EveryMinutes < 1 || item.EveryMinutes > 1440 || item.DisplaySeconds < 1 || item.DisplaySeconds > 3600) return false;
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                string temp = FilePath + ".tmp";
                using (FileStream stream = File.Create(temp)) Serializer.Serialize(stream, settings);
                if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak", true); else File.Move(temp, FilePath);
                Sync(); return true;
            } catch (Exception e) { MessageBox.Show("할 일 저장에 실패했어요. " + e.Message, "춘식이 할 일"); return false; }
        }
        void DisposeRuntime(Runtime run) { run.Note.DisposeNote(); if (run.Owned != null) run.Owned.Close(); }
        void Sync() {
            HashSet<string> keep = new HashSet<string>();
            foreach (TodoItem item in settings.Items) {
                if (item.Done || String.IsNullOrWhiteSpace(item.Text) || item.EveryMinutes < 1 || item.EveryMinutes > 1440 || item.DisplaySeconds < 1 || item.DisplaySeconds > 3600) continue;
                keep.Add(item.Id); Runtime run;
                if (!runtimes.TryGetValue(item.Id, out run)) {
                    TodoItem captured = item;
                    run = new Runtime { Item = item, Always = item.Always, Minutes = item.EveryMinutes, Seconds = item.DisplaySeconds, Until = clock.Elapsed.TotalSeconds + item.DisplaySeconds };
                    run.Note = new TodoNote(delegate { Complete(captured); }, delegate { OpenSettings(null); }, delegate(double left, double top) {
                        captured.NoteLeft = left; captured.NoteTop = top; captured.HasPosition = true; Save(); if (renderEditor != null) renderEditor();
                    }); runtimes.Add(item.Id, run);
                }
                run.Item = item;
                if (run.Always != item.Always || run.Minutes != item.EveryMinutes || run.Seconds != item.DisplaySeconds) { run.Always = item.Always; run.Minutes = item.EveryMinutes; run.Seconds = item.DisplaySeconds; run.Until = clock.Elapsed.TotalSeconds + item.DisplaySeconds; run.Next = 0; }
                if (!item.Standalone && item.Dedicated && (run.Owned == null || run.Owned.HasClosed)) { run.Owned = new PetWindow(true); run.Owned.Show(); }
                if ((item.Standalone || !item.Dedicated) && run.Owned != null) { run.Owned.Close(); run.Owned = null; }
            }
            foreach (string id in new List<string>(runtimes.Keys)) if (!keep.Contains(id)) { DisposeRuntime(runtimes[id]); runtimes.Remove(id); }
        }
        void Complete(TodoItem item) {
            Runtime run;
            if (runtimes.TryGetValue(item.Id, out run) && run.Note.IsVisible) {
                run.Note.AnimateComplete(delegate { FinishComplete(item); }); return;
            }
            FinishComplete(item);
        }
        void FinishComplete(TodoItem item) {
            DateTime? previousDate = item.CompletedAt; item.Done = true; item.CompletedAt = DateTime.Now;
            DateTime? completion = item.CompletedAt;
            if (!Save()) { item.Done = false; item.CompletedAt = previousDate; return; }
            UndoToast.ShowCompleted(item.Text, delegate {
                if (!settings.Items.Contains(item) || !item.Done || item.CompletedAt != completion) return;
                item.Done = false; item.CompletedAt = null;
                if (!Save()) { item.Done = true; item.CompletedAt = completion; }
                if (renderEditor != null) renderEditor();
            });
            if (renderEditor != null) renderEditor();
        }
        public bool IsHoldingFor(PetWindow pet) {
            foreach (Runtime run in runtimes.Values) if (run.Pet == pet && run.Note.IsVisible) return true;
            return false;
        }
        void Tick() {
            if (stopping) return;
            Sync();
            List<PetWindow> pets = PetWindow.TodoPets();
            List<Rect> occupied = new List<Rect>();
            if (ReminderService.Current != null) occupied.AddRange(ReminderService.Current.VisibleBounds());
            double now = clock.Elapsed.TotalSeconds;
            foreach (Runtime run in runtimes.Values) {
                if (run.Note.IsCompleting) continue;
                TodoItem item = run.Item;
                run.Pet = item.Standalone ? null : item.Dedicated ? run.Owned : pets.Count == 0 ? null : pets[Math.Max(0, Math.Min(pets.Count - 1, item.PetIndex))];
                bool blocked = !item.Standalone && (run.Pet == null || (ReminderService.Current != null && ReminderService.Current.IsSpeakingFor(run.Pet)));
                if (!item.Always && run.Until <= now && run.Next == 0) run.Next = Math.Max(run.Until + 1, run.Until - run.Seconds + item.EveryMinutes * 60);
                if (!item.Always && run.Next > 0 && now >= run.Next && !blocked) { run.Until = now + run.Seconds; run.Next = 0; }
                if ((blocked || (!item.Always && now >= run.Until)) && !run.Note.IsDragging) { run.Note.Hide(); continue; }
                run.Note.Update(item);
                if (!run.Note.IsVisible) run.Note.Show();
                occupied.Add(item.Standalone ? run.Note.PlaceStandalone(item, occupied) : run.Note.Follow(run.Pet, occupied));
            }
        }
        public void Stop() {
            stopping = true; timer.Stop();
            if (saveEditor != null) saveEditor();
            foreach (Runtime run in runtimes.Values) DisposeRuntime(run); runtimes.Clear();
            if (editor != null) editor.Close();
        }
        static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(0, 100, 168));
        static readonly ControlTemplate SoftButton = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'><Border x:Name='b' CornerRadius='10' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Opacity' Value='0.75'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='b' Property='BorderBrush' Value='#0064A8'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.4'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
        static readonly Style ChoiceStyle = (Style)System.Windows.Markup.XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='RadioButton'><Setter Property='Padding' Value='15,8'/><Setter Property='Margin' Value='2'/><Setter Property='Foreground' Value='#52637A'/><Setter Property='Background' Value='Transparent'/><Setter Property='FontSize' Value='12'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='RadioButton'><Border x:Name='b' CornerRadius='9' Background='{TemplateBinding Background}' Padding='{TemplateBinding Padding}' BorderBrush='Transparent' BorderThickness='1'><ContentPresenter HorizontalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='b' Property='BorderBrush' Value='#EAAE38'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter><Style.Triggers><Trigger Property='IsChecked' Value='True'><Setter Property='Background' Value='#0064A8'/><Setter Property='Foreground' Value='White'/><Setter Property='FontWeight' Value='SemiBold'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter Property='Opacity' Value='0.8'/></Trigger></Style.Triggers></Style>");
        static Button Button(string text, Action action) {
            Button b = new Button { Content = text, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(3), Background = BubblePalette.BrushFor("#D9EAFE"), BorderBrush = new SolidColorBrush(Color.FromRgb(210, 225, 241)), Foreground = Accent, Template = SoftButton, Cursor = System.Windows.Input.Cursors.Hand }; b.Click += delegate { action(); }; return b;
        }
        static FrameworkElement Choices(string first, string second, bool firstSelected, Action<bool> select) {
            StackPanel panel = new StackPanel { Orientation = Orientation.Horizontal };
            string group = Guid.NewGuid().ToString("N");
            RadioButton a = new RadioButton { Content = first, GroupName = group, Style = ChoiceStyle, IsChecked = firstSelected };
            RadioButton b = new RadioButton { Content = second, GroupName = group, Style = ChoiceStyle, IsChecked = !firstSelected };
            a.Checked += delegate { select(true); }; b.Checked += delegate { select(false); };
            panel.Children.Add(a); panel.Children.Add(b);
            return new Border { CornerRadius = new CornerRadius(12), Background = new SolidColorBrush(Color.FromRgb(237, 242, 248)), Child = panel, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        }
        static WrapPanel SettingRow(StackPanel parent, string label) {
            Grid row = new Grid { Margin = new Thickness(0, 5, 0, 5) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(95) }); row.ColumnDefinitions.Add(new ColumnDefinition());
            row.Children.Add(new TextBlock { Text = label, Foreground = Brushes.SlateGray, FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            WrapPanel content = new WrapPanel { VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(content, 1); row.Children.Add(content); parent.Children.Add(row); return content;
        }
        public void OpenSettings(PetWindow preferred) {
            preferredPet = preferred;
            if (editor != null) { editor.Activate(); return; }
            Window win = new Window { Title = "춘식이 포스트잇 · 할 일", Width = 880, Height = 740, MinWidth = 800, MinHeight = 480, UseLayoutRounding = true, Background = new SolidColorBrush(Color.FromRgb(247, 249, 252)), WindowStartupLocation = WindowStartupLocation.CenterScreen }; editor = win;
            DockPanel root = new DockPanel { Margin = new Thickness(22) }; win.Content = root;
            StackPanel top = new StackPanel(); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
            top.Children.Add(new TextBlock { Text = "춘식이에게 기억을 맡겨요", FontSize = 24, FontWeight = FontWeights.Bold, Foreground = Brushes.DarkSlateBlue });
            top.Children.Add(new TextBlock { Text = "기억할 일을 적고, 춘식이가 보여줄 방식을 골라주세요. 변경사항은 자동으로 저장돼요.", Foreground = Brushes.SlateGray, Margin = new Thickness(0, 8, 0, 12) });
            TextBlock status = new TextBlock { Foreground = Brushes.SlateGray, Margin = new Thickness(0, 10, 0, 0) }; DockPanel.SetDock(status, Dock.Bottom); root.Children.Add(status);
            DockPanel addRow = new DockPanel { Margin = new Thickness(0, 0, 0, 14) }; top.Children.Add(addRow);
            TextBox entry = new TextBox { FontSize = 16, Padding = new Thickness(10), BorderThickness = new Thickness(0), Background = Brushes.Transparent, MaxLength = 160, ToolTip = "예: 기획서 작성 · Enter로 바로 등록" };
            DispatcherTimer debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            Func<bool> persist = delegate { debounce.Stop(); bool ok = Save(); status.Text = ok ? "저장됨 · 프로그램을 다시 열어도 할 일이 남아요" : "할 일 내용을 입력하고 간격을 1~1440분, 표시 시간을 1~3600초로 지정해 주세요. 저장되지 않았어요."; status.Foreground = ok ? Brushes.SlateGray : Brushes.Firebrick; return ok; };
            saveEditor = persist;
            Action changed = delegate { status.Text = "저장 중…"; debounce.Stop(); debounce.Start(); }; debounce.Tick += delegate { persist(); };
            StackPanel list = new StackPanel(); ScrollViewer scroll = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; root.Children.Add(scroll);
            Action render = null;
            Action add = delegate {
                if (String.IsNullOrWhiteSpace(entry.Text)) { status.Text = "할 일을 먼저 입력해 주세요."; entry.Focus(); return; }
                if (settings.Items.Count >= 50) { status.Text = "최대 50개까지 저장할 수 있어요. 완료한 항목을 삭제해 주세요."; return; }
                List<PetWindow> pets = PetWindow.TodoPets(); int index = preferredPet == null ? 0 : Math.Max(0, pets.IndexOf(preferredPet));
                TodoItem item = new TodoItem { Text = entry.Text.Trim(), PetIndex = index, Dedicated = preferredPet != null && preferredPet.IsTodoCompanion };
                settings.Items.Add(item); if (!persist()) { settings.Items.Remove(item); return; } entry.Clear(); render(); scroll.ScrollToBottom();
            };
            Button addButton = Button("＋ 할 일 등록", add); DockPanel.SetDock(addButton, Dock.Right); addRow.Children.Add(addButton); addRow.Children.Add(new Border { Background = Brushes.White, BorderBrush = Brushes.LightSteelBlue, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 3, 7, 3), Child = entry });
            entry.KeyDown += delegate(object sender, System.Windows.Input.KeyEventArgs e) { if (e.Key == System.Windows.Input.Key.Enter) { add(); e.Handled = true; } };
            render = delegate {
                list.Children.Clear();
                foreach (TodoItem item in settings.Items) {
                    StackPanel cardContent = new StackPanel { Margin = new Thickness(18) };
                    Border card = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(18), BorderBrush = new SolidColorBrush(Color.FromRgb(224, 232, 242)), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 14), Child = cardContent }; list.Children.Add(card);
                    DockPanel heading = new DockPanel { Margin = new Thickness(0, 0, 0, item.Done ? 0 : 10) }; cardContent.Children.Add(heading);
                    Button done = Button(item.Done ? "✓ 완료됨" : "○ 진행 중", delegate { if (!item.Done) Complete(item); else { item.Done = false; item.CompletedAt = null; changed(); render(); } });
                    done.ToolTip = item.Done ? "다시 할 일로 되돌리기" : "완료 처리하기";
                    if (item.Done) { done.Background = BubblePalette.BrushFor("#D5F3DF"); done.Foreground = Brushes.DarkGreen; }
                    DockPanel.SetDock(done, Dock.Left); heading.Children.Add(done);
                    Button delete = Button("삭제", delegate { int i = settings.Items.IndexOf(item); settings.Items.Remove(item); if (!persist()) settings.Items.Insert(i, item); render(); });
                    delete.Background = Brushes.White; delete.Foreground = Brushes.SlateGray; DockPanel.SetDock(delete, Dock.Right); heading.Children.Add(delete);
                    TextBox title = new TextBox { Text = item.Text, FontSize = 17, FontWeight = FontWeights.SemiBold, Padding = new Thickness(10, 8, 5, 8), MaxLength = 160, BorderThickness = new Thickness(0), Foreground = item.Done ? Brushes.Gray : new SolidColorBrush(Color.FromRgb(41, 53, 72)) }; heading.Children.Add(title);
                    title.TextChanged += delegate { item.Text = title.Text; changed(); };
                    if (item.Done) { cardContent.Children.Add(new TextBlock { Text = item.CompletedAt.HasValue ? "완료 · " + item.CompletedAt.Value.ToString("yyyy-MM-dd HH:mm") : "완료 · 날짜 기록 없음", Foreground = Brushes.SlateGray, Margin = new Thickness(0, 6, 0, 0) }); continue; }
                    cardContent.Children.Add(Button("▶ 표시 미리보기", delegate { PreviewUI.Todo(win, item); }));
                    WrapPanel display = SettingRow(cardContent, "표시 방식");
                    display.Children.Add(Choices("항상 보여주기", "가끔 알려주기", item.Always, delegate(bool value) { item.Always = value; changed(); render(); }));
                    if (!item.Always) {
                        NumberField interval = new NumberField(item.EveryMinutes, 1440, "리마인드 간격 분") { Margin = new Thickness(10, 0, 0, 0) }; display.Children.Add(interval);
                        display.Children.Add(new TextBlock { Text = "분마다", Foreground = Brushes.SlateGray, Margin = new Thickness(7), VerticalAlignment = VerticalAlignment.Center });
                        interval.Changed += delegate { item.EveryMinutes = interval.Value; changed(); };
                        WrapPanel duration = SettingRow(cardContent, "표시 시간");
                        NumberField seconds = new NumberField(item.DisplaySeconds, 3600, "포스트잇 표시 시간 초"); duration.Children.Add(seconds);
                        duration.Children.Add(new TextBlock { Text = "초 동안 표시", Foreground = Brushes.SlateGray, Margin = new Thickness(7), VerticalAlignment = VerticalAlignment.Center });
                        seconds.ToolTip = "1~3600초 · 표시 시간이 알림 간격보다 길면 숨긴 후 1초 뒤 다시 표시해요.";
                        seconds.Changed += delegate { item.DisplaySeconds = seconds.Value; changed(); };
                    }
                    WrapPanel appearance = SettingRow(cardContent, "춘식이 모양");
                    appearance.Children.Add(Choices("빼꼼 · 작고 귀엽게", "전신 · 온몸이 보이게", item.Peek, delegate(bool value) { item.Peek = value; changed(); render(); }));
                    WrapPanel placement = SettingRow(cardContent, "이동 방식");
                    placement.Children.Add(Choices("놓은 자리에 고정", "중력 켜기", item.Pinned, delegate(bool value) { item.Pinned = value; changed(); render(); }));
                    if (item.Standalone) placement.Children.Add(Button("위치 초기화", delegate { item.HasPosition = false; Runtime run; if (runtimes.TryGetValue(item.Id, out run)) run.Note.ResetPosition(); changed(); }));
                    if (!item.Standalone) {
                        WrapPanel assignment = SettingRow(cardContent, "담당 춘식이");
                        assignment.Children.Add(Choices("기존 춘식이", "새 전담 춘식이", !item.Dedicated, delegate(bool value) { item.Dedicated = !value; changed(); render(); }));
                        if (!item.Dedicated) {
                            List<string> petNames = new List<string>(); for (int i = 0; i < Math.Max(1, PetWindow.Count); i++) petNames.Add("춘식이 " + (i + 1));
                            ComboBox target = new ComboBox { ItemsSource = petNames, SelectedIndex = Math.Max(0, Math.Min(petNames.Count - 1, item.PetIndex)), Width = 104, Margin = new Thickness(10, 0, 0, 0), Padding = new Thickness(5), VerticalAlignment = VerticalAlignment.Center };
                            target.SelectionChanged += delegate { item.PetIndex = target.SelectedIndex; changed(); }; assignment.Children.Add(target);
                        }
                    }
                    WrapPanel colors = SettingRow(cardContent, "메모 색상"); colors.Children.Add(BubblePalette.Picker(item.Color, delegate(string color) { item.Color = color; changed(); }, 6));
                    string explanation = item.Pinned ? "제목이나 춘식이를 끌어 옮기세요. 놓은 위치를 기억해요." : item.Peek ? "끌어 놓으면 포스트잇과 춘식이가 함께 아래로 내려와요." : "담당 춘식이 머리 위에서 메모를 보여줘요.";
                    cardContent.Children.Add(new Border { Background = new SolidColorBrush(Color.FromRgb(247, 249, 252)), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 9, 12, 9), Margin = new Thickness(0, 10, 0, 0), Child = new TextBlock { Text = explanation, FontSize = 12, Foreground = Brushes.SlateGray, TextWrapping = TextWrapping.Wrap } });
                }
                if (settings.Items.Count == 0) list.Children.Add(new TextBlock { Text = "아직 할 일이 없어요. 위에서 하나 등록해 보세요.", Margin = new Thickness(10, 30, 10, 10), Foreground = Brushes.SlateGray });
            };
            renderEditor = render; render();
            win.Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) { if (!stopping && !persist()) { e.Cancel = true; MessageBox.Show(win, status.Text, "입력 확인"); } };
            win.Closed += delegate { debounce.Stop(); editor = null; renderEditor = null; saveEditor = null; }; win.Show();
        }
    }
    public sealed class TodoNote : Window {
        bool disposing;
        readonly DispatcherTimer swallowTimer = new DispatcherTimer();
        public bool IsCompleting { get; private set; }
        public void AnimateComplete(Action finished) {
            if (IsCompleting || disposing) return;
            IsCompleting = true; UpdateLayout();
            double oldHeight = ActualHeight; SizeToContent = SizeToContent.Manual; Height = oldHeight;
            root.IsHitTestVisible = false;
            mascot.Visibility = Visibility.Visible; mascot.Source = peekArt;
            mascot.Width = 86; mascot.Height = 66; mascot.HorizontalAlignment = HorizontalAlignment.Right; mascot.VerticalAlignment = VerticalAlignment.Top; mascot.Margin = new Thickness(0, 0, 7, 0);
            UpdateLayout();
            // Anchor below the white muzzle in the cropped sprite; account for Uniform letterboxing.
            double artScale = Math.Min(mascot.ActualWidth / peekArt.PixelWidth, mascot.ActualHeight / peekArt.PixelHeight);
            Point mouthLocal = new Point((mascot.ActualWidth - peekArt.PixelWidth * artScale) / 2 + peekArt.PixelWidth * 0.495 * artScale,
                (mascot.ActualHeight - peekArt.PixelHeight * artScale) / 2 + peekArt.PixelHeight * 0.795 * artScale);
            Point mouth = mascot.TranslatePoint(mouthLocal, root);
            Point paperTarget = root.TranslatePoint(mouth, paper), tapeTarget = root.TranslatePoint(mouth, tape);
            ScaleTransform shrink = new ScaleTransform(1, 1, paperTarget.X, paperTarget.Y);
            RotateTransform twist = new RotateTransform(0, paperTarget.X, paperTarget.Y);
            TransformGroup transforms = new TransformGroup(); transforms.Children.Add(shrink); transforms.Children.Add(twist); paper.RenderTransform = transforms;
            ScaleTransform tapeShrink = new ScaleTransform(1, 1, tapeTarget.X, tapeTarget.Y); tape.RenderTransform = tapeShrink;
            ScaleTransform cheeks = new ScaleTransform(1, 1); mascot.RenderTransformOrigin = new Point(mouthLocal.X / mascot.ActualWidth, mouthLocal.Y / mascot.ActualHeight); mascot.RenderTransform = cheeks;
            Canvas overlay = new Canvas { IsHitTestVisible = false };
            System.Windows.Shapes.Ellipse opening = new System.Windows.Shapes.Ellipse { Width = 6.5, Height = 7, Fill = new SolidColorBrush(Color.FromRgb(52, 25, 24)), Stroke = Brushes.Black, StrokeThickness = 1.1 };
            Canvas.SetLeft(opening, mouth.X - 3.25); Canvas.SetTop(opening, mouth.Y - 3.5); overlay.Children.Add(opening);
            System.Windows.Shapes.Ellipse tongue = new System.Windows.Shapes.Ellipse { Width = 3.3, Height = 3.2, Fill = new SolidColorBrush(Color.FromRgb(233, 116, 127)) };
            Canvas.SetLeft(tongue, mouth.X - 1.65); Canvas.SetTop(tongue, mouth.Y - 0.3); overlay.Children.Add(tongue); root.Children.Add(overlay);
            Stopwatch animation = Stopwatch.StartNew();
            EventHandler tick = null;
            tick = delegate {
                double t = animation.Elapsed.TotalSeconds;
                double u = Math.Max(0, Math.Min(1, (t - 0.15) / 0.8));
                double scale = Math.Max(0.005, (1 - u) * (1 - u));
                shrink.ScaleX = scale; shrink.ScaleY = Math.Max(0.005, scale * (1 - 0.35 * Math.Sin(u * Math.PI)));
                twist.Angle = Math.Sin(u * Math.PI * 3) * 8;
                tapeShrink.ScaleX = tapeShrink.ScaleY = scale;
                paper.Opacity = tape.Opacity = 1 - Math.Pow(u, 4);
                cheeks.ScaleX = 1 + Math.Sin(t * 15) * 0.035; cheeks.ScaleY = 1 - Math.Sin(t * 15) * 0.025;
                if (t > 0.95) { opening.Visibility = tongue.Visibility = Visibility.Collapsed; mascot.Opacity = Math.Max(0, Math.Min(1, 1 - (t - 1.05) / 0.2)); }
                if (t < 1.25) return;
                swallowTimer.Stop(); swallowTimer.Tick -= tick; animation.Stop();
                root.Children.Remove(overlay); paper.RenderTransform = Transform.Identity; tape.RenderTransform = new RotateTransform(-4); mascot.RenderTransform = Transform.Identity;
                paper.Opacity = tape.Opacity = mascot.Opacity = 1; root.IsHitTestVisible = true;
                SizeToContent = SizeToContent.Height; IsCompleting = false;
                if (current != null) Update(current);
                finished();
            };
            swallowTimer.Interval = TimeSpan.FromMilliseconds(25); swallowTimer.Tick += tick; swallowTimer.Start();
        }
        public void DisposeNote() { disposing = true; swallowTimer.Stop(); Close(); }
        static System.Windows.Media.Imaging.BitmapSource peekArt;
        public static void LoadArt(string folder) {
            System.Windows.Media.Imaging.BitmapImage bitmap = new System.Windows.Media.Imaging.BitmapImage();
            bitmap.BeginInit(); bitmap.UriSource = new Uri(Path.Combine(folder, "chunsik-peek.png")); bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; bitmap.EndInit(); bitmap.Freeze();
            System.Windows.Media.Imaging.FormatConvertedBitmap pixels = new System.Windows.Media.Imaging.FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            int width = pixels.PixelWidth, height = pixels.PixelHeight, stride = width * 4;
            byte[] data = new byte[stride * height]; pixels.CopyPixels(data, stride, 0);
            int left = width, top = height, right = 0, bottom = 0;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) if (data[y * stride + x * 4 + 3] >= 80) { left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y); }
            if (left >= right || top >= bottom) throw new InvalidDataException("Empty peeking sprite");
            peekArt = new System.Windows.Media.Imaging.CroppedBitmap(bitmap, new Int32Rect(left, top, right - left + 1, bottom - top + 1)); peekArt.Freeze();
        }
        readonly Grid root;
        readonly Border tape;
        readonly Image mascot;
        readonly StackPanel noteContent;
        readonly TextBlock heading;
        readonly Action<double, double> moved;
        TodoItem current;
        bool dragging, positionReady;
        public bool IsDragging { get { return dragging; } }
        bool lastStandalone;
        public void ResetPosition() { positionReady = false; }
        readonly Border paper;
        readonly TextBlock message;
        public TodoNote(Action complete, Action edit, Action<double, double> positionChanged) {
            moved = positionChanged;
            Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) { if (!disposing && TodoService.Current != null && !TodoService.Current.Stopping && !PetWindow.IsExiting) { e.Cancel = true; Hide(); } };
            UseLayoutRounding = true; SnapsToDevicePixels = true;
            Title = "춘식이 포스트잇"; Width = 260; SizeToContent = SizeToContent.Height; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true; Background = Brushes.Transparent; Topmost = true; ShowActivated = false; ShowInTaskbar = false;
            root = new Grid { Margin = new Thickness(5) }; Content = root;
            StackPanel content = new StackPanel { Margin = new Thickness(16, 23, 16, 12) }; noteContent = content;
            paper = new Border { CornerRadius = new CornerRadius(2, 2, 16, 2), BorderBrush = new SolidColorBrush(Color.FromRgb(190, 170, 102)), BorderThickness = new Thickness(0, 0, 1, 2), Child = content, Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 6, ShadowDepth = 2, Opacity = 0.2 } }; root.Children.Add(paper);
            tape = new Border { Width = 72, Height = 15, Background = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -3, 0, 0), RenderTransform = new RotateTransform(-4), IsHitTestVisible = false }; root.Children.Add(tape);
            heading = new TextBlock { Text = "TO-DO  /  잊지 말아요", FontSize = 10, Foreground = Brushes.DarkSlateGray, Margin = new Thickness(0, 0, 0, 8), Cursor = System.Windows.Input.Cursors.SizeAll, ToolTip = "끌어서 위치 이동 · 고정 옵션을 켜면 중력 없이 유지" };
            heading.MouseLeftButtonDown += DragNote; content.Children.Add(heading);
            message = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 17, Foreground = new SolidColorBrush(Color.FromRgb(64, 55, 44)) };
            content.Children.Add(new ScrollViewer { Content = message, MaxHeight = 160, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) }; content.Children.Add(buttons);
            Button done = new Button { Content = "✓ 완료", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(3), Background = Brushes.Transparent, BorderBrush = Brushes.Tan }; done.Click += delegate { complete(); }; buttons.Children.Add(done);
            Button settings = new Button { Content = "편집", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(3), Background = Brushes.Transparent, BorderBrush = Brushes.Tan }; settings.Click += delegate { edit(); }; buttons.Children.Add(settings);
            mascot = new Image { Stretch = Stretch.Uniform, Width = 86, Height = 66, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Cursor = System.Windows.Input.Cursors.SizeAll, ToolTip = "포스트잇과 함께 끌어 옮기기" };
            // Match the wandering pet: high-quality downsampling for the large transparent source.
            RenderOptions.SetBitmapScalingMode(mascot, BitmapScalingMode.HighQuality);
            mascot.SnapsToDevicePixels = true; mascot.UseLayoutRounding = true;
            mascot.MouseLeftButtonDown += DragNote; root.Children.Add(mascot);
            ContextMenu menu = new ContextMenu(); MenuItem manage = new MenuItem { Header = "포스트잇 설정" }; manage.Click += delegate { edit(); }; menu.Items.Add(manage); ContextMenu = menu;
        }
        void DragNote(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            if (current == null || e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;
            // Dragging a legacy following note switches it to a detached, fixed companion.
            if (!current.Standalone) current.Pinned = true; lastStandalone = true; positionReady = true; dragging = true;
            try { DragMove(); }
            finally { dragging = false; Rect area = WorkingArea(); ClampPosition(area); moved(Left, Top); }
            e.Handled = true;
        }
        public void Update(TodoItem item) {
            if (lastStandalone != item.Standalone) positionReady = false;
            lastStandalone = item.Standalone; current = item;
            paper.Background = BubblePalette.BrushFor(item.Color); message.Text = item.Always ? "□ " + item.Text : "오늘 ‘" + item.Text + "’ 잊지 않았지?";
            mascot.Visibility = item.Standalone ? Visibility.Visible : Visibility.Collapsed;
            mascot.Source = item.Peek ? peekArt : PetWindow.ToolPose(false);
            mascot.Width = item.Peek ? 86 : 76; mascot.Height = item.Peek ? 66 : 88;
            mascot.VerticalAlignment = item.Peek ? VerticalAlignment.Top : VerticalAlignment.Bottom;
            mascot.HorizontalAlignment = item.Peek ? HorizontalAlignment.Right : HorizontalAlignment.Center;
            mascot.Margin = item.Peek ? new Thickness(0, 0, 7, 0) : new Thickness(0, 3, 0, 0);
            paper.Margin = new Thickness(0, item.Peek ? 20 : 0, 0, item.Pinned && !item.Peek ? 94 : 0);
            tape.Margin = new Thickness(0, item.Peek ? 17 : -3, 0, 0);
            noteContent.Margin = new Thickness(16, item.Peek ? 28 : 23, 16, 12);
            heading.MaxWidth = item.Peek ? 135 : 220;
            heading.HorizontalAlignment = HorizontalAlignment.Left;
        }
        double NoteHeight { get { return ActualHeight > 0 ? ActualHeight : 200; } }
        Rect WorkingArea() {
            PresentationSource source = PresentationSource.FromVisual(this);
            Matrix fromDevice = source == null || source.CompositionTarget == null ? Matrix.Identity : source.CompositionTarget.TransformFromDevice;
            Rect candidate = new Rect(Double.IsNaN(Left) ? 0 : Left, Double.IsNaN(Top) ? 0 : Top, Width, NoteHeight);
            Rect best = SystemParameters.WorkArea; double overlap = 0;
            foreach (System.Windows.Forms.Screen screen in System.Windows.Forms.Screen.AllScreens) {
                System.Drawing.Rectangle work = screen.WorkingArea;
                Point a = fromDevice.Transform(new Point(work.Left, work.Top)), b = fromDevice.Transform(new Point(work.Right, work.Bottom));
                Rect area = new Rect(a, b), intersection = Rect.Intersect(area, candidate);
                double amount = intersection.IsEmpty ? 0 : intersection.Width * intersection.Height;
                if (amount > overlap) { overlap = amount; best = area; }
            }
            return best;
        }
        void ClampPosition(Rect area) {
            Left = Math.Max(area.Left, Math.Min(area.Right - Width, Left));
            Top = Math.Max(area.Top, Math.Min(area.Bottom - NoteHeight, Top));
        }
        public Rect PlaceStandalone(TodoItem item, List<Rect> occupied) {
            if (dragging) return new Rect(Left, Top, Width, NoteHeight);
            if (!positionReady) {
                Rect area = SystemParameters.WorkArea;
                Left = item.HasPosition ? item.NoteLeft : area.Right - Width - 24;
                Top = item.HasPosition ? item.NoteTop : area.Top + 80;
                area = WorkingArea(); ClampPosition(area);
                if (!item.HasPosition) foreach (Rect other in occupied) if (!other.IsEmpty && new Rect(Left, Top, Width, NoteHeight).IntersectsWith(other)) {
                    Top = other.Bottom + 12;
                    if (Top + NoteHeight > area.Bottom) { Top = area.Top + 30; Left = Math.Max(area.Left, Left - Width - 12); }
                }
                ClampPosition(area); positionReady = true;
            }
            Rect workArea = WorkingArea();
            if (!item.Pinned) Top = Math.Min(workArea.Bottom - NoteHeight, Top + 24);
            ClampPosition(workArea);
            return new Rect(Left, Top, Width, NoteHeight);
        }
        public Rect Follow(PetWindow pet, List<Rect> occupied) {
            Rect area; Point head; if (!pet.TrySpeechAnchor(out area, out head)) return Rect.Empty;
            double height = ActualHeight > 0 ? ActualHeight : 180;
            Rect rect = new Rect(Math.Max(area.Left, Math.Min(area.Right - Width, head.X - Width / 2)), Math.Max(area.Top, head.Y - height - 5), Width, height);
            foreach (Rect other in occupied) if (!other.IsEmpty && rect.IntersectsWith(other)) {
                if (other.Top - height - 5 >= area.Top) rect.Y = other.Top - height - 5;
                else if (other.Right + Width + 5 <= area.Right) rect.X = other.Right + 5;
                else if (other.Left - Width - 5 >= area.Left) rect.X = other.Left - Width - 5;
            }
            Left = rect.Left; Top = rect.Top; return rect;
        }
    }
}
