using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml.Serialization;
namespace ChunsikDesktop {
    public class HistoryEntry {
        public string Id { get; set; } public DateTime Time { get; set; }
        public string Title { get; set; } public string State { get; set; }
        public NoticeData Notice { get; set; }
    }
    public static class ReminderHistory {
        static readonly string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChunsikDesktopPet", "history.xml");
        static readonly XmlSerializer serializer = new XmlSerializer(typeof(List<HistoryEntry>));
        static List<HistoryEntry> entries = Load(); static bool warned, dirty; static Window editor; static Action render;
        static DispatcherTimer debounce;
        static List<HistoryEntry> Load() {
            foreach (string p in new string[] { path, path + ".bak" }) try { if (File.Exists(p)) using (FileStream f = File.OpenRead(p)) { List<HistoryEntry> loaded = (List<HistoryEntry>)serializer.Deserialize(f); if (loaded == null) throw new InvalidDataException(); loaded.RemoveAll(delegate(HistoryEntry e) { return e == null || e.Notice == null || e.Notice.Messages == null; }); if (loaded.Count > 300) loaded.RemoveRange(300, loaded.Count - 300); return loaded; } } catch { }
            return new List<HistoryEntry>();
        }
        public static void Flush() {
            if (debounce != null) debounce.Stop(); if (!dirty) return;
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(path)); using (FileStream f = File.Create(path + ".tmp")) serializer.Serialize(f, entries);
                if (File.Exists(path)) File.Replace(path + ".tmp", path, path + ".bak"); else File.Move(path + ".tmp", path);
                dirty = false; warned = false;
            } catch (Exception e) { if (editor != null) editor.Title = "알림 이력 · 저장 실패: " + e.Message; if (!warned) { warned = true; MessageBox.Show("알림 이력을 저장하지 못했어요. 이번 실행 중 기록만 유지됩니다.\n" + e.Message, "알림 이력"); } }
        }
        public static void Record(NoticeData data, string state) {
            HistoryEntry entry = entries.Find(delegate(HistoryEntry e) { return e.Id == data.HistoryId; });
            if (entry == null) { data.HistoryId = Guid.NewGuid().ToString("N"); entry = new HistoryEntry { Id = data.HistoryId, Time = DateTime.Now, Title = data.Label, Notice = data }; entries.Insert(0, entry); }
            entry.State = state; if (entries.Count > 300) entries.RemoveRange(300, entries.Count - 300);
            dirty = true;
            if (debounce == null) { debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) }; debounce.Tick += delegate { Flush(); }; }
            debounce.Stop(); debounce.Start(); if (render != null) render();
        }
        public static void Open() {
            if (editor != null) { editor.Activate(); return; }
            Window win = new Window { Title = "알림 이력 · 최근 300건", Width = 760, Height = 600, WindowStartupLocation = WindowStartupLocation.CenterScreen }; editor = win;
            DockPanel root = new DockPanel { Margin = new Thickness(16) }; win.Content = root;
            WrapPanel toolbar = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) }; DockPanel.SetDock(toolbar, Dock.Top); root.Children.Add(toolbar);
            ComboBox filter = new ComboBox { ItemsSource = new string[] { "전체", "놓침·복구", "다시 알림", "확인", "대기", "표시됨" }, SelectedIndex = 0, Width = 120, Margin = new Thickness(0, 0, 8, 0) }; toolbar.Children.Add(filter);
            DatePicker date = new DatePicker { Width = 135, ToolTip = "날짜로 필터 · 비우면 전체", Margin = new Thickness(0, 0, 8, 0) }; toolbar.Children.Add(date);
            Button allDates = new Button { Content = "모든 날짜", Padding = new Thickness(8), Margin = new Thickness(0, 0, 8, 0) }; allDates.Click += delegate { date.SelectedDate = null; }; toolbar.Children.Add(allDates);
            Button clear = new Button { Content = "이력 비우기", Padding = new Thickness(8) }; toolbar.Children.Add(clear);
            StackPanel list = new StackPanel(); root.Children.Add(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            render = delegate {
                list.Children.Clear(); DateTime lastDate = DateTime.MinValue; int shown = 0;
                foreach (HistoryEntry entry in entries) {
                    string selected = filter.SelectedItem as string ?? "전체", state = entry.State ?? "";
                    if (date.SelectedDate.HasValue && entry.Time.Date != date.SelectedDate.Value.Date) continue;
                    if (selected == "놓침·복구" && !entry.Notice.Missed && !state.Contains("복구")) continue;
                    if (selected != "전체" && selected != "놓침·복구" && !state.Contains(selected)) continue;
                    if (lastDate != entry.Time.Date) { lastDate = entry.Time.Date; list.Children.Add(new TextBlock { Text = lastDate.ToString("yyyy-MM-dd (ddd)"), FontSize = 18, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 14, 0, 10) }); }
                    shown++;
                    StackPanel row = new StackPanel { Margin = new Thickness(12) };
                    row.Children.Add(new TextBlock { Text = entry.Time.ToString("HH:mm:ss") + " · " + state + " · " + entry.Title, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold });
                    Expander messages = new Expander { Header = "메시지 보기", Margin = new Thickness(0, 7, 0, 7) };
                    messages.Content = new TextBlock { Text = String.Join("\n\n", entry.Notice.Messages.ToArray()), TextWrapping = TextWrapping.Wrap, FontSize = 15 }; row.Children.Add(messages);
                    Button replay = new Button { Content = "다시 보기", HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(12, 5, 12, 5) };
                    replay.Click += delegate { ReminderService.Current.Replay(entry.Notice); }; row.Children.Add(replay);
                    list.Children.Add(new Border { CornerRadius = new CornerRadius(12), Background = System.Windows.Media.Brushes.WhiteSmoke, Margin = new Thickness(0, 0, 0, 8), Child = row });
                }
                if (shown == 0) list.Children.Add(new TextBlock { Text = "조건에 맞는 알림 기록이 없어요.", Margin = new Thickness(0, 20, 0, 0) });
            };
            filter.SelectionChanged += delegate { render(); }; date.SelectedDateChanged += delegate { render(); };
            clear.Click += delegate { if (MessageBox.Show(win, "저장된 알림 이력을 모두 지울까요?", "이력 비우기", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return; entries.Clear(); dirty = true; Flush(); render(); };
            win.Closed += delegate { Flush(); editor = null; render = null; }; render(); win.Show();
        }
    }
}
