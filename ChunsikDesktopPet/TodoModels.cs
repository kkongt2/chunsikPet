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
        // Legacy files may contain Pinned=false; notes are now always fixed.
        [XmlIgnore] public bool Pinned { get { return true; } set { } }
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
        public bool Minimized { get; set; }
        public double NoteOpacity { get; set; }
        public double NoteWidth { get; set; }
        public double NoteHeight { get; set; }
        public TodoItem() { Id = Guid.NewGuid().ToString("N"); Text = ""; Pinned = true; Peek = true; Always = true; EveryMinutes = 60; DisplaySeconds = 20; Color = "#FFE894"; FontSize = 17; NoteOpacity = 0.6; NoteWidth = 260; NoteHeight = 280; }
        public static bool ValidAppearance(TodoItem item) {
            return item.NoteOpacity >= 0.1 && item.NoteOpacity <= 1 && item.FontSize >= 10 && item.FontSize <= 64 && item.NoteWidth >= 220 && item.NoteWidth <= 1200 && item.NoteHeight >= 100 && item.NoteHeight <= 1200;
        }
    }
    public class TodoSettings {
        public const int ActiveLimit = 50, ArchiveLimit = 1000;
        public List<TodoItem> Items { get; set; }
        public int CountItems(bool done) { return Items.FindAll(delegate(TodoItem item) { return item != null && item.Done == done; }).Count; }
        public bool HasRoom(bool done) { return CountItems(done) < (done ? ArchiveLimit : ActiveLimit); }
        public bool ValidCounts() { return Items != null && CountItems(false) <= ActiveLimit && CountItems(true) <= ArchiveLimit; }
        public TodoSettings() { Items = new List<TodoItem>(); }
    }
}
