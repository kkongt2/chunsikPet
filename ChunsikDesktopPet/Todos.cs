using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
                    if (item.Text == null) item.Text = "";
                    if (Double.IsNaN(item.NoteOpacity) || item.NoteOpacity < 0.1 || item.NoteOpacity > 1) item.NoteOpacity = 0.6;
                    item.NoteOpacity = Math.Max(0.1, Math.Min(1, item.NoteOpacity));
                    if (item.FontSize < 10 || item.FontSize > 64) item.FontSize = 17;
                    if (Double.IsNaN(item.NoteWidth) || item.NoteWidth < 220 || item.NoteWidth > 1200) item.NoteWidth = 260;
                    if (Double.IsNaN(item.NoteHeight) || item.NoteHeight < 200 || item.NoteHeight > 1200) item.NoteHeight = 280;
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
            foreach (TodoItem item in settings.Items) if (item.Text == null || item.Text.Length > 160 || !TodoItem.ValidAppearance(item) || item.EveryMinutes < 1 || item.EveryMinutes > 1440 || item.DisplaySeconds < 1 || item.DisplaySeconds > 3600) return false;
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                string temp = FilePath + ".tmp";
                using (FileStream stream = File.Create(temp)) Serializer.Serialize(stream, settings);
                if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak", true); else File.Move(temp, FilePath);
                if (!stopping) Sync(); return true;
            } catch (Exception e) { MessageBox.Show("할 일 저장에 실패했어요. " + e.Message, "춘식이 할 일"); return false; }
        }
        void DisposeRuntime(Runtime run) { run.Note.DisposeNote(); if (run.Owned != null) run.Owned.Close(); }
        void Sync() {
            HashSet<string> keep = new HashSet<string>();
            foreach (TodoItem item in settings.Items) {
                if (item.Done || item.EveryMinutes < 1 || item.EveryMinutes > 1440 || item.DisplaySeconds < 1 || item.DisplaySeconds > 3600) continue;
                keep.Add(item.Id); Runtime run;
                if (!runtimes.TryGetValue(item.Id, out run)) {
                    TodoItem captured = item;
                    run = new Runtime { Item = item, Always = item.Always, Minutes = item.EveryMinutes, Seconds = item.DisplaySeconds, Until = clock.Elapsed.TotalSeconds + item.DisplaySeconds };
                    run.Note = new TodoNote(delegate { Complete(captured); }, delegate { OpenSettings(null); }, delegate(double left, double top) {
                        captured.NoteLeft = left; captured.NoteTop = top; captured.HasPosition = true; Save(); if (renderEditor != null) renderEditor();
                    }, delegate { bool ok = Save(); if (ok && renderEditor != null) renderEditor(); return ok; }, delegate { AddBlank(captured); }); runtimes.Add(item.Id, run);
                }
                run.Item = item;
                if (run.Always != item.Always || run.Minutes != item.EveryMinutes || run.Seconds != item.DisplaySeconds) { run.Always = item.Always; run.Minutes = item.EveryMinutes; run.Seconds = item.DisplaySeconds; run.Until = clock.Elapsed.TotalSeconds + item.DisplaySeconds; run.Next = 0; }
                if (!item.Standalone && item.Dedicated && (run.Owned == null || run.Owned.HasClosed)) { run.Owned = new PetWindow(true); run.Owned.Show(); }
                if ((item.Standalone || !item.Dedicated) && run.Owned != null) { run.Owned.Close(); run.Owned = null; }
            }
            foreach (string id in new List<string>(runtimes.Keys)) if (!keep.Contains(id)) { DisposeRuntime(runtimes[id]); runtimes.Remove(id); }
        }
        void AddBlank(TodoItem near) {
            if (settings.Items.Count >= 50) { MessageBox.Show("포스트잇은 최대 50개까지 저장할 수 있어요."); return; }
            TodoItem item = new TodoItem { Color = near.Color, FontSize = near.FontSize, NoteOpacity = near.NoteOpacity, NoteWidth = near.NoteWidth, NoteHeight = near.NoteHeight };
            Runtime source;
            if (runtimes.TryGetValue(near.Id, out source)) { item.HasPosition = true; item.NoteLeft = source.Note.Left + 32; item.NoteTop = source.Note.Top + 32; }
            settings.Items.Add(item);
            if (!Save()) { settings.Items.Remove(item); return; }
            if (renderEditor != null) renderEditor();
            // Wait for Tick to show and place the blank note before focusing its editor.
            Tick(); Runtime run;
            if (runtimes.TryGetValue(item.Id, out run)) run.Note.Dispatcher.BeginInvoke(new Action(run.Note.BeginEdit));
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
                if (run.Note.IsCompleting) { if (run.Note.IsVisible) occupied.Add(new Rect(run.Note.Left, run.Note.Top, run.Note.Width, run.Note.Height)); continue; }
                TodoItem item = run.Item;
                run.Pet = item.Standalone ? null : item.Dedicated ? run.Owned : pets.Count == 0 ? null : pets[Math.Max(0, Math.Min(pets.Count - 1, item.PetIndex))];
                bool blocked = !item.Standalone && (run.Pet == null || (ReminderService.Current != null && ReminderService.Current.IsSpeakingFor(run.Pet)));
                if (!item.Always && run.Until <= now && run.Next == 0) run.Next = Math.Max(run.Until + 1, run.Until - run.Seconds + item.EveryMinutes * 60);
                if (!item.Always && run.Next > 0 && now >= run.Next && !blocked) { run.Until = now + run.Seconds; run.Next = 0; }
                if ((blocked || (!item.Always && now >= run.Until)) && !run.Note.IsDragging && !run.Note.IsEditing) { run.Note.Hide(); continue; }
                run.Note.Update(item);
                if (!run.Note.IsVisible) run.Note.Show();
                occupied.Add(item.Standalone ? run.Note.PlaceStandalone(item, occupied) : run.Note.Follow(run.Pet, occupied));
            }
        }
        public void Stop() {
            stopping = true; timer.Stop();
            foreach (Runtime run in runtimes.Values) run.Note.FlushEdit();
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
            if (editor != null) { SettingsHub.Open("포스트잇"); return; }
            Window win = new Window { Title = "춘식이 포스트잇 · 할 일", Width = 880, Height = 740, MinWidth = 800, MinHeight = 480, UseLayoutRounding = true, Background = new SolidColorBrush(Color.FromRgb(247, 249, 252)), WindowStartupLocation = WindowStartupLocation.CenterScreen }; editor = win;
            DockPanel root = new DockPanel { Margin = new Thickness(22) }; win.Content = root;
            StackPanel top = new StackPanel(); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
            top.Children.Add(new TextBlock { Text = "춘식이에게 기억을 맡겨요", FontSize = 24, FontWeight = FontWeights.Bold, Foreground = Brushes.DarkSlateBlue });
            top.Children.Add(new TextBlock { Text = "기억할 일을 적고, 춘식이가 보여줄 방식을 골라주세요. 변경사항은 자동으로 저장돼요.", Foreground = Brushes.SlateGray, Margin = new Thickness(0, 8, 0, 12) });
            TextBlock status = new TextBlock { Foreground = Brushes.SlateGray, Margin = new Thickness(0, 10, 0, 0) }; DockPanel.SetDock(status, Dock.Bottom); root.Children.Add(status);
            DockPanel addRow = new DockPanel { Margin = new Thickness(0, 0, 0, 14) }; top.Children.Add(addRow);
            TextBox entry = new TextBox { FontSize = 16, Padding = new Thickness(10), BorderThickness = new Thickness(0), Background = Brushes.Transparent, MaxLength = 160, ToolTip = "예: 기획서 작성 · Enter로 바로 등록" };
            DispatcherTimer debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            Func<bool> persist = delegate { debounce.Stop(); bool ok = Save(); status.Text = ok ? "저장됨 · 프로그램을 다시 열어도 할 일이 남아요" : "간격을 1~1440분, 표시 시간을 1~3600초로 지정해 주세요. 저장되지 않았어요."; status.Foreground = ok ? Brushes.SlateGray : Brushes.Firebrick; return ok; };
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
            top.Children.Add(Button("＋ 빈 포스트잇", delegate { AddBlank(new TodoItem()); }));
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
                    WrapPanel font = SettingRow(cardContent, "글자 크기");
                    font.Children.Add(Button("A−", delegate { item.FontSize = Math.Max(10, item.FontSize - 2); changed(); render(); }));
                    font.Children.Add(new TextBlock { Text = item.FontSize + "", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6) });
                    font.Children.Add(Button("A+", delegate { item.FontSize = Math.Min(64, item.FontSize + 2); changed(); render(); }));
                    WrapPanel opacityRow = SettingRow(cardContent, "메모 불투명도");
                    Slider opacitySlider = new Slider { Minimum = 10, Maximum = 100, Value = item.NoteOpacity * 100, Width = 180, TickFrequency = 5, IsSnapToTickEnabled = true, VerticalAlignment = VerticalAlignment.Center };
                    TextBlock opacityText = new TextBlock { Text = Math.Round(item.NoteOpacity * 100) + "%", Margin = new Thickness(10, 0, 0, 0) };
                    opacityRow.Children.Add(opacitySlider); opacityRow.Children.Add(opacityText);
                    opacitySlider.ValueChanged += delegate { item.NoteOpacity = opacitySlider.Value / 100; opacityText.Text = Math.Round(opacitySlider.Value) + "%"; changed(); };
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
            win.Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) { if (!stopping && !persist()) { e.Cancel = true; MessageBox.Show(SettingsHub.OwnerFor(win), status.Text, "입력 확인"); } };
            win.Closed += delegate { debounce.Stop(); editor = null; renderEditor = null; saveEditor = null; }; SettingsHub.Attach(win, "포스트잇", persist);
        }
    }
    public sealed class TodoNote : Window {
        bool disposing;
        readonly Stopwatch interactionClock = Stopwatch.StartNew();
        readonly DispatcherTimer swallowTimer = new DispatcherTimer();
        public bool IsCompleting { get; private set; }
        public void AnimateComplete(Action finished) {
            if (IsCompleting || disposing) return;
            IsCompleting = true; SyncGhostHeader(); mascot.Opacity = 1; UpdateLayout();
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
                SizeToContent = SizeToContent.Manual; IsCompleting = false;
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
        readonly Grid noteContent;
        readonly TextBlock heading;
        readonly GhostNoteHeader ghostHeader;
        readonly GhostNoteHeader restoreTab;
        readonly Button modeButton, restoreButton, moreTransparent, lessTransparent;
        readonly TextBlock opacityFeedback;
        readonly DispatcherTimer feedbackTimer;
        readonly GhostNoteHeader ghostMascot;
        readonly Image ghostMascotImage;
        readonly Action<double, double> moved;
        TodoItem current;
        bool dragging, resizing, positionReady;
        public bool IsDragging { get { return dragging || resizing; } }
        public bool IsEditing { get; private set; }
        readonly TextBox inlineEditor;
        readonly Button completeButton, saveEditButton, cancelEditButton;
        double completeAllowedAfter;
        readonly ScrollViewer messageScroll;
        readonly Func<bool> persist;
        readonly List<GhostNoteHeader> ghostControls = new List<GhostNoteHeader>();
        readonly List<FrameworkElement> ghostAnchors = new List<FrameworkElement>();
        bool lastStandalone;
        public void ResetPosition() { positionReady = false; }
        readonly Border paper;
        readonly TextBlock message;
        public TodoNote(Action complete, Action edit, Action<double, double> positionChanged, Func<bool> save, Action add) {
            moved = positionChanged; persist = save;
            Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) { if (!disposing && TodoService.Current != null && !TodoService.Current.Stopping && !PetWindow.IsExiting) { e.Cancel = true; Hide(); } };
            UseLayoutRounding = true; SnapsToDevicePixels = true;
            Title = "춘식이 포스트잇"; Width = 260; Height = 280; MinWidth = 220; MinHeight = 260; MaxWidth = 1200; MaxHeight = 1200; SizeToContent = SizeToContent.Manual; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true; Background = Brushes.Transparent; Topmost = true; ShowActivated = false; ShowInTaskbar = false;
            root = new Grid { Margin = new Thickness(5) }; Content = root;
            Grid content = new Grid { Margin = new Thickness(16, 23, 16, 12) }; noteContent = content;
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition());
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            paper = new Border { CornerRadius = new CornerRadius(2, 2, 16, 2), BorderBrush = new SolidColorBrush(Color.FromRgb(190, 170, 102)), BorderThickness = new Thickness(0, 0, 1, 2), Child = content, Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 6, ShadowDepth = 2, Opacity = 0.2 } }; root.Children.Add(paper);
            tape = new Border { Width = 72, Height = 15, Background = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -3, 0, 0), RenderTransform = new RotateTransform(-4), IsHitTestVisible = false }; root.Children.Add(tape);
            heading = new TextBlock { Text = "수정/이동", FontSize = 10, Height = 24, Foreground = Brushes.DarkSlateGray, Margin = new Thickness(0, 2, 0, 2), Cursor = System.Windows.Input.Cursors.SizeAll, ToolTip = "끌어서 위치 이동 · 고정 옵션을 켜면 중력 없이 유지" };
            DockPanel noteHeader = new DockPanel { LastChildFill = true }; content.Children.Add(noteHeader);
            heading.MouseLeftButtonDown += DragNote;
            Button addNote = NoteButton("＋", "새 빈 포스트잇 추가", add);
            modeButton = NoteButton("클릭 통과 ON", "본문 클릭을 뒤쪽 프로그램으로 통과시키기", ToggleGhost);
            Button minimizeButton = NoteButton("−", "메모 최소화 · 작은 탭을 누르면 복원", ToggleMinimized);
            StackPanel topActions = new StackPanel { Orientation = Orientation.Horizontal };
            topActions.Children.Add(addNote); topActions.Children.Add(minimizeButton);
            DockPanel.SetDock(topActions, Dock.Right); noteHeader.Children.Add(topActions); noteHeader.Children.Add(heading);
            message = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 17, Foreground = new SolidColorBrush(Color.FromRgb(64, 55, 44)) };
            message.ToolTip = "평소에는 클릭이 뒤로 통과해요. 상단 헤더를 눌러 편집하세요.";
            message.Cursor = System.Windows.Input.Cursors.IBeam;
            messageScroll = new ScrollViewer { Background = Brushes.Transparent, Content = message, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Grid.SetRow(messageScroll, 1); content.Children.Add(messageScroll);
            inlineEditor = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxLength = 160, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Visibility = Visibility.Collapsed, Background = Brushes.Transparent, BorderBrush = Brushes.Tan, Padding = new Thickness(4) };
            Grid.SetRow(inlineEditor, 1); content.Children.Add(inlineEditor);
            messageScroll.PreviewMouseLeftButtonDown += delegate(object sender, System.Windows.Input.MouseButtonEventArgs e) { if (e.ClickCount == 2) { BeginEdit(); e.Handled = true; } };
            inlineEditor.PreviewKeyDown += delegate(object sender, System.Windows.Input.KeyEventArgs e) {
                if (e.Key == System.Windows.Input.Key.Escape) { EndEdit(false); e.Handled = true; }
                else if (e.Key == System.Windows.Input.Key.Enter && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0) { EndEdit(true); e.Handled = true; }
            };
            // Moving focus to Save/Cancel must not implicitly save before Click runs.
            // Leaving the window still saves through Deactivated below.
            Deactivated += delegate { if (IsEditing) EndEdit(true); };
            StackPanel footer = new StackPanel { Margin = new Thickness(0, 4, 0, 0) }; Grid.SetRow(footer, 2); content.Children.Add(footer);
            DockPanel status = new DockPanel(); footer.Children.Add(status);
            DockPanel.SetDock(modeButton, Dock.Left); status.Children.Add(modeButton);
            WrapPanel buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right }; footer.Children.Add(buttons);
            StackPanel opacityButtons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 8, 0) };
            moreTransparent = NoteButton("◐−", "더 투명하게 · 불투명도 10% 낮추기 (최소 10%)", delegate { ChangeOpacity(-10); });
            lessTransparent = NoteButton("◐+", "더 선명하게 · 불투명도 10% 높이기 (최대 100%)", delegate { ChangeOpacity(10); });
            moreTransparent.Background = lessTransparent.Background = BubblePalette.BrushFor("#D9EAFE");
            opacityButtons.Children.Add(moreTransparent); opacityButtons.Children.Add(lessTransparent); opacityButtons.HorizontalAlignment = HorizontalAlignment.Right; status.Children.Add(opacityButtons);
            Button smaller = NoteButton("A−", "글자 작게", delegate { ChangeFont(-2); }); buttons.Children.Add(smaller);
            Button larger = NoteButton("A+", "글자 크게", delegate { ChangeFont(2); }); buttons.Children.Add(larger);
            completeButton = NoteButton("✓", "이 할 일을 완료 처리", delegate {
                if (IsEditing || interactionClock.Elapsed.TotalSeconds < completeAllowedAfter) return;
                complete();
            }); completeButton.Margin = new Thickness(18, 2, 2, 2); completeButton.Background = BubblePalette.BrushFor("#D5F3DF"); buttons.Children.Add(completeButton);
            saveEditButton = NoteButton("저장", "메시지만 저장 · Ctrl+Enter", delegate { EndEdit(true); });
            cancelEditButton = NoteButton("취소", "변경사항 취소 · Esc", delegate { EndEdit(false); });
            saveEditButton.Visibility = cancelEditButton.Visibility = Visibility.Collapsed;
            buttons.Children.Add(saveEditButton); buttons.Children.Add(cancelEditButton);
            opacityFeedback = new TextBlock { Visibility = Visibility.Collapsed, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Background = Brushes.DarkSlateGray, Foreground = Brushes.White, Padding = new Thickness(8, 4, 8, 4), IsHitTestVisible = false };
            root.Children.Add(opacityFeedback);
            feedbackTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.4) };
            feedbackTimer.Tick += delegate { feedbackTimer.Stop(); opacityFeedback.Visibility = Visibility.Collapsed; };
            mascot = new Image { Stretch = Stretch.Uniform, Width = 86, Height = 66, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Cursor = System.Windows.Input.Cursors.SizeAll, ToolTip = "포스트잇과 함께 끌어 옮기기" };
            // Match the wandering pet: high-quality downsampling for the large transparent source.
            RenderOptions.SetBitmapScalingMode(mascot, BitmapScalingMode.HighQuality);
            mascot.SnapsToDevicePixels = true; mascot.UseLayoutRounding = true;
            mascot.MouseLeftButtonDown += DragNote; root.Children.Add(mascot);
            ContextMenu menu = new ContextMenu(); MenuItem manage = new MenuItem { Header = "포스트잇 설정" }; manage.Click += delegate { edit(); }; menu.Items.Add(manage); ContextMenu = menu;
            MenuItem finishTask = new MenuItem { Header = "✓ 이 할 일 완료하기" };
            finishTask.Click += delegate { if (EndEdit(true)) complete(); }; menu.Items.Add(finishTask);
            MenuItem opacityMenu = new MenuItem { Header = "메모 불투명도 (10~100%)" }; menu.Items.Add(opacityMenu);
            Slider noteOpacity = new Slider { Minimum = 10, Maximum = 100, Width = 160, TickFrequency = 5, IsSnapToTickEnabled = true, Margin = new Thickness(8) };
            MenuItem opacityControl = new MenuItem { Header = noteOpacity, StaysOpenOnClick = true }; opacityMenu.Items.Add(opacityControl);
            bool loadingOpacity = false;
            menu.Opened += delegate { loadingOpacity = true; noteOpacity.Value = current == null ? 100 : current.NoteOpacity * 100; loadingOpacity = false; };
            noteOpacity.ValueChanged += delegate {
                if (loadingOpacity || current == null) return;
                double old = current.NoteOpacity; current.NoteOpacity = noteOpacity.Value / 100;
                if (!persist()) current.NoteOpacity = old;
                Update(current); ShowOpacityFeedback();
            };
            System.Windows.Controls.Primitives.Thumb grip = new System.Windows.Controls.Primitives.Thumb { Width = 18, Height = 18, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Cursor = System.Windows.Input.Cursors.SizeNWSE, ToolTip = "끌어서 이 포스트잇 크기 조절" };
            grip.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Thumb'><Border Background='Transparent'><TextBlock Text='◢' Foreground='#927845' HorizontalAlignment='Right' VerticalAlignment='Bottom'/></Border></ControlTemplate>");
            grip.DragStarted += delegate { resizing = true; EndEdit(true); };
            grip.DragDelta += delegate(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e) {
                Width = Math.Max(MinWidth, Math.Min(MaxWidth, Width + e.HorizontalChange));
                Height = Math.Max(MinHeight, Math.Min(MaxHeight, Height + e.VerticalChange));
            };
            grip.DragCompleted += delegate {
                resizing = false; if (current == null) return;
                double oldWidth = current.NoteWidth, oldHeight = current.NoteHeight;
                current.NoteWidth = Width; current.NoteHeight = Height;
                if (!persist()) { current.NoteWidth = oldWidth; current.NoteHeight = oldHeight; Update(current); }
                ClampPosition(WorkingArea()); ApplyGhostMode();
            }; root.Children.Add(grip);
            ghostHeader = new GhostNoteHeader(this, BeginEdit,
                delegate { dragging = true; },
                delegate { dragging = false; if (current != null) { current.Pinned = true; positionReady = true; ClampPosition(WorkingArea()); moved(Left, Top); ApplyGhostMode(); } });
            foreach (Button original in new Button[] { addNote, smaller, larger, completeButton, moreTransparent, lessTransparent, modeButton, minimizeButton }) {
                Button source = original;
                Button proxy = NoteButton(source.Content.ToString(), source.ToolTip.ToString(), delegate { source.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); });
                proxy.Margin = new Thickness(0);
                proxy.Background = source == moreTransparent || source == lessTransparent ? source.Background : BubblePalette.BrushFor("#FFE894");
                ghostAnchors.Add(source); ghostControls.Add(new GhostNoteHeader(this, proxy));
            }
            System.Windows.Controls.Primitives.Thumb proxyGrip = new System.Windows.Controls.Primitives.Thumb { Template = grip.Template, Cursor = grip.Cursor, ToolTip = grip.ToolTip };
            proxyGrip.DragStarted += delegate { grip.RaiseEvent(new System.Windows.Controls.Primitives.DragStartedEventArgs(0, 0)); };
            proxyGrip.DragDelta += delegate(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e) { grip.RaiseEvent(new System.Windows.Controls.Primitives.DragDeltaEventArgs(e.HorizontalChange, e.VerticalChange)); };
            proxyGrip.DragCompleted += delegate(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e) { grip.RaiseEvent(new System.Windows.Controls.Primitives.DragCompletedEventArgs(e.HorizontalChange, e.VerticalChange, e.Canceled)); };
            ghostAnchors.Add(grip); ghostControls.Add(new GhostNoteHeader(this, proxyGrip));
            // An owned window above the header keeps the face and paws in front of
            // its native HWND; WPF Panel.ZIndex cannot order separate top-level HWNDs.
            ghostMascotImage = new Image { Stretch = Stretch.Uniform, IsHitTestVisible = false };
            RenderOptions.SetBitmapScalingMode(ghostMascotImage, BitmapScalingMode.HighQuality);
            ghostMascot = new GhostNoteHeader(ghostHeader, ghostMascotImage);
            ghostMascot.SourceInitialized += delegate { NativeNoteInput.SetClickThrough(ghostMascot, true); };
            restoreButton = NoteButton("▣ 메모", "클릭해서 메모 복원", ToggleMinimized);
            restoreButton.Margin = new Thickness(0); restoreButton.Background = BubblePalette.BrushFor("#FFE894");
            restoreTab = new GhostNoteHeader(this, restoreButton);
            SourceInitialized += delegate { ApplyGhostMode(); };
            Loaded += delegate { ApplyGhostMode(); };
            ContentRendered += delegate { SyncGhostHeader(); };
            IsVisibleChanged += delegate { SyncGhostHeader(); if (IsVisible) ApplyGhostMode(); };
            LocationChanged += delegate { SyncGhostHeader(); };
            SizeChanged += delegate { SyncGhostHeader(); };
            LayoutUpdated += delegate { SyncGhostHeader(); };
            Closed += delegate { feedbackTimer.Stop(); restoreTab.Close(); ghostMascot.Close(); ghostHeader.Close(); foreach (GhostNoteHeader control in ghostControls) control.Close(); };
        }
        void ApplyGhostMode() {
            if (ghostHeader == null || disposing) return;
            NativeNoteInput.SetClickThrough(this, current != null && (current.Minimized || (current.GhostEnabled && !IsEditing && !IsDragging)));
            Opacity = 1;
            double memoOpacity = IsEditing || IsDragging ? 1 : current == null ? 0.6 : Math.Max(0.1, Math.Min(1, current.NoteOpacity));
            if (paper.Background != null) { Brush background = paper.Background.Clone(); background.Opacity = memoOpacity; paper.Background = background; }
            messageScroll.Opacity = memoOpacity;
            root.Visibility = current != null && current.Minimized ? Visibility.Hidden : Visibility.Visible;
            SyncGhostHeader();
        }
        void SyncGhostHeader() {
            if (ghostHeader == null || disposing) return;
            bool show = IsVisible && !IsEditing && !IsCompleting && current != null && !current.Minimized;
            if (IsVisible && current != null && current.Minimized && !IsCompleting) restoreTab.Align(root, Math.Min(180, Width - 10), 28); else restoreTab.Hide();
            if (show) {
                double headerWidth = Math.Max(1, heading.ActualWidth);
                if (mascot.Visibility == Visibility.Visible && mascot.ActualWidth > 0) {
                    Point textAt = heading.TranslatePoint(new Point(0, 0), this);
                    Point petAt = mascot.TranslatePoint(new Point(0, 0), this);
                    if (petAt.Y < textAt.Y + 24 && petAt.Y + mascot.ActualHeight > textAt.Y)
                        headerWidth = Math.Max(1, Math.Min(headerWidth, petAt.X - textAt.X - 8));
                }
                ghostHeader.Align(heading, headerWidth);
            } else ghostHeader.Hide();
            for (int i = 0; i < ghostControls.Count; i++) {
                FrameworkElement anchor = ghostAnchors[i];
                Button sourceButton = anchor as Button, proxyButton = ghostControls[i].Content as Button;
                if (sourceButton != null && proxyButton != null) {
                    proxyButton.Content = sourceButton.Content; proxyButton.IsEnabled = sourceButton.IsEnabled;
                    proxyButton.Background = sourceButton.Background;
                }
                if (show && current.GhostEnabled && anchor.IsVisible && anchor.ActualWidth > 0 && anchor.ActualHeight > 0)
                    ghostControls[i].Align(anchor, anchor.ActualWidth, anchor.ActualHeight);
                else ghostControls[i].Hide();
            }
            if (show && mascot.Visibility == Visibility.Visible && mascot.ActualWidth > 0 && mascot.ActualHeight > 0) {
                ghostMascotImage.Source = mascot.Source;
                ghostMascot.Align(mascot, mascot.ActualWidth, mascot.ActualHeight);
                mascot.Opacity = 0;
            } else {
                ghostMascot.Hide();
                if (!IsCompleting) mascot.Opacity = 1;
            }
        }
        static Button NoteButton(string text, string hint, Action action) {
            Button button = new Button { Content = text, ToolTip = hint, Padding = new Thickness(4, 3, 4, 3), Margin = new Thickness(2), FontSize = 11, Background = Brushes.Transparent, BorderBrush = Brushes.Tan, Cursor = System.Windows.Input.Cursors.Hand };
            System.Windows.Automation.AutomationProperties.SetName(button, hint);
            button.MinWidth = 28; button.MinHeight = 26;
            button.Click += delegate { action(); }; return button;
        }
        void ToggleGhost() {
            if (current == null) return;
            bool old = current.GhostEnabled; current.GhostEnabled = !old;
            if (!persist()) current.GhostEnabled = old;
            Update(current);
        }
        void ToggleMinimized() {
            if (current == null || IsCompleting || !EndEdit(true)) return;
            bool old = current.Minimized; current.Minimized = !old;
            if (!persist()) current.Minimized = old;
            Update(current);
        }
        void ChangeOpacity(int deltaPercent) {
            if (current == null) return;
            double old = current.NoteOpacity;
            current.NoteOpacity = Math.Max(10, Math.Min(100, Math.Round(old * 100) + deltaPercent)) / 100.0;
            if (!persist()) current.NoteOpacity = old;
            Update(current); ShowOpacityFeedback();
        }
        void ShowOpacityFeedback() {
            opacityFeedback.Text = "불투명도 " + Math.Round(current.NoteOpacity * 100) + "%";
            opacityFeedback.Visibility = Visibility.Visible; feedbackTimer.Stop(); feedbackTimer.Start();
        }
        void ChangeFont(int delta) {
            if (current == null) return;
            int old = current.FontSize; current.FontSize = Math.Max(10, Math.Min(64, old + delta));
            if (!persist()) current.FontSize = old;
            Update(current);
        }
        public void BeginEdit() {
            if (current == null || current.Minimized || IsCompleting || IsEditing) return;
            IsEditing = true; ApplyGhostMode(); inlineEditor.Text = current.Text;
            completeButton.Visibility = Visibility.Collapsed;
            saveEditButton.Visibility = cancelEditButton.Visibility = Visibility.Visible;
            inlineEditor.FontSize = current.FontSize; messageScroll.Visibility = Visibility.Collapsed; inlineEditor.Visibility = Visibility.Visible;
            Activate(); inlineEditor.Focus(); System.Windows.Input.Keyboard.Focus(inlineEditor); inlineEditor.CaretIndex = inlineEditor.Text.Length;
        }
        public bool FlushEdit() { return EndEdit(true); }
        bool EndEdit(bool save) {
            if (!IsEditing) return true;
            string old = current.Text;
            IsEditing = false;
            if (save) {
                current.Text = inlineEditor.Text;
                if (!persist()) { current.Text = old; IsEditing = true; ApplyGhostMode(); inlineEditor.Focus(); return false; }
            }
            inlineEditor.Visibility = Visibility.Collapsed; messageScroll.Visibility = Visibility.Visible;
            saveEditButton.Visibility = cancelEditButton.Visibility = Visibility.Collapsed;
            completeButton.Visibility = Visibility.Visible;
            // A second click of a Save double-click must never complete the task.
            completeAllowedAfter = interactionClock.Elapsed.TotalSeconds + System.Windows.Forms.SystemInformation.DoubleClickTime / 1000.0 + 0.1;
            Update(current); ApplyGhostMode(); return true;
        }
        void DragNote(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            if (current == null || e.LeftButton != System.Windows.Input.MouseButtonState.Pressed || !EndEdit(true)) return;
            // Dragging a legacy following note switches it to a detached, fixed companion.
            if (!current.Standalone) current.Pinned = true; lastStandalone = true; positionReady = true; dragging = true; ApplyGhostMode();
            try { DragMove(); }
            finally { dragging = false; Rect area = WorkingArea(); ClampPosition(area); moved(Left, Top); ApplyGhostMode(); }
            e.Handled = true;
        }
        public void Update(TodoItem item) {
            Opacity = 1;
            if (lastStandalone != item.Standalone) positionReady = false;
            lastStandalone = item.Standalone; current = item;
            modeButton.Content = item.GhostEnabled ? "클릭 통과 ON" : "클릭 통과 OFF";
            modeButton.Background = item.GhostEnabled ? BubblePalette.BrushFor("#D5F3DF") : BubblePalette.BrushFor("#E6E6E6");
            moreTransparent.IsEnabled = item.NoteOpacity > 0.1; lessTransparent.IsEnabled = item.NoteOpacity < 1;
            string firstLine = (item.Text ?? "").Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            restoreButton.Content = new TextBlock { Text = "▣ " + (String.IsNullOrWhiteSpace(firstLine) ? "빈 메모" : firstLine.Trim()), TextTrimming = TextTrimming.CharacterEllipsis };
            restoreButton.ToolTip = (item.Text ?? "") + "\n클릭해서 복원";
            paper.Background = BubblePalette.BrushFor(item.Color);
            MinHeight = item.Pinned && !item.Peek ? 330 : 260;
            if (!resizing) { Width = item.NoteWidth; Height = Math.Max(MinHeight, item.NoteHeight); }
            message.FontSize = item.FontSize; inlineEditor.FontSize = item.FontSize;
            bool blank = String.IsNullOrWhiteSpace(item.Text);
            message.Text = blank ? "위쪽 헤더를 눌러 할 일을 적어보세요" : item.Always ? item.Text : "오늘 ‘" + item.Text + "’ 잊지 않았지?";
            message.Foreground = blank ? Brushes.SlateGray : new SolidColorBrush(Color.FromRgb(64, 55, 44));
            mascot.Visibility = item.Standalone ? Visibility.Visible : Visibility.Collapsed;
            mascot.Source = item.Peek ? peekArt : PetWindow.ToolPose(false);
            mascot.Width = item.Peek ? 86 : 76; mascot.Height = item.Peek ? 66 : 88;
            mascot.VerticalAlignment = item.Peek ? VerticalAlignment.Top : VerticalAlignment.Bottom;
            mascot.HorizontalAlignment = item.Peek ? HorizontalAlignment.Right : HorizontalAlignment.Center;
            mascot.Margin = item.Peek ? new Thickness(0, 0, 7, 0) : new Thickness(0, 3, 0, 0);
            paper.Margin = new Thickness(0, item.Peek ? 20 : 0, 0, item.Pinned && !item.Peek ? 94 : 0);
            tape.Margin = new Thickness(0, item.Peek ? 17 : -3, 0, 0);
            noteContent.Margin = new Thickness(10, item.Peek ? 48 : 18, 10, 8);
            heading.MaxWidth = double.PositiveInfinity;
            heading.HorizontalAlignment = HorizontalAlignment.Stretch;
            ApplyGhostMode();
        }
        double NoteHeight { get { return ActualHeight > 0 ? ActualHeight : 200; } }
        Rect WorkingArea() { return ScreenLayout.AreaFor(this); }
        public void RememberRecoveredPosition() {
            if (current == null || !current.Pinned || !IsVisible || dragging || IsCompleting) return;
            if (Math.Abs(current.NoteLeft - Left) < 0.5 && Math.Abs(current.NoteTop - Top) < 0.5 && current.HasPosition) return;
            current.NoteLeft = Left; current.NoteTop = Top; current.HasPosition = true; moved(Left, Top);
        }

        void ClampPosition(Rect area) {
            Left = Math.Max(area.Left, Math.Min(area.Right - Width, Left));
            Top = Math.Max(area.Top, Math.Min(area.Bottom - NoteHeight, Top));
        }
        public Rect PlaceStandalone(TodoItem item, List<Rect> occupied) {
            if (dragging || resizing || IsEditing) return new Rect(Left, Top, Width, NoteHeight);
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
