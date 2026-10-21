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
            if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
            window.Show(); window.Activate();
        }
        static void Ensure() {
            if (window != null) return;
            building = true;
            window = new Window { Title = "춘식이 설정", Width = 1000, Height = 760, MinWidth = 620, MinHeight = 480, WindowStartupLocation = WindowStartupLocation.CenterScreen, Background = Theme.Surface };
            tabs = new TabControl { Margin = new Thickness(16), TabStripPlacement = Dock.Left, Background = Theme.Surface, BorderThickness = new Thickness(0) };
            tabs.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='TabControl'><Grid><Grid.ColumnDefinitions><ColumnDefinition Width='155'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions><ItemsPresenter Margin='0,10,12,0'/><Border Grid.Column='1' Background='White' CornerRadius='16' BorderBrush='#D2E1F1' BorderThickness='1'><ContentPresenter ContentSource='SelectedContent'/></Border></Grid></ControlTemplate>");
            tabs.ItemsPanel = (ItemsPanelTemplate)System.Windows.Markup.XamlReader.Parse(@"<ItemsPanelTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><StackPanel/></ItemsPanelTemplate>");
            Style navigation = (Style)System.Windows.Markup.XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='TabItem'><Setter Property='Foreground' Value='#546578'/><Setter Property='FontSize' Value='14'/><Setter Property='Margin' Value='0,0,0,8'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='TabItem'><Border x:Name='card' Background='Transparent' CornerRadius='11' Padding='14,14'><ContentPresenter ContentSource='Header'/></Border><ControlTemplate.Triggers><Trigger Property='IsSelected' Value='True'><Setter TargetName='card' Property='Background' Value='#0064A8'/><Setter Property='Foreground' Value='White'/><Setter Property='FontWeight' Value='SemiBold'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='card' Property='BorderBrush' Value='#EAAE38'/><Setter TargetName='card' Property='BorderThickness' Value='2'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>");
            tabs.ItemContainerStyle = navigation; window.Content = tabs;
            foreach (string name in new string[] { "캐릭터", "알림", "포스트잇", "타이머·스톱워치", "프로그램" }) {
                TabItem tab = new TabItem { Header = name, Padding = new Thickness(18, 10, 18, 10) }; sections[name] = tab; tabs.Items.Add(tab);
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
            FrameworkElement element = content as FrameworkElement; if (element != null && name == "알림") element.MinWidth = 0;
            tab.Content = new ScrollViewer { Content = content, HorizontalScrollBarVisibility = name == "알림" ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled }; tab.Tag = form; forms[form] = flush;
            form.Closed += delegate { forms.Remove(form); if (tab.Tag == form) { tab.Tag = null; tab.Content = null; } };
            tabs.SelectedItem = tab; window.Activate();
        }
    }
}
