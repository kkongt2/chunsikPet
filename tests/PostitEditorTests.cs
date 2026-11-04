using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ChunsikDesktop;
internal static class PostitEditorTests {
    static void Check(bool ok, string message) { if (!ok) throw new Exception("FAIL post-it editor: " + message); }
    static IEnumerable<T> All<T>(DependencyObject root) where T : DependencyObject {
        if (root is T) yield return (T)root;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (T child in All<T>(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    static void Pump() { Application.Current.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(delegate {})); }
    public static void Run() {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChunsikDesktopPet", "todos.xml");
        byte[] old = File.Exists(path) ? File.ReadAllBytes(path) : null, backup = File.Exists(path + ".bak") ? File.ReadAllBytes(path + ".bak") : null;
        TodoSettings data = new TodoSettings();
        data.Items.Add(new TodoItem { Text = "이번 주 회의 자료 정리", Always = true });
        data.Items.Add(new TodoItem { Text = "점심 먹고 가볍게 산책하기", Always = false, EveryMinutes = 30 });
        data.Items.Add(new TodoItem { Text = "완료한 메모", Done = true, CompletedAt = DateTime.Now });
        TodoService service = (TodoService)Activator.CreateInstance(typeof(TodoService), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { data }, null);
        TodoService prior = TodoService.Current; TodoService.Current = service;
        try {
            ((DispatcherTimer)typeof(TodoService).GetField("timer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(service)).Stop();
            service.OpenSettings(null); Pump();
            Window hub = (Window)typeof(SettingsHub).GetField("window", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            hub.Width = 1000; hub.Height = 760; Pump(); hub.UpdateLayout();
            TabControl nav = All<TabControl>(hub).First();
            Check(nav.TabStripPlacement == Dock.Left, "left navigation");
            Check(All<TextBox>(hub).Count(x => x.Text == "완료한 메모") == 0, "completed notes hidden in active filter");
            Expander detail = All<Expander>(hub).First(x => Convert.ToString(x.Header) == "표시 · 알림 설정");
            detail.IsExpanded = true; Pump(); hub.UpdateLayout();
            Check(All<RadioButton>(hub).Any(x => Convert.ToString(x.Content) == "항상 표시"), "detail controls available");
            TextBox title = All<TextBox>(hub).First(x => x.Text == data.Items[0].Text);
            title.Text = "이번 주 회의 자료 정리 · 수정"; Check(data.Items[0].Text == title.Text, "inline edit updates model");
            All<RadioButton>(hub).First(x => Convert.ToString(x.Content) == "완료 보관함").IsChecked = true; Pump();
            Check(All<TextBox>(hub).Any(x => x.Text == "완료한 메모"), "completed filter opens records");
            All<Button>(hub).First(x => Convert.ToString(x.Content) == "복원").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
            Check(!data.Items[2].Done, "restore persists through redesigned controls");
            All<RadioButton>(hub).First(x => Convert.ToString(x.Content) == "진행 중").IsChecked = true; Pump();
            detail = All<Expander>(hub).First(x => Convert.ToString(x.Header) == "표시 · 알림 설정"); detail.IsExpanded = true;
            hub.Width = 800; Pump(); hub.UpdateLayout();
            TabItem notes = nav.SelectedItem as TabItem;
            ScrollViewer outer = notes.Content as ScrollViewer;
            Check(outer.HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled && outer.ExtentWidth <= outer.ViewportWidth + 1, "narrow layout does not scroll horizontally");
            hub.Width = 1000; Pump(); hub.UpdateLayout();
            RenderTargetBitmap image = new RenderTargetBitmap((int)hub.ActualWidth, (int)hub.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            image.Render(hub);
            PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using (MemoryStream output = new MemoryStream()) {
                encoder.Save(output); File.WriteAllBytes("tests/bin/postit-settings.png", output.ToArray());
                Console.WriteLine("POSTIT_PREVIEW_BASE64=" + Convert.ToBase64String(output.ToArray()));
            }
        } finally {
            SettingsHub.Close(); service.Stop(); TodoService.Current = prior;
            if (old == null) File.Delete(path); else File.WriteAllBytes(path, old);
            if (backup == null) File.Delete(path + ".bak"); else File.WriteAllBytes(path + ".bak", backup);
        }
        Console.WriteLine("PASS: post-it cards, inline edit, completion restore and narrow layout");
    }
}
