using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Serialization;
using ChunsikDesktop;

internal static class SettingsImprovementsTests {
    static void Check(bool value, string message) { if (!value) throw new Exception("FAIL settings improvements: " + message); }
    static void Pump() { Application.Current.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(delegate {})); }
    static IEnumerable<T> Find<T>(DependencyObject parent) where T : DependencyObject {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is T) yield return (T)child;
            foreach (T nested in Find<T>(child)) yield return nested;
        }
    }
    public static void Run() {
        SettingsBundle bundle = new SettingsBundle { Format = 2, Version = "1.62.0", Reminders = new ReminderSettings(), Todos = new TodoSettings(), Pets = new PetOptions(), HotkeyModifiers = 2, HotkeyKey = 0x71 };
        bundle.Reminders.Items.Add(new Reminder());
        for (int i = 0; i < 70; i++) bundle.Todos.Items.Add(new TodoItem { Done = true, Text = "완료 " + i });
        bundle.Todos.Items.Add(new TodoItem { Text = "진행 중" });
        string preview = SettingsBackup.Preview(bundle);
        Check(preview.Contains("완료 보관함: 70개") && preview.Contains("진행 중 포스트잇: 1개") && preview.Contains("Ctrl + F2"), "preview shows separate counts and selected shortcut");
        XmlSerializer serializer = new XmlSerializer(typeof(SettingsBundle));
        string xml; using (StringWriter writer = new StringWriter()) { serializer.Serialize(writer, bundle); xml = writer.ToString(); }
        using (StringReader reader = new StringReader(xml)) bundle = (SettingsBundle)serializer.Deserialize(reader);
        Check(bundle.HotkeyModifiers == 2 && bundle.HotkeyKey == 0x71 && bundle.Todos.Items.Count == 71, "new backup roundtrip preserves shortcut and archive");
        MethodInfo buildFiles = typeof(SettingsBackup).GetMethod("BuildFiles", BindingFlags.NonPublic | BindingFlags.Static);
        Dictionary<string, byte[]> files = (Dictionary<string, byte[]>)buildFiles.Invoke(null, new object[] { bundle });
        Check(System.Text.Encoding.UTF8.GetString(files["hotkey.txt"]) == "2,113", "restore includes exact shortcut file");
        bundle.Format = 1; bundle.HotkeyKey = 0;
        Check(SettingsBackup.Preview(bundle).Contains("현재 단축키 유지"), "old backups explicitly preserve shortcut");
        files = (Dictionary<string, byte[]>)buildFiles.Invoke(null, new object[] { bundle });
        Check(!files.ContainsKey("hotkey.txt"), "old backup cannot overwrite shortcut");
        bundle.Format = 2;
        bool invalid = false; try { SettingsBackup.Preview(bundle); } catch (InvalidDataException) { invalid = true; }
        Check(invalid, "invalid shortcut rejected before restore");
        bundle.HotkeyKey = 0x71;
        for (int i = 0; i < 50; i++) bundle.Todos.Items.Add(new TodoItem());
        invalid = false; try { SettingsBackup.Preview(bundle); } catch (InvalidDataException) { invalid = true; }
        Check(invalid, "backup rejects excess active notes even with valid archive");

        string path = ReminderService.SettingsPath;
        byte[] previous = File.Exists(path) ? File.ReadAllBytes(path) : null;
        byte[] previousBak = File.Exists(path + ".bak") ? File.ReadAllBytes(path + ".bak") : null;
        ReminderService oldService = ReminderService.Current;
        ReminderService service = new ReminderService(); ReminderService.Current = service;
        ((DispatcherTimer)typeof(ReminderService).GetField("timer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(service)).Stop();
        Reminder custom = new Reminder { Title = "상세 편집 테스트" };
        service.Snapshot().Items.Add(custom);
        try {
            service.OpenSettings(); Pump();
            Window win = (Window)typeof(SettingsHub).GetField("window", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            ListBox list = null;
            foreach (ListBox candidate in Find<ListBox>(win)) if (System.Windows.Automation.AutomationProperties.GetName(candidate) == "알림 목록") list = candidate;
            Check(list != null && list.Items.Count == service.Snapshot().Items.Count, "all reminders appear in selector");
            list.SelectedIndex = list.Items.Count - 1; Pump();
            RadioButton interval = null;
            foreach (RadioButton button in Find<RadioButton>(win)) if ((string)button.Content == "일정 간격") interval = button;
            Check(interval != null && interval.IsEnabled, "selected custom reminder exposes editable schedule");
            interval.IsChecked = true; Pump();
            Check(((Reminder)((ListBoxItem)list.SelectedItem).Tag).IsInterval, "schedule change preserves selected reminder");
            foreach (double width in new double[] { 1120, 800 }) {
                win.Width = width; win.Height = 800; win.UpdateLayout(); Pump();
                foreach (ScrollViewer viewer in Find<ScrollViewer>(win)) Check(viewer.ScrollableWidth < 1, "no horizontal overflow at width " + width);
                RenderTargetBitmap bitmap = new RenderTargetBitmap((int)win.ActualWidth, (int)win.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(win);
                PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (FileStream output = File.Create("tests/bin/reminder-settings-" + width + ".png")) encoder.Save(output);
            }
            SettingsHub.Close(); Pump();
            Reminder saved = service.Snapshot().Items.Find(delegate(Reminder r) { return r.Id == custom.Id; });
            Check(saved != null && saved.IsInterval, "selected schedule saved through existing close path");
        } finally {
            service.Stop(); SettingsHub.Close(); ReminderService.Current = oldService;
            if (previous == null) { if (File.Exists(path)) File.Delete(path); } else File.WriteAllBytes(path, previous);
            if (previousBak == null) { if (File.Exists(path + ".bak")) File.Delete(path + ".bak"); } else File.WriteAllBytes(path + ".bak", previousBak);
        }
        Console.WriteLine("PASS settings improvements: backup compatibility, archive capacity and responsive reminder editor");
    }
}
