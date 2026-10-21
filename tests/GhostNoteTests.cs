using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ChunsikDesktop;
internal static class GhostNoteTests {
    static int count;
    static void Check(bool ok, string name) { count++; if (!ok) throw new Exception("FAIL: " + name); }
    static T Field<T>(TodoNote note, string name) { return (T)typeof(TodoNote).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(note); }
    static void Pump() { Application.Current.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(delegate {})); }
    [STAThread] public static void Main() {
        Application app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        TodoNote.LoadArt(System.IO.Path.GetFullPath("ChunsikDesktopPet"));
        bool allowSave = true; int completed = 0;
        TodoItem item = new TodoItem { Text = "원본" };
        TodoNote note = new TodoNote(delegate { completed++; }, delegate {}, delegate(double x, double y) {}, delegate { return allowSave; }, delegate {});
        note.Update(item); note.Show(); Pump();
        GhostNoteHeader header = Field<GhostNoteHeader>(note, "ghostHeader");
        Check(NativeNoteInput.IsClickThrough(note), "ghost body has native click-through style");
        Console.WriteLine("Header visible=" + header.IsVisible + ", loaded=" + header.IsLoaded + ", note loaded=" + note.IsLoaded + ", heading loaded=" + Field<TextBlock>(note, "heading").IsLoaded + ", click-through=" + NativeNoteInput.IsClickThrough(header));
        Check(header.IsVisible && !NativeNoteInput.IsClickThrough(header), "separate header remains clickable");
        Check(note.Opacity == 0.6 && note.Topmost, "ghost is translucent and topmost");
        ((Button)header.Content).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
        Check(note.IsEditing && note.Opacity == 1 && !NativeNoteInput.IsClickThrough(note), "header opens opaque interactive edit mode");
        Check(!header.IsVisible, "ghost header hidden during editing");
        Field<TextBox>(note, "inlineEditor").Text = "保存";
        Field<Button>(note, "saveEditButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
        Check(item.Text == "保存" && completed == 0, "save changes text without completing task");
        Check(!note.IsEditing && NativeNoteInput.IsClickThrough(note) && header.IsVisible, "save returns to ghost mode");
        note.BeginEdit(); Field<TextBox>(note, "inlineEditor").Text = "discard";
        Field<Button>(note, "cancelEditButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
        Check(item.Text == "保存" && NativeNoteInput.IsClickThrough(note), "cancel preserves original and restores ghost");
        note.BeginEdit(); allowSave = false;
        Field<Button>(note, "saveEditButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
        Check(note.IsEditing && !NativeNoteInput.IsClickThrough(note) && note.Opacity == 1, "failed save remains interactive");
        allowSave = true; note.FlushEdit(); Pump();
        double oldLeft = header.Left; note.Left += 50; Pump();
        Check(Math.Abs(header.Left - oldLeft - 50) < 2, "header tracks note movement");
        note.Hide(); Pump(); Check(!header.IsVisible, "header hides with note");
        note.Show(); Pump(); Check(header.IsVisible, "header returns with note");
        note.DisposeNote(); Pump(); Check(!header.IsVisible, "header closes with note");
        Console.WriteLine("PASS: " + count + " ghost mode assertions"); app.Shutdown();
    }
}
