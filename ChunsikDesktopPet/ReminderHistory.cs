using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
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
        static List<HistoryEntry> entries = Load(); static Window editor; static Action render;
        static List<HistoryEntry> Load() {
            foreach (string p in new string[] { path, path + ".bak" }) try { if (File.Exists(p)) using (FileStream f = File.OpenRead(p)) return (List<HistoryEntry>)serializer.Deserialize(f); } catch { }
            return new List<HistoryEntry>();
        }
        static void Save() {
            try { Directory.CreateDirectory(Path.GetDirectoryName(path)); using (FileStream f = File.Create(path + ".tmp")) serializer.Serialize(f, entries);
                if (File.Exists(path)) File.Replace(path + ".tmp", path, path + ".bak"); else File.Move(path + ".tmp", path);
            } catch (Exception e) { if (editor != null) editor.Title = "알림 이력 · 저장 실패: " + e.Message; }
            if (render != null) render();
        }
        public static void Record(NoticeData data, string state) {
            HistoryEntry entry = entries.Find(delegate(HistoryEntry e) { return e.Id == data.HistoryId; });
            if (entry == null) { data.HistoryId = Guid.NewGuid().ToString("N"); entry = new HistoryEntry { Id = data.HistoryId, Time = DateTime.Now, Title = data.Label, Notice = data }; entries.Insert(0, entry); }
            entry.State = state; if (entries.Count > 300) entries.RemoveRange(300, entries.Count - 300); Save();
        }
        public static void Open() {
            if (editor != null) { editor.Activate(); return; }
            Window win = new Window { Title = "알림 이력 · 최근 300건", Width = 720, Height = 550, WindowStartupLocation = WindowStartupLocation.CenterScreen }; editor = win;
            StackPanel list = new StackPanel { Margin = new Thickness(16) }; win.Content = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            render = delegate {
                list.Children.Clear(); list.Children.Add(new TextBlock { Text = "표시됨은 읽음 확인을 뜻하지 않아요. 닫은 알림은 ‘확인’으로 기록돼요.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
                foreach (HistoryEntry entry in entries) {
                    StackPanel row = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
                    row.Children.Add(new TextBlock { Text = entry.Time.ToString("MM-dd HH:mm:ss") + " · " + entry.State + " · " + entry.Title, TextWrapping = TextWrapping.Wrap });
                    Button replay = new Button { Content = "다시 보기", HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(12, 5, 12, 5) };
                    replay.Click += delegate { if (entry.Notice != null) ReminderService.Current.Replay(entry.Notice); }; row.Children.Add(replay); list.Children.Add(row);
                }
                if (entries.Count == 0) list.Children.Add(new TextBlock { Text = "아직 알림 기록이 없어요." });
            };
            win.Closed += delegate { editor = null; render = null; }; render(); win.Show();
        }
    }
}
