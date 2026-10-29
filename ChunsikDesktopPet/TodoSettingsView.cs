using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace ChunsikDesktop {
    public sealed partial class TodoService {
        static Button Button(string text, Action action) { return SettingsTheme.Button(text, action); }
        static readonly Style SettingSegmentStyle = (Style)System.Windows.Markup.XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='RadioButton'><Setter Property='FontSize' Value='13'/><Setter Property='Padding' Value='4,7'/><Setter Property='Margin' Value='1'/><Setter Property='Background' Value='Transparent'/><Setter Property='Foreground' Value='#52637A'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='RadioButton'><Border x:Name='box' Background='{TemplateBinding Background}' BorderBrush='Transparent' BorderThickness='1' CornerRadius='7' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='box' Property='Opacity' Value='0.8'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='box' Property='BorderBrush' Value='#3568D4'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter><Style.Triggers><Trigger Property='IsChecked' Value='True'><Setter Property='Background' Value='#3568D4'/><Setter Property='Foreground' Value='White'/><Setter Property='FontWeight' Value='SemiBold'/></Trigger></Style.Triggers></Style>");
        static Border SettingChoice(string[] labels, int selected, Action<int> change) {
            string group = Guid.NewGuid().ToString("N");
            System.Windows.Controls.Primitives.UniformGrid segments = new System.Windows.Controls.Primitives.UniformGrid { Columns = Math.Min(2, Math.Max(1, labels.Length)) };
            for (int i = 0; i < labels.Length; i++) {
                int index = i;
                RadioButton segment = new RadioButton { Content = labels[i], ToolTip = labels[i], GroupName = group, IsChecked = i == selected, Style = SettingSegmentStyle };
                System.Windows.Automation.AutomationProperties.SetName(segment, labels[i]);
                segment.Checked += delegate { change(index); }; segments.Children.Add(segment);
            }
            return new Border { Background = new SolidColorBrush(Color.FromRgb(237, 242, 248)), CornerRadius = new CornerRadius(9), Padding = new Thickness(2), Margin = new Thickness(0, 3, 0, 3), Child = segments };
        }
        public void OpenSettings(PetWindow preferred) {
            preferredPet = preferred;
            if (editor != null) { SettingsHub.Open("포스트잇"); return; }
            Window win = new Window { Title = "포스트잇", Width = 1060, Height = 760, MinWidth = 620, MinHeight = 480, UseLayoutRounding = true, Background = SettingsTheme.Canvas }; editor = win;
            DockPanel root = new DockPanel { Margin = new Thickness(24) }; win.Content = root; SettingsTheme.Apply(root);
            StackPanel top = new StackPanel(); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
            TextBlock heading = SettingsTheme.Text("포스트잇", 26); heading.Foreground = SettingsTheme.Ink; heading.FontWeight = FontWeights.Bold; top.Children.Add(heading);
            TextBlock intro = SettingsTheme.Text("할 일을 정리하고, 필요한 순간에 꺼내 보세요.");
            intro.Margin = new Thickness(0, 6, 0, 16); top.Children.Add(intro);
            WrapPanel tools = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) }; top.Children.Add(tools);
            Button visibility = Button(AllHidden ? "전체 보이기" : "전체 숨기기", delegate { ToggleAllVisibility(); });
            tools.Children.Add(visibility);
            tools.Children.Add(Button("빈 메모 추가", delegate { AddBlank(new TodoItem()); }));
            Expander shortcuts = new Expander { Header = "단축키 · " + TodoHotKey.Label, Content = TodoHotKey.BuildSettings(), Margin = new Thickness(0, 0, 0, 14), Foreground = SettingsTheme.Muted, FontSize = 13 };
            top.Children.Add(shortcuts);
            Action hotkeyChanged = delegate { shortcuts.Header = "단축키 · " + TodoHotKey.Label; };
            TodoHotKey.Changed += hotkeyChanged;
            TextBlock status = SettingsTheme.Text("변경사항은 자동 저장됩니다.");
            status.Margin = new Thickness(0, 10, 0, 0); DockPanel.SetDock(status, Dock.Bottom); root.Children.Add(status);
            DispatcherTimer debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            Func<bool> persist = delegate {
                debounce.Stop(); bool ok = Save();
                status.Text = ok ? "저장됨" : "저장되지 않았어요. 간격은 1~1440분, 표시 시간은 1~3600초인지 확인해 주세요.";
                status.Foreground = ok ? SettingsTheme.Muted : Brushes.Firebrick; return ok;
            };
            saveEditor = persist;
            Action changed = delegate { status.Text = "저장 중…"; debounce.Stop(); debounce.Start(); };
            debounce.Tick += delegate { persist(); };
            StackPanel list = new StackPanel();
            ScrollViewer scroll = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 8, 0) };
            root.Children.Add(scroll);
            Action render = null;
            HashSet<string> expanded = new HashSet<string>();
            DockPanel addRow = new DockPanel { Margin = new Thickness(0, 0, 0, 14) }; top.Children.Add(addRow);
            TextBox entry = new TextBox { MaxLength = 160, ToolTip = "새 할 일 · Enter로 추가", VerticalContentAlignment = VerticalAlignment.Center };
            System.Windows.Automation.AutomationProperties.SetName(entry, "새 할 일");
            TextBlock placeholder = SettingsTheme.Text("새 할 일을 입력하세요");
            placeholder.Margin = new Thickness(12, 0, 0, 0); placeholder.VerticalAlignment = VerticalAlignment.Center; placeholder.IsHitTestVisible = false;
            Grid entryBox = new Grid(); entryBox.Children.Add(entry); entryBox.Children.Add(placeholder);
            entry.TextChanged += delegate { placeholder.Visibility = entry.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; };
            WrapPanel filters = new WrapPanel { Margin = new Thickness(0, 0, 0, 16) }; top.Children.Add(filters);
            TextBox search = new TextBox { Width = 190, ToolTip = "메모 내용 검색", Margin = new Thickness(0, 0, 12, 6) };
            System.Windows.Automation.AutomationProperties.SetName(search, "메모 검색");
            Grid searchBox = new Grid(); searchBox.Children.Add(search);
            TextBlock searchHint = SettingsTheme.Text("메모 검색"); searchHint.Margin = new Thickness(12, 0, 12, 6); searchHint.VerticalAlignment = VerticalAlignment.Center; searchHint.IsHitTestVisible = false; searchBox.Children.Add(searchHint); filters.Children.Add(searchBox);
            int filter = 1; // Start with active work; completed notes stay one click away.
            Border filterChoice = SettingChoice(new[] { "전체", "진행 중", "완료" }, filter, delegate(int value) { filter = value; if (render != null) render(); });
            ((System.Windows.Controls.Primitives.UniformGrid)filterChoice.Child).Columns = 3;
            filterChoice.Width = 240; filterChoice.Margin = new Thickness(0, 0, 12, 6); filters.Children.Add(filterChoice);
            TextBlock resultCount = SettingsTheme.Text(""); resultCount.VerticalAlignment = VerticalAlignment.Center; filters.Children.Add(resultCount);
            search.TextChanged += delegate { searchHint.Visibility = search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; if (render != null) render(); };
            Action add = delegate {
                if (String.IsNullOrWhiteSpace(entry.Text)) { status.Text = "할 일을 입력해 주세요."; entry.Focus(); return; }
                if (settings.Items.Count >= 50) { status.Text = "최대 50개까지 저장할 수 있어요. 완료 항목을 정리해 주세요."; return; }
                List<PetWindow> pets = PetWindow.TodoPets(); int index = preferredPet == null ? 0 : Math.Max(0, pets.IndexOf(preferredPet));
                TodoItem item = new TodoItem { Text = entry.Text.Trim(), PetIndex = index, Dedicated = preferredPet != null && preferredPet.IsTodoCompanion };
                settings.Items.Add(item); if (!persist()) { settings.Items.Remove(item); return; }
                entry.Clear(); search.Clear(); filter = 1;
                ((RadioButton)((System.Windows.Controls.Primitives.UniformGrid)filterChoice.Child).Children[1]).IsChecked = true;
                render(); scroll.ScrollToBottom(); entry.Focus();
            };
            Button addButton = SettingsTheme.Button("할 일 추가", add, true); addButton.Margin = new Thickness(10, 0, 0, 0);
            DockPanel.SetDock(addButton, Dock.Right); addRow.Children.Add(addButton); addRow.Children.Add(entryBox);
            entry.KeyDown += delegate(object sender, System.Windows.Input.KeyEventArgs e) { if (e.Key == System.Windows.Input.Key.Enter) { add(); e.Handled = true; } };
            Button clearCompleted = Button("완료 항목 정리…", delegate {
                List<TodoItem> before = new List<TodoItem>(settings.Items);
                int count = before.Count(item => item.Done);
                if (count == 0) return;
                if (MessageBox.Show(SettingsHub.OwnerFor(win), "완료한 메모 " + count + "개를 모두 삭제할까요? 검색 결과에 없는 완료 메모도 포함되며 되돌릴 수 없습니다.", "완료 항목 정리", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                settings.Items.RemoveAll(item => item.Done);
                if (!persist()) { settings.Items.Clear(); settings.Items.AddRange(before); }
                render();
            });
            tools.Children.Add(clearCompleted);
            render = delegate {
                list.Children.Clear();
                visibility.Content = AllHidden ? "전체 보이기" : "전체 숨기기";
                clearCompleted.IsEnabled = settings.Items.Any(item => item.Done);
                List<TodoItem> visibleItems = QueryItems(settings.Items, search.Text, filter);
                resultCount.Text = visibleItems.Count + "개";
                foreach (TodoItem item in visibleItems) {
                    StackPanel content = new StackPanel();
                    Border card = SettingsTheme.Card(content); list.Children.Add(card);
                    Grid summary = new Grid(); summary.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); summary.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); content.Children.Add(summary);
                    TextBox title = new TextBox { Text = item.Text, FontSize = 15, MaxLength = 160, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
                        MinHeight = 56, MaxHeight = 130, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, BorderThickness = new Thickness(0),
                        Background = Brushes.Transparent, Foreground = item.Done ? SettingsTheme.Muted : SettingsTheme.Ink, Margin = new Thickness(0, 0, 12, 0), ToolTip = "메모 내용 · 자동 저장" };
                    System.Windows.Automation.AutomationProperties.SetName(title, "메모 내용");
                    title.TextChanged += delegate { item.Text = title.Text; changed(); }; summary.Children.Add(title);
                    Button done = Button(item.Done ? "복원" : "완료", delegate { if (!item.Done) Complete(item); else RestoreItem(item); });
                    done.VerticalAlignment = VerticalAlignment.Top; done.Margin = new Thickness(0); Grid.SetColumn(done, 1); summary.Children.Add(done);
                    DockPanel footer = new DockPanel { Margin = new Thickness(0, 10, 0, 0) }; content.Children.Add(footer);
                    StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal }; DockPanel.SetDock(actions, Dock.Right); footer.Children.Add(actions);
                    if (!item.Done) actions.Children.Add(Button("미리보기", delegate { PreviewUI.Todo(win, item); }));
                    actions.Children.Add(Button("삭제", delegate { int i = settings.Items.IndexOf(item); settings.Items.Remove(item); if (!persist()) settings.Items.Insert(i, item); render(); }));
                    TextBlock caption = SettingsTheme.Text(item.Done ? "완료 · " + (item.CompletedAt.HasValue ? item.CompletedAt.Value.ToString("yyyy.MM.dd HH:mm") : "날짜 없음") :
                        (item.Always ? "항상 표시" : item.EveryMinutes + "분마다 · " + item.DisplaySeconds + "초 표시") + "  ·  " + (item.Peek ? "빼꼼" : "전신"));
                    caption.VerticalAlignment = VerticalAlignment.Center; caption.Margin = new Thickness(0, 0, 10, 0); footer.Children.Add(caption);
                    if (item.Done) continue;
                    WrapPanel details = new WrapPanel { Margin = new Thickness(0, 14, 0, 0) };
                    Expander detail = new Expander { Header = "표시 · 알림 설정", Content = details, IsExpanded = expanded.Contains(item.Id), Foreground = SettingsTheme.Muted, FontSize = 13, Margin = new Thickness(0, 14, 0, 0) };
                    detail.Expanded += delegate { expanded.Add(item.Id); }; detail.Collapsed += delegate { expanded.Remove(item.Id); }; content.Children.Add(detail);
                    StackPanel display = new StackPanel();
                    display.Children.Add(SettingChoice(new[] { "항상 표시", "가끔 알림" }, item.Always ? 0 : 1, delegate(int value) { item.Always = value == 0; changed(); render(); }));
                    if (!item.Always) {
                        StackPanel intervalRow = new StackPanel { Orientation = Orientation.Horizontal };
                        NumberField interval = new NumberField(item.EveryMinutes, 1440, "알림 간격 분"); intervalRow.Children.Add(interval);
                        intervalRow.Children.Add(new TextBlock { Text = "분마다", Margin = new Thickness(8), VerticalAlignment = VerticalAlignment.Center }); display.Children.Add(intervalRow);
                        interval.Changed += delegate { item.EveryMinutes = interval.Value; caption.Text = item.EveryMinutes + "분마다 · " + item.DisplaySeconds + "초 표시  ·  " + (item.Peek ? "빼꼼" : "전신"); changed(); };
                        StackPanel durationRow = new StackPanel { Orientation = Orientation.Horizontal };
                        NumberField seconds = new NumberField(item.DisplaySeconds, 3600, "표시 시간 초"); durationRow.Children.Add(seconds);
                        durationRow.Children.Add(new TextBlock { Text = "초 표시", Margin = new Thickness(8), VerticalAlignment = VerticalAlignment.Center }); display.Children.Add(durationRow);
                        seconds.Changed += delegate { item.DisplaySeconds = seconds.Value; caption.Text = item.EveryMinutes + "분마다 · " + item.DisplaySeconds + "초 표시  ·  " + (item.Peek ? "빼꼼" : "전신"); changed(); };
                    }
                    details.Children.Add(SettingsTheme.Field("표시 방식", display));
                    StackPanel placement = new StackPanel();
                    placement.Children.Add(SettingChoice(new[] { "빼꼼", "전신" }, item.Peek ? 0 : 1, delegate(int value) { item.Peek = value == 0; changed(); render(); }));
                    if (item.Standalone) placement.Children.Add(Button("위치 초기화", delegate { item.HasPosition = false; Runtime run; if (runtimes.TryGetValue(item.Id, out run)) run.Note.ResetPosition(); changed(); }));
                    else {
                        placement.Children.Add(SettingChoice(new[] { "기존 춘식이", "새 전담 춘식이" }, item.Dedicated ? 1 : 0, delegate(int value) { item.Dedicated = value == 1; changed(); render(); }));
                        if (!item.Dedicated) {
                            ComboBox pets = new ComboBox { Margin = new Thickness(0, 8, 0, 0), MinHeight = 32 };
                            for (int i = 0; i < Math.Max(1, PetWindow.Count); i++) pets.Items.Add("춘식이 " + (i + 1));
                            pets.SelectedIndex = Math.Max(0, Math.Min(pets.Items.Count - 1, item.PetIndex));
                            pets.SelectionChanged += delegate { item.PetIndex = pets.SelectedIndex; changed(); }; placement.Children.Add(pets);
                        }
                    }
                    details.Children.Add(SettingsTheme.Field("춘식이 모양", placement));
                    StackPanel font = new StackPanel { Orientation = Orientation.Horizontal };
                    TextBlock fontValue = SettingsTheme.Text(item.FontSize + " pt"); fontValue.Width = 60; fontValue.VerticalAlignment = VerticalAlignment.Center;
                    font.Children.Add(Button("A−", delegate { item.FontSize = Math.Max(10, item.FontSize - 2); fontValue.Text = item.FontSize + " pt"; changed(); }));
                    font.Children.Add(fontValue);
                    font.Children.Add(Button("A+", delegate { item.FontSize = Math.Min(64, item.FontSize + 2); fontValue.Text = item.FontSize + " pt"; changed(); }));
                    details.Children.Add(SettingsTheme.Field("글자 크기", font));
                    StackPanel opacity = new StackPanel();
                    TextBlock clarity = SettingsTheme.Text(Math.Round(item.NoteOpacity * 100) + "% · 100%가 가장 선명해요"); opacity.Children.Add(clarity);
                    Slider slider = new Slider { Minimum = 10, Maximum = 100, Value = item.NoteOpacity * 100, TickFrequency = 5, IsSnapToTickEnabled = true, Margin = new Thickness(0, 8, 0, 0) };
                    System.Windows.Automation.AutomationProperties.SetName(slider, "메모 선명도");
                    slider.ValueChanged += delegate { item.NoteOpacity = slider.Value / 100; clarity.Text = Math.Round(slider.Value) + "% · 100%가 가장 선명해요"; changed(); }; opacity.Children.Add(slider);
                    details.Children.Add(SettingsTheme.Field("선명도", opacity));
                    details.Children.Add(SettingsTheme.Field("메모 색상", BubblePalette.Picker(item.Color, delegate(string color) { item.Color = color; changed(); }, 6)));
                }
                if (visibleItems.Count == 0) {
                    TextBlock empty = SettingsTheme.Text(settings.Items.Count == 0 ? "아직 메모가 없어요. 위에서 첫 할 일을 추가해 보세요." : "조건에 맞는 메모가 없어요. 검색어나 필터를 바꿔 보세요.", 15);
                    empty.Margin = new Thickness(12, 30, 12, 30); list.Children.Add(SettingsTheme.Card(empty));
                }
            };
            // Keep the visibility action label in sync without replacing focused editor controls.
            DispatcherTimer visibilityTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            visibilityTimer.Tick += delegate { visibility.Content = AllHidden ? "전체 보이기" : "전체 숨기기"; };
            visibilityTimer.Start();
            renderEditor = render; render();
            win.Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) { if (!stopping && !persist()) { e.Cancel = true; MessageBox.Show(SettingsHub.OwnerFor(win), status.Text, "입력 확인"); } };
            win.Closed += delegate { TodoHotKey.Changed -= hotkeyChanged; debounce.Stop(); visibilityTimer.Stop(); editor = null; renderEditor = null; saveEditor = null; };
            SettingsHub.Attach(win, "포스트잇", persist);
        }
    }
}
