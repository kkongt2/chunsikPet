using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace ChunsikDesktop {
    public static class SettingsHub {
        static Window window; static TabControl tabs; static bool closing, building;
        static readonly Dictionary<string, TabItem> sections = new Dictionary<string, TabItem>();
        static readonly Dictionary<Window, Func<bool>> forms = new Dictionary<Window, Func<bool>>();
        public static Action About;
        public static Window OwnerFor(Window original) { return forms.ContainsKey(original) ? window : original; }
        public static void Close() { if (window != null) window.Close(); }
        static Action OpenSection(string name) {
            if (name == "캐릭터") return PetPreferences.Current.OpenSettings;
            if (name == "알림") return ReminderService.Current.OpenSettings;
            if (name == "포스트잇") return delegate { TodoService.Current.OpenSettings(null); };
            return About;
        }
        public static void Open(string name) {
            Ensure(); tabs.SelectedItem = sections[name];
            if (name != "타이머·스톱워치" && sections[name].Tag == null) { Action open = OpenSection(name); if (open != null) open(); }
            window.Show(); window.Activate();
        }
        static void Ensure() {
            if (window != null) return;
            building = true;
            window = new Window { Title = "춘식이 설정", Width = 1120, Height = 800, MinWidth = 800, MinHeight = 480, WindowStartupLocation = WindowStartupLocation.CenterScreen, Background = SettingsTheme.Canvas };
            tabs = new TabControl { Margin = new Thickness(12), TabStripPlacement = Dock.Left, Background = SettingsTheme.Canvas, BorderThickness = new Thickness(0) };
            tabs.ItemContainerStyle = (Style)System.Windows.Markup.XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='TabItem'><Setter Property='Width' Value='148'/><Setter Property='Margin' Value='0,0,10,6'/><Setter Property='FontSize' Value='14'/><Setter Property='Foreground' Value='#66758A'/><Setter Property='Background' Value='Transparent'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='TabItem'><Border x:Name='nav' Background='{TemplateBinding Background}' CornerRadius='8' Padding='16,13'><ContentPresenter ContentSource='Header' HorizontalAlignment='Left'/></Border><ControlTemplate.Triggers><Trigger Property='IsSelected' Value='True'><Setter Property='Foreground' Value='#264FA8'/><Setter Property='FontWeight' Value='SemiBold'/><Setter TargetName='nav' Property='Background' Value='#E6EDFC'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='nav' Property='Opacity' Value='0.8'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='nav' Property='BorderBrush' Value='#3568D4'/><Setter TargetName='nav' Property='BorderThickness' Value='1'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>");
            window.Content = tabs;
            foreach (string name in new string[] { "캐릭터", "알림", "포스트잇", "타이머·스톱워치", "프로그램" }) {
                TabItem tab = new TabItem { Header = name, Padding = new Thickness(18, 10, 18, 10) }; sections[name] = tab; tabs.Items.Add(tab);
                string captured = name; Button load = new Button { Content = name + " 설정 열기", Padding = new Thickness(20), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }; load.Click += delegate { Open(captured); }; tab.Content = load;
            }
            StackPanel tools = new StackPanel { Margin = new Thickness(28), MaxWidth = 600, HorizontalAlignment = HorizontalAlignment.Left };
            tools.Children.Add(new TextBlock { Text = "시간을 춘식이에게 맡겨요", FontSize = 24, Margin = new Thickness(0, 0, 0, 20) });
            foreach (bool timer in new bool[] { true, false }) { bool countdown = timer; Button button = new Button { Content = timer ? "⏱ 타이머 · 전담 춘식이 열기" : "⏲ 스톱워치 · 전담 춘식이 열기", Padding = new Thickness(20), Margin = new Thickness(0, 0, 0, 12) }; button.Click += delegate { if (countdown) TimeToolWindow.OpenTimer(); else TimeToolWindow.OpenStopwatch(); }; tools.Children.Add(button); }
            tools.Children.Add(new TextBlock { Text = "측정 창은 바탕화면에서 독립적으로 동작합니다.", Foreground = Brushes.SlateGray }); sections["타이머·스톱워치"].Content = tools;
            tabs.SelectionChanged += delegate(object sender, SelectionChangedEventArgs e) { if (building || e.Source != tabs) return; TabItem tab = tabs.SelectedItem as TabItem; if (tab != null && tab.Tag == null && (string)tab.Header != "타이머·스톱워치") { Action open = OpenSection((string)tab.Header); if (open != null) open(); } };
            window.Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) {
                if (closing) return;
                foreach (Func<bool> flush in new List<Func<bool>>(forms.Values)) if (flush != null && !flush()) { e.Cancel = true; return; }
                closing = true; foreach (Window form in new List<Window>(forms.Keys)) form.Close();
            };
            window.Closed += delegate { forms.Clear(); sections.Clear(); window = null; tabs = null; closing = false; }; window.Show(); building = false;
        }
        public static void Attach(Window form, string name, Func<bool> flush) {
            Ensure(); TabItem tab = sections[name]; object content = form.Content; form.Content = null;
            FrameworkElement element = content as FrameworkElement; if (element != null && name == "알림") element.MinWidth = 1240;
            tab.Content = new ScrollViewer { Content = content, HorizontalScrollBarVisibility = name == "포스트잇" ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled }; tab.Tag = form; forms[form] = flush;
            form.Closed += delegate { forms.Remove(form); if (tab.Tag == form) { tab.Tag = null; Button reopen = new Button { Content = name + " 설정 다시 열기", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(20) }; reopen.Click += delegate { Open(name); }; tab.Content = reopen; } };
            tabs.SelectedItem = tab; window.Activate();
        }
    }
}

