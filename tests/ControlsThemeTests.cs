using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
internal static class ControlsThemeTests {
    static void Check(bool ok, string text) { if (!ok) throw new Exception("FAIL theme: " + text); }
    static void Pump() { Application.Current.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(delegate {})); }
    static IEnumerable<T> All<T>(DependencyObject root) where T : DependencyObject {
        if (root is T) yield return (T)root;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (T child in All<T>(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    public static void Run() {
        Window win = new Window { Width = 900, Height = 720, Background = new SolidColorBrush(Color.FromRgb(245,247,250)), Title = "공용 컨트롤 테마" };
        StackPanel root = new StackPanel { Margin = new Thickness(24) }; win.Content = root;
        root.Children.Add(new TextBlock { Text = "춘식이 · 공용 UI", FontSize = 24, Margin = new Thickness(0,0,0,16) });
        TabControl tabs = new TabControl { Height = 125 };
        tabs.Items.Add(new TabItem { Header = "기본 설정", Content = new TextBlock { Text = "선택된 탭의 내용", Margin = new Thickness(14) } });
        tabs.Items.Add(new TabItem { Header = "표시 설정", Content = new TextBox { Text = "탭 안에서도 같은 입력 스타일", Margin = new Thickness(14) } });
        root.Children.Add(tabs);
        WrapPanel row = new WrapPanel { Margin = new Thickness(0,12,0,12) }; root.Children.Add(row);
        CheckBox check = new CheckBox { Content = "자동으로 잠들기", Margin = new Thickness(0,8,20,0), IsThreeState = true }; row.Children.Add(check);
        ComboBox combo = new ComboBox { Width = 200, ItemsSource = new[] { "모니터 1", "모니터 2", "모니터 3" }, SelectedIndex = 0 }; row.Children.Add(combo);
        Button button = new Button { Content = "미리보기", Margin = new Thickness(12,0,0,0) }; row.Children.Add(button);
        TextBox text = new TextBox { Text = "메모와 숫자 입력 · 한글 입력도 그대로", AcceptsReturn = true, Height = 65, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,12) }; root.Children.Add(text);
        Slider slider = new Slider { Minimum = 0, Maximum = 20, Value = 5, LargeChange = 5, TickFrequency = 1, IsSnapToTickEnabled = true, TickPlacement = TickPlacement.BottomRight, Margin = new Thickness(0,0,0,16) }; root.Children.Add(slider);
        StackPanel longContent = new StackPanel { Width = 1200 };
        for (int i = 0; i < 30; i++) longContent.Children.Add(new TextBlock { Text = "알림 기록 " + (i + 1) + "  ·  기존 스크롤 동작 유지", Height = 32, Padding = new Thickness(10,5,0,0) });
        ScrollViewer scroll = new ScrollViewer { Content = longContent, Height = 185, VerticalScrollBarVisibility = ScrollBarVisibility.Visible, HorizontalScrollBarVisibility = ScrollBarVisibility.Visible }; root.Children.Add(scroll);
        try {
            win.Show(); Pump(); win.UpdateLayout();
            foreach (Control control in new Control[] { tabs, check, combo, button, text, slider }) {
                Check(control.Style != null && control.Template != null, control.GetType().Name + " has shared style/template");
            }
            int toggles = 0; check.Checked += delegate { toggles++; }; check.IsChecked = true;
            Check(toggles == 1, "checkbox handler retained");
            check.IsChecked = null; Check(check.IsChecked == null, "three-state remains available"); check.IsChecked = true;
            int clicks = 0; button.Click += delegate { clicks++; }; button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Check(clicks == 1, "button click handler retained");
            int edits = 0; text.TextChanged += delegate { edits++; }; text.Select(0,2); text.SelectedText = "입력";
            Check(edits > 0 && text.Text.StartsWith("입력"), "text editing and change handler");
            Check(text.Template.FindName("PART_ContentHost", text) is ScrollViewer, "textbox edit host preserved");
            int selections = 0; combo.SelectionChanged += delegate { selections++; }; combo.SelectedIndex = 1; combo.IsDropDownOpen = true; Pump();
            Check(selections == 1 && ((Popup)combo.Template.FindName("PART_Popup", combo)).IsOpen, "combo selection and popup preserved");
            combo.IsDropDownOpen = false;
            combo.IsEditable = true; Pump();
            Check(((TextBox)combo.Template.FindName("PART_EditableTextBox", combo)).IsVisible, "editable combo host preserved");
            combo.Text = "직접 입력"; Check(combo.Text == "직접 입력", "editable combo value"); combo.IsEditable = false; combo.SelectedIndex = 1;
            int changes = 0; slider.ValueChanged += delegate { changes++; };
            Slider.IncreaseLarge.Execute(null, slider);
            Check(slider.Value == 10 && changes > 0, "slider routed keyboard/page command");
            Track track = (Track)slider.Template.FindName("PART_Track", slider);
            Check(track != null && track.Thumb != null && track.Maximum == 20 && track.Value == slider.Value, "slider track range/value binding");
            track.Thumb.RaiseEvent(new DragDeltaEventArgs(40,0)); Check(slider.Value > 10, "slider drag handler retained");
            tabs.SelectedIndex = 1; Pump();
            Check(((ContentPresenter)tabs.Template.FindName("PART_SelectedContentHost", tabs)).Content == ((TabItem)tabs.Items[1]).Content, "tab selection content retained");
            tabs.TabStripPlacement = Dock.Left; Pump(); Check(tabs.ActualWidth > 0, "left tab layout");
            tabs.TabStripPlacement = Dock.Top; tabs.SelectedIndex = 0; Pump();
            scroll.ScrollToVerticalOffset(100); scroll.ScrollToHorizontalOffset(100); Pump();
            Check(scroll.VerticalOffset > 0 && scroll.HorizontalOffset > 0, "both scroll directions retained");
            foreach (ScrollBar bar in All<ScrollBar>(scroll)) {
                Check(bar.Style != null && bar.Template.FindName("PART_Track", bar) is Track, "scrollbar template/track");
            }
            scroll.ScrollToTop(); scroll.ScrollToLeftEnd(); Pump(); win.UpdateLayout();
            RenderTargetBitmap bitmap = new RenderTargetBitmap((int)win.ActualWidth, (int)win.ActualHeight, 96,96,PixelFormats.Pbgra32); bitmap.Render(win);
            PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (MemoryStream bytes = new MemoryStream()) { encoder.Save(bytes); File.WriteAllBytes("tests/bin/controls-theme.png", bytes.ToArray()); Console.WriteLine("CONTROLS_PREVIEW_BASE64=" + Convert.ToBase64String(bytes.ToArray())); }
        } finally { win.Close(); }
        Console.WriteLine("PASS: shared templates, checkbox/button/text/selection events, slider drag/commands, tab content and scroll axes");
    }
}
