using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ChunsikDesktop;
class UiControlsTests {
    static int checks;
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; }
    [STAThread] static void Main() {
        Application app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        Theme.Install(app);
        NumberField minute = new NumberField(5, 59, "minute"), hour = new NumberField(0, 99, "hour");
        TextBox input = (TextBox)((Grid)minute.Child).Children[0];
        input.Text = "60"; Check(minute.Value == 5, "Reject out-of-range minutes");
        input.Text = "5x"; Check(minute.Value == 5, "Reject nonnumeric input");
        input.Text = "-1"; Check(minute.Value == 5, "Reject negative input");
        input.Text = "59"; Check(minute.Value == 59, "Accept maximum minutes");
        minute.SetValue(25); Check(minute.Value == 25, "Apply preset");
        hour.SetValue(99); Check(hour.Value == 99, "Accept maximum hours");
        StackPanel panel = new StackPanel();
        panel.Children.Add(minute); panel.Children.Add(hour);
        panel.Children.Add(new Button { Content = "button" });
        panel.Children.Add(new CheckBox { Content = "enabled", IsChecked = true });
        panel.Children.Add(new ToggleButton { Template = Theme.SwitchTemplate, Width = 44, Height = 24 });
        panel.Children.Add(new RadioButton { Style = Theme.SegmentStyle, Content = "segment", IsChecked = true });
        panel.Children.Add(new ToggleButton { Style = Theme.DayStyle, Content = "월" });
        Window preview = new Window { Content = panel, Width = 400, Height = 400 };
        preview.Show(); preview.UpdateLayout();
        foreach (UIElement element in panel.Children) { Control control = element as Control; if (control != null) Check(control.Template != null, "Template applied: " + control.GetType().Name); }
        preview.Close();
        bool flushed = false;
        SettingsHub.Attach(new Window { Content = new TextBlock { Text = "settings" } }, "캐릭터", delegate { flushed = true; return true; });
        Window hub = SettingsHub.OwnerFor((Window)typeof(SettingsHub).GetField("window", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null));
        TabControl tabs = (TabControl)hub.Content;
        tabs.UpdateLayout(); Check(tabs.TabStripPlacement == Dock.Left, "Sidebar position");
        Check(tabs.SelectedContent != null, "Selected page loaded");
        tabs.SelectedIndex = 3; hub.UpdateLayout(); Check(tabs.SelectedContent != null, "Time tools loaded without placeholder");
        SettingsHub.Close(); Check(flushed, "Settings flush on close");
        app.Shutdown(); Console.WriteLine("UI control tests passed: " + checks);
    }
}
