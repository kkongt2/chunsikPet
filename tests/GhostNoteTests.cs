using System;
using System.Reflection;
using System.Collections.Generic;
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
        bool allowSave = true; int completed = 0, added = 0;
        TodoItem item = new TodoItem { Text = "원본" };
        TodoNote note = new TodoNote(delegate { completed++; }, delegate {}, delegate(double x, double y) {}, delegate { return allowSave; }, delegate { added++; });
        note.Update(item); note.Show(); Pump();
        GhostNoteHeader header = Field<GhostNoteHeader>(note, "ghostHeader");
        Check(NativeNoteInput.IsClickThrough(note), "ghost body has native click-through style");
        Console.WriteLine("Header visible=" + header.IsVisible + ", loaded=" + header.IsLoaded + ", note loaded=" + note.IsLoaded + ", heading loaded=" + Field<TextBlock>(note, "heading").IsLoaded + ", click-through=" + NativeNoteInput.IsClickThrough(header));
        Check(header.IsVisible && !NativeNoteInput.IsClickThrough(header), "separate header remains clickable");
        Check(note.Opacity == 0.6 && note.Topmost, "ghost is translucent and topmost");
        GhostNoteHeader mascotLayer = Field<GhostNoteHeader>(note, "ghostMascot");
        Check(mascotLayer.IsVisible && mascotLayer.Owner == header, "mascot layer is owned by and therefore above header");
        Check(NativeNoteInput.IsClickThrough(mascotLayer), "mascot does not intercept header clicks");
        Check(((Button)header.Content).Content.ToString().Contains("메모 수정/이동은 여기 꾹!"), "header explains edit and move");
        item.NoteOpacity = 0.3; note.Update(item); Pump(); Check(note.Opacity == 0.3, "30 percent opacity supported");
        item.NoteOpacity = 0.9; note.Update(item); Pump(); Check(note.Opacity == 0.9, "90 percent opacity supported");
        item.NoteOpacity = 0.6; note.Update(item); Pump();
        List<GhostNoteHeader> controls = Field<List<GhostNoteHeader>>(note, "ghostControls");
        Check(controls.Count == 5, "buttons and resize grip each have an interactive region");
        foreach (GhostNoteHeader control in controls) Check(control.IsVisible && !NativeNoteInput.IsClickThrough(control), "ghost action region accepts input");
        ((Button)controls[0].Content).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(added == 1 && !note.IsEditing, "add works directly in ghost mode");
        int font = item.FontSize;
        ((Button)controls[1].Content).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(item.FontSize == font - 2 && !note.IsEditing, "smaller works directly");
        ((Button)controls[2].Content).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(item.FontSize == font && !note.IsEditing, "larger works directly");
        ((Button)controls[3].Content).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(completed == 1 && !note.IsEditing, "complete works directly with one click");
        Check(((Button)controls[0].Content).Content.ToString() == "＋" && ((Button)controls[3].Content).Content.ToString() == "✓", "compact add and complete icons");
        Check(typeof(TodoNote).GetField("fontLabel", BindingFlags.Instance | BindingFlags.NonPublic) == null, "font number removed");
        System.Windows.Controls.Primitives.Thumb resize = (System.Windows.Controls.Primitives.Thumb)controls[4].Content;
        double width = note.Width;
        resize.RaiseEvent(new System.Windows.Controls.Primitives.DragStartedEventArgs(0, 0));
        resize.RaiseEvent(new System.Windows.Controls.Primitives.DragDeltaEventArgs(20, 10));
        resize.RaiseEvent(new System.Windows.Controls.Primitives.DragCompletedEventArgs(20, 10, false)); Pump();
        Check(note.Width == width + 20 && item.NoteWidth == note.Width && NativeNoteInput.IsClickThrough(note), "direct resize persists size and keeps ghost body");
        ((Button)header.Content).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
        Check(note.IsEditing && note.Opacity == 1 && !NativeNoteInput.IsClickThrough(note), "header opens opaque interactive edit mode");
        Check(!header.IsVisible, "ghost header hidden during editing");
        foreach (GhostNoteHeader control in controls) Check(!control.IsVisible, "ghost actions hidden while edit controls are active");
        Field<TextBox>(note, "inlineEditor").Text = "保存";
        Field<Button>(note, "saveEditButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
        Check(item.Text == "保存" && completed == 1, "save changes text without completing task");
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
        foreach (GhostNoteHeader control in controls) Check(!control.IsVisible, "action windows hide with note");
        note.Show(); Pump(); Check(header.IsVisible, "header returns with note");
        note.DisposeNote(); Pump(); Check(!header.IsVisible, "header closes with note");
        Console.WriteLine("PASS: " + count + " ghost mode assertions"); app.Shutdown();
    }
}
