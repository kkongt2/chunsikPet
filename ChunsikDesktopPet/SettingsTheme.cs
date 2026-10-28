using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Markup;
namespace ChunsikDesktop {
    internal static class SettingsTheme {
        static readonly ResourceDictionary Controls = LoadControls();
        static ResourceDictionary LoadControls() {
            using (System.IO.Stream stream = typeof(SettingsTheme).Assembly.GetManifestResourceStream("Theme.Controls.xaml"))
                return (ResourceDictionary)XamlReader.Load(stream);
        }
        public static void Install(Application app) {
            if (!app.Resources.MergedDictionaries.Contains(Controls)) app.Resources.MergedDictionaries.Add(Controls);
        }
        public static ControlTemplate SharedButtonTemplate { get { return (ControlTemplate)Controls["SoftButtonTemplate"]; } }
        public static readonly Brush Ink = Color("#253247"), Muted = Color("#66758A"), Accent = Color("#3568D4"), Line = Color("#DFE5EE"), Canvas = Color("#F5F7FA");
        static Brush Color(string value) { SolidColorBrush brush = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString(value)); brush.Freeze(); return brush; }
        public static readonly ControlTemplate ButtonTemplate = (ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'><Border x:Name='box' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' CornerRadius='7' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='box' Property='Opacity' Value='0.8'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='box' Property='BorderBrush' Value='#3568D4'/><Setter TargetName='box' Property='BorderThickness' Value='2'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.4'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
        public static Button Button(string text, Action action, bool primary = false) {
            Button button = new Button { Content = text, FontSize = 13, MinHeight = 34, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 6, 0),
                Template = ButtonTemplate, Background = primary ? Accent : Brushes.White, Foreground = primary ? Brushes.White : Ink,
                BorderBrush = primary ? Accent : Line, Cursor = System.Windows.Input.Cursors.Hand };
            button.Click += delegate { action(); }; return button;
        }
        public static TextBlock Text(string text, double size = 13) {
            return new TextBlock { Text = text, FontSize = size, Foreground = Muted, TextWrapping = TextWrapping.Wrap };
        }
        public static Border Card(UIElement child) {
            return new Border { Background = Brushes.White, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(16), Margin = new Thickness(0, 0, 0, 12), Child = child };
        }
        public static StackPanel Field(string label, UIElement control) {
            StackPanel field = new StackPanel { Width = 220, Margin = new Thickness(0, 0, 18, 16) };
            TextBlock heading = Text(label); heading.FontWeight = FontWeights.SemiBold; heading.Foreground = Ink; heading.Margin = new Thickness(0, 0, 0, 8);
            field.Children.Add(heading); field.Children.Add(control); return field;
        }
        public static void Apply(FrameworkElement root) {
            // Post-it settings retain their v1.59 appearance, including template-created scrollbars/popups.
            foreach (Type type in new[] { typeof(ComboBox), typeof(ComboBoxItem), typeof(CheckBox), typeof(Slider), typeof(System.Windows.Controls.Primitives.ScrollBar), typeof(TabControl), typeof(TabItem) })
                root.Resources[type] = new Style(type);
            Style text = new Style(typeof(TextBox));
            text.Setters.Add(new Setter(Control.FontSizeProperty, 14.0));
            text.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 8, 10, 8)));
            text.Setters.Add(new Setter(Control.BorderBrushProperty, Line));
            text.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            text.Setters.Add(new Setter(Control.ForegroundProperty, Ink));
            root.Resources[typeof(TextBox)] = text;
            Style button = new Style(typeof(Button));
            button.Setters.Add(new Setter(Control.TemplateProperty, ButtonTemplate));
            button.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.White));
            button.Setters.Add(new Setter(Control.ForegroundProperty, Ink));
            button.Setters.Add(new Setter(Control.BorderBrushProperty, Line));
            root.Resources[typeof(Button)] = button;
        }
    }
}
