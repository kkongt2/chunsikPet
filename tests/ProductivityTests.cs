using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ChunsikDesktop;

internal static class ProductivityTests {
    static void Check(bool value, string name) { if (!value) throw new Exception("FAIL productivity: " + name); }
    static object Get(object target, string name) { return target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target); }
    static void Set(object target, string name, object value) { target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value); }
    static object Call(object target, string name, params object[] args) { return target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args); }
    static void Pump() { Application.Current.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(delegate {})); }
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    public static void Run() {
        TimeToolWindow.OpenTimer(); Pump();
        TimeToolWindow tool = (TimeToolWindow)typeof(TimeToolWindow).GetField("countdownWindow", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        try {
            Check(((DispatcherTimer)Get(tool, "refresh")).Interval.TotalMilliseconds == 1000, "idle refresh reduced");
            Call(tool, "SetDuration", TimeSpan.FromMinutes(25)); Call(tool, "Toggle");
            Check((TimeSpan)Get(tool, "duration") == TimeSpan.FromMinutes(25), "preset starts at requested duration");
            Call(tool, "Extend", 5); Check((TimeSpan)Get(tool, "duration") == TimeSpan.FromMinutes(30), "extend preserves elapsed measurement");
            Call(tool, "SetDuration", TimeSpan.FromMinutes(5)); Check((TimeSpan)Get(tool, "duration") == TimeSpan.FromMinutes(30), "preset cannot reset active timer");
            tool.Close(); Pump();
            Check(!tool.IsVisible && ((Stopwatch)Get(tool, "elapsed")).IsRunning, "close hides active timer without stopping");
            Check(((DispatcherTimer)Get(tool, "refresh")).Interval.TotalMilliseconds == 1000, "hidden timer uses low frequency");
            TimeToolWindow.OpenTimer(); Check(tool.IsVisible, "tray open restores same timer");
            Call(tool, "Toggle"); tool.Close(); Pump();
            Check(!tool.IsVisible && !(bool)Get(tool, "completed"), "paused measurement is also protected");
            PresentationPolicy.Set(-1);
            Set(tool, "duration", TimeSpan.Zero); Call(tool, "UpdateDisplay");
            Check((bool)Get(tool, "completed") && !tool.IsVisible, "hidden completion recorded during do not disturb");
            PresentationPolicy.Set(0); Pump();
            Check(tool.IsVisible && (bool)Get(tool, "completed"), "completion revealed on suppression release");
            Call(tool, "Reset"); Call(tool, "SetDuration", TimeSpan.FromSeconds(359999)); Call(tool, "Extend", 1);
            Check(((TextBox)Get(tool, "hours")).Text == "99" && ((TextBox)Get(tool, "seconds")).Text == "59", "maximum duration remains bounded");
        } finally { PresentationPolicy.Set(0); TimeToolWindow.StopAll(); }
        PetWindow pet = new PetWindow(true);
        try {
            pet.Show(); Pump(); pet.SetPresentationSuppressed(true);
            Check(!((DispatcherTimer)Get(pet, "timer")).IsEnabled && !((Stopwatch)Get(pet, "clock")).IsRunning, "suppressed pet pauses timer and animation clock");
            pet.SetPresentationSuppressed(false);
            Check(((DispatcherTimer)Get(pet, "timer")).IsEnabled && ((Stopwatch)Get(pet, "clock")).IsRunning, "pet resumes normally");
        } finally { pet.Close(); }
        List<TodoItem> items = new List<TodoItem> {
            new TodoItem { Text = "회의 Alpha" },
            new TodoItem { Text = "회의 beta", Done = true, CompletedAt = new DateTime(2026, 10, 20) },
            new TodoItem { Text = "최근 회의", Done = true, CompletedAt = new DateTime(2026, 10, 27) },
            new TodoItem { Text = "날짜 없는 기록", Done = true }
        };
        Check(TodoService.QueryItems(items, " alpha ", 0).Count == 1, "search trims and ignores case");
        Check(TodoService.QueryItems(items, "회의", 1).Count == 1, "search intersects incomplete filter");
        List<TodoItem> done = TodoService.QueryItems(items, "", 2);
        Check(done.Count == 3 && done[0] == items[2] && done[2] == items[3], "completion sorted newest first with undated last");
        Check(TodoService.QueryItems(items, "없는검색", 0).Count == 0 && items.Count == 4, "search never mutates records");
        string todosPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChunsikDesktopPet", "todos.xml");
        byte[] oldTodos = File.Exists(todosPath) ? File.ReadAllBytes(todosPath) : null;
        byte[] oldBackup = File.Exists(todosPath + ".bak") ? File.ReadAllBytes(todosPath + ".bak") : null;
        TodoSettings data = new TodoSettings(); TodoItem restoredItem = new TodoItem { Text = "복원 시험", Done = true, CompletedAt = DateTime.Today };
        data.Items.Add(restoredItem);
        TodoService service = (TodoService)Activator.CreateInstance(typeof(TodoService), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { data }, null);
        try {
            restoredItem.NoteOpacity = 0;
            Check(!(bool)Call(service, "RestoreItem", restoredItem) && restoredItem.Done && restoredItem.CompletedAt.HasValue, "failed restore rolls back completion state");
            restoredItem.NoteOpacity = 0.6;
            Check((bool)Call(service, "RestoreItem", restoredItem) && !restoredItem.Done && !restoredItem.CompletedAt.HasValue, "completion restore commits immediately");
            using (FileStream input = File.OpenRead(todosPath)) {
                TodoSettings saved = (TodoSettings)new System.Xml.Serialization.XmlSerializer(typeof(TodoSettings)).Deserialize(input);
                Check(!saved.Items[0].Done && !saved.Items[0].CompletedAt.HasValue, "restored item remains active after reload");
            }
        } finally {
            service.Stop();
            if (oldTodos == null) File.Delete(todosPath); else File.WriteAllBytes(todosPath, oldTodos);
            if (oldBackup == null) File.Delete(todosPath + ".bak"); else File.WriteAllBytes(todosPath + ".bak", oldBackup);
        }
        Check(!TodoHotKey.Valid(4, 0x41) && !TodoHotKey.Valid(2, 0x7B) && TodoHotKey.Valid(3, 0x4B), "hotkey validation excludes unmodified and reserved F12");
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChunsikDesktopPet", "hotkey.txt");
        byte[] prior = File.Exists(path) ? File.ReadAllBytes(path) : null;
        try {
            using (TodoHotKey hotkey = new TodoHotKey(delegate {})) {
                string message;
                Check(hotkey.TryChange(7, 0x7A, out message), "new shortcut reserves and saves");
                Check(TodoHotKey.Label == "Ctrl + Alt + Shift + F11", "label updated");
                Check(RegisterHotKey(IntPtr.Zero, 0x6756, 7, 0x4B), "reserve conflicting key for test");
                try {
                    Check(!hotkey.TryChange(7, 0x4B, out message) && hotkey.Registered && TodoHotKey.Label.EndsWith("F11"), "conflict preserves old shortcut");
                } finally { UnregisterHotKey(IntPtr.Zero, 0x6756); }
            }
            using (TodoHotKey reloaded = new TodoHotKey(delegate {})) Check(reloaded.Registered && TodoHotKey.Label.EndsWith("F11"), "shortcut restored after restart");
        } finally { if (prior == null) File.Delete(path); else File.WriteAllBytes(path, prior); }
        Console.WriteLine("PASS: timer presets/protection, hidden animation, todo filters and configurable hotkey");
    }
}
