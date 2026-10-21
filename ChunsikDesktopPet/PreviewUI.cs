using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace ChunsikDesktop {
    public static class PreviewUI {
        static Window window; static Action refresh;
        public static void Refresh() { if (refresh != null) refresh(); }
        public static void Reminder(Window owner, Reminder item, Func<int> seconds) {
            Show(owner, delegate { return item.GetMessages().ToArray(); }, delegate { return item.BubbleColor; }, delegate { return item.Kind; }, seconds, false);
        }
        public static void Todo(Window owner, TodoItem item) {
            Show(owner, delegate { return new string[] { item.Text }; }, delegate { return item.Color; }, delegate { return ""; }, delegate { return item.DisplaySeconds; }, true, delegate { return item.FontSize; });
        }
        static void Show(Window owner, Func<string[]> messages, Func<string> color, Func<string> kind, Func<int> seconds, bool postit, Func<int> fontSize = null) {
            owner = SettingsHub.OwnerFor(owner);
            if (window != null) window.Close();
            Window win = new Window { Title = "설정 미리보기 · 실제 알림 기록에 남지 않아요", Width = 370, Height = 380, Owner = owner, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, Background = Brushes.WhiteSmoke };
            window = win; StackPanel panel = new StackPanel { Margin = new Thickness(22) }; win.Content = panel;
            TextBlock text = new TextBlock { FontSize = 17, TextWrapping = TextWrapping.Wrap, MaxHeight = 100 };
            Border bubble = new Border { CornerRadius = new CornerRadius(postit ? 2 : 18), Padding = new Thickness(18), Child = text }; panel.Children.Add(bubble);
            Image image = new Image { Height = 120, Margin = new Thickness(0, 8, 0, 8), Stretch = Stretch.Uniform }; RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality); panel.Children.Add(image);
            TextBlock status = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.SlateGray }; panel.Children.Add(status);
            Button close = new Button { Content = "미리보기 닫기", Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 12, 0, 0) }; close.Click += delegate { win.Close(); }; panel.Children.Add(close);
            Stopwatch clock = Stopwatch.StartNew(); DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
            refresh = delegate { bubble.Background = BubblePalette.BrushFor(color()); };
            Action draw = delegate {
                if (fontSize != null) text.FontSize = fontSize();
                string[] list = messages(); double t = clock.Elapsed.TotalSeconds; int duration = Math.Max(1, seconds());
                text.Text = list.Length == 0 ? "메시지를 입력해 주세요" : list[((int)t / duration) % list.Length];
                image.Source = PetWindow.PreviewPose(kind(), (int)(t * 5));
                bool visible = !postit || t % (duration + 2) < duration; bubble.Opacity = visible ? 1 : 0;
                status.Text = postit ? duration + "초 표시 · 2초 숨김으로 반복 미리보기" : "메시지 " + list.Length + "개 · " + duration + "초마다 전환\n색상·문구 수정 시 바로 반영돼요.";
                Refresh();
            };
            timer.Tick += delegate { draw(); }; win.Closed += delegate { timer.Stop(); clock.Stop(); if (window == win) { window = null; refresh = null; } };
            draw(); timer.Start(); win.Show();
        }
    }
}
