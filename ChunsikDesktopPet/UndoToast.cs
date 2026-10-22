using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
namespace ChunsikDesktop {
    public sealed class UndoToast : Window {
        static UndoToast current;
        readonly Button undoButton = new Button();
        public static void ShowCompleted(string text, Action undo) {
            if (current != null) current.Close();
            UndoToast win = new UndoToast {
                Width = 380, SizeToContent = SizeToContent.Height, WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize, AllowsTransparency = true, Background = Brushes.Transparent,
                ShowInTaskbar = false, ShowActivated = false, Topmost = true, Title = "할 일 완료",
                UseLayoutRounding = true, SnapsToDevicePixels = true
            };
            current = win;
            Border card = new Border {
                Margin = new Thickness(10), Padding = new Thickness(18, 16, 18, 16), CornerRadius = new CornerRadius(18),
                Background = new SolidColorBrush(Color.FromRgb(255, 253, 245)), BorderBrush = new SolidColorBrush(Color.FromRgb(232, 221, 190)), BorderThickness = new Thickness(1),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 12, ShadowDepth = 3, Opacity = 0.18 }
            }; win.Content = card;
            StackPanel panel = new StackPanel(); card.Child = panel;
            DockPanel heading = new DockPanel(); panel.Children.Add(heading);
            Button close = new Button { Content = "×", Width = 26, Height = 26, FontSize = 18, Background = Brushes.Transparent, Foreground = Brushes.SlateGray, BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand, ToolTip = "알림 닫기" };
            close.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border Background='{TemplateBinding Background}' CornerRadius='13'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border></ControlTemplate>");
            close.Click += delegate { win.Close(); }; DockPanel.SetDock(close, Dock.Right); heading.Children.Add(close);
            heading.Children.Add(new TextBlock { Text = "✓  할 일을 완료했어요", FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(42, 113, 84)), VerticalAlignment = VerticalAlignment.Center });
            panel.Children.Add(new TextBlock { Text = String.IsNullOrWhiteSpace(text) ? "빈 메모" : text.Replace('\r', ' ').Replace('\n', ' '), ToolTip = text, FontSize = 13, Foreground = Brushes.DarkSlateGray, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 7, 0, 14) });
            Grid actions = new Grid(); actions.ColumnDefinitions.Add(new ColumnDefinition()); actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); panel.Children.Add(actions);
            TextBlock remaining = new TextBlock { Text = "12초 안에 되돌릴 수 있어요", FontSize = 11, Foreground = Brushes.SlateGray, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) }; actions.Children.Add(remaining);
            Button button = win.undoButton;
            button.Content = "↶  되돌리기"; button.MinHeight = 38; button.MinWidth = 118; button.Padding = new Thickness(16, 9, 16, 9);
            button.FontSize = 13; button.FontWeight = FontWeights.SemiBold; button.Foreground = Brushes.White;
            button.Background = new SolidColorBrush(Color.FromRgb(37, 108, 152)); button.Cursor = System.Windows.Input.Cursors.Hand;
            button.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'><Border x:Name='b' CornerRadius='11' Background='{TemplateBinding Background}' BorderBrush='Transparent' BorderThickness='2' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Background' Value='#3586B6'/></Trigger><Trigger Property='IsPressed' Value='True'><Setter TargetName='b' Property='Background' Value='#18557C'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='b' Property='BorderBrush' Value='#F5BC56'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
            System.Windows.Automation.AutomationProperties.SetName(button, "완료한 할 일 되돌리기");
            Grid.SetColumn(button, 1); actions.Children.Add(button);
            ProgressBar progress = new ProgressBar { Minimum = 0, Maximum = 12, Value = 12, Height = 3, BorderThickness = new Thickness(0), Foreground = new SolidColorBrush(Color.FromRgb(92, 162, 189)), Background = new SolidColorBrush(Color.FromRgb(234, 234, 224)), Margin = new Thickness(0, 14, 0, 0), IsHitTestVisible = false }; panel.Children.Add(progress);
            Stopwatch elapsed = Stopwatch.StartNew();
            DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            timer.Tick += delegate {
                double left = Math.Max(0, 12 - elapsed.Elapsed.TotalSeconds);
                if (left <= 0) { win.Close(); return; }
                remaining.Text = (int)Math.Ceiling(left) + "초 안에 되돌릴 수 있어요"; progress.Value = left;
            };
            button.Click += delegate { win.Close(); undo(); };
            win.Loaded += delegate {
                Rect area = ScreenLayout.AreaFor(win);
                win.Width = Math.Min(380, Math.Max(220, area.Width - 24)); win.UpdateLayout();
                win.Left = Math.Max(area.Left, area.Right - win.ActualWidth - 12);
                win.Top = Math.Max(area.Top, area.Bottom - win.ActualHeight - 12);
                win.BeginAnimation(Window.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
            };
            win.Closed += delegate { timer.Stop(); elapsed.Stop(); if (current == win) current = null; };
            win.Show(); timer.Start();
        }
    }
}
