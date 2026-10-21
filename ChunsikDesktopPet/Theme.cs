using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace ChunsikDesktop {
    // Templates are parsed once on the application's UI thread and reused by every settings page.
    public static class Theme {
        public static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(0, 100, 168));
        public static readonly Brush Surface = new SolidColorBrush(Color.FromRgb(244, 248, 253));
        public static readonly Brush Line = new SolidColorBrush(Color.FromRgb(210, 225, 241));
        public static readonly Style DayStyle = (Style)System.Windows.Markup.XamlReader.Parse(@"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
       xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type ToggleButton}'>
  <Setter Property='Width' Value='24'/><Setter Property='Height' Value='24'/>
  <Setter Property='Margin' Value='2,0,2,0'/><Setter Property='FontSize' Value='11'/>
  <Setter Property='FontWeight' Value='SemiBold'/><Setter Property='Cursor' Value='Hand'/>
  <Setter Property='Background' Value='#EDF1F5'/><Setter Property='Foreground' Value='#687586'/>
  <Setter Property='BorderBrush' Value='#DCE3EB'/><Setter Property='BorderThickness' Value='1'/>
  <Setter Property='Template'><Setter.Value>
    <ControlTemplate TargetType='{x:Type ToggleButton}'>
      <Border CornerRadius='12' Background='{TemplateBinding Background}'
              BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}'>
        <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
      </Border>
    </ControlTemplate>
  </Setter.Value></Setter>
  <Style.Triggers>
    <Trigger Property='IsChecked' Value='True'>
      <Setter Property='Background' Value='#0064A8'/><Setter Property='Foreground' Value='White'/>
      <Setter Property='BorderBrush' Value='#0064A8'/>
    </Trigger>
    <Trigger Property='IsMouseOver' Value='True'><Setter Property='Opacity' Value='0.82'/></Trigger>
    <Trigger Property='IsPressed' Value='True'><Setter Property='Opacity' Value='0.65'/></Trigger>
    <Trigger Property='IsKeyboardFocused' Value='True'>
      <Setter Property='BorderBrush' Value='#F6B73C'/><Setter Property='BorderThickness' Value='2'/>
    </Trigger>
  </Style.Triggers>
</Style>");
        public static readonly Style SegmentStyle = (Style)System.Windows.Markup.XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='RadioButton'><Setter Property='Padding' Value='8,7'/><Setter Property='FontSize' Value='11'/><Setter Property='Foreground' Value='#546578'/><Setter Property='Background' Value='Transparent'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='RadioButton'><Border CornerRadius='9' Background='{TemplateBinding Background}' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center'/></Border></ControlTemplate></Setter.Value></Setter><Style.Triggers><Trigger Property='IsChecked' Value='True'><Setter Property='Background' Value='#0064A8'/><Setter Property='Foreground' Value='White'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter Property='Opacity' Value='0.7'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.6'/></Trigger></Style.Triggers></Style>");
        public static readonly ControlTemplate ButtonTemplate = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'><Border x:Name='b' CornerRadius='10' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Opacity' Value='0.75'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='b' Property='BorderBrush' Value='#0064A8'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.4'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
        public static readonly Style ChoiceStyle = (Style)System.Windows.Markup.XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='RadioButton'><Setter Property='Padding' Value='15,8'/><Setter Property='Margin' Value='2'/><Setter Property='Foreground' Value='#52637A'/><Setter Property='Background' Value='Transparent'/><Setter Property='FontSize' Value='12'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='RadioButton'><Border x:Name='b' CornerRadius='9' Background='{TemplateBinding Background}' Padding='{TemplateBinding Padding}' BorderBrush='Transparent' BorderThickness='1'><ContentPresenter HorizontalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='b' Property='BorderBrush' Value='#EAAE38'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter><Style.Triggers><Trigger Property='IsChecked' Value='True'><Setter Property='Background' Value='#0064A8'/><Setter Property='Foreground' Value='White'/><Setter Property='FontWeight' Value='SemiBold'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter Property='Opacity' Value='0.8'/></Trigger></Style.Triggers></Style>");
        public static readonly ControlTemplate SwitchTemplate = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ToggleButton'><Border x:Name='track' Background='#CCD5DF' CornerRadius='12'><Ellipse x:Name='thumb' Width='18' Height='18' Fill='White' HorizontalAlignment='Left' Margin='3'/></Border><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='track' Property='Background' Value='#0064A8'/><Setter TargetName='thumb' Property='HorizontalAlignment' Value='Right'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='track' Property='BorderBrush' Value='#F6B73C'/><Setter TargetName='track' Property='BorderThickness' Value='2'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
        public static readonly ControlTemplate ToggleTemplate = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ToggleButton'><Border x:Name='box' CornerRadius='10' Background='#E4EAF1' Padding='{TemplateBinding Padding}'><DockPanel><TextBlock x:Name='state' Text='OFF' FontWeight='Bold' Margin='0,0,12,0'/><ContentPresenter/></DockPanel></Border><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='box' Property='Background' Value='#CDEBDE'/><Setter TargetName='state' Property='Text' Value='ON'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='box' Property='BorderBrush' Value='#0064A8'/><Setter TargetName='box' Property='BorderThickness' Value='1'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
        public static void Install(Application app) {
            Style button = new Style(typeof(Button));
            button.Setters.Add(new Setter(Control.TemplateProperty, ButtonTemplate));
            button.Setters.Add(new Setter(Control.BackgroundProperty, Surface));
            button.Setters.Add(new Setter(Control.BorderBrushProperty, Line));
            button.Setters.Add(new Setter(Control.ForegroundProperty, Accent));
            button.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12, 7, 12, 7)));
            button.Setters.Add(new Setter(FrameworkElement.CursorProperty, System.Windows.Input.Cursors.Hand));
            app.Resources[typeof(Button)] = button;
        }
        public static StackPanel Card(Panel parent, string title) {
            StackPanel panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = title, FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = Accent, Margin = new Thickness(0, 0, 0, 14) });
            parent.Children.Add(new Border { Child = panel, Background = Brushes.White, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(18), Margin = new Thickness(0, 0, 0, 14) });
            return panel;
        }
    }
}
