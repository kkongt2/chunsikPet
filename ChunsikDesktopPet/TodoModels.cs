using System;
using System.Collections.Generic;
using System.Xml.Serialization;
namespace ChunsikDesktop {
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
        public int FontSize { get; set; }
        public double NoteWidth { get; set; }
        public double NoteHeight { get; set; }
        public string ReminderTemplate { get; set; }
        public bool HasDeadline { get; set; }
        public string DeadlineDate { get; set; }
        public string DeadlineTime { get; set; }
        public string LastDeadline { get; set; }
        [XmlIgnore] public string DeadlineKey { get { return DeadlineDate + " " + DeadlineTime; } }
        public bool TryDeadline(out DateTime due) { return DateTime.TryParseExact(DeadlineKey, "yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out due); }
        public bool DeadlineDue(DateTime now) { DateTime due; return !Done && HasDeadline && TryDeadline(out due) && now >= due && LastDeadline != DeadlineKey; }
        public string ReminderText() { return (ReminderTemplate ?? "오늘 ‘{할일}’ 잊지 않았지?").Replace("{할일}", Text ?? ""); }
        public TodoItem() { Id = Guid.NewGuid().ToString("N"); Text = ""; Pinned = true; Peek = true; Always = true; EveryMinutes = 60; DisplaySeconds = 20; Color = "#FFE894"; FontSize = 17; NoteWidth = 260; NoteHeight = 280; ReminderTemplate = "오늘 ‘{할일}’ 잊지 않았지?"; DeadlineDate = DateTime.Today.ToString("yyyy-MM-dd"); DeadlineTime = "18:00"; LastDeadline = ""; }
        public static bool ValidContent(TodoItem item) {
            DateTime due;
            return item.Text != null && item.Text.Length <= 160 && item.ReminderTemplate != null && item.ReminderTemplate.Length <= 500 && (!item.HasDeadline || item.TryDeadline(out due));
        }
        public static bool ValidAppearance(TodoItem item) {
            return item.FontSize >= 10 && item.FontSize <= 64 && item.NoteWidth >= 220 && item.NoteWidth <= 1200 && item.NoteHeight >= 200 && item.NoteHeight <= 1200;
        }
    }
    public class TodoSettings {
        public List<TodoItem> Items { get; set; }
        [XmlIgnore] public int ActiveCount { get { int count = 0; foreach (TodoItem item in Items) if (item != null && !item.Done) count++; return count; } }
        public TodoSettings() { Items = new List<TodoItem>(); }
    }
}
