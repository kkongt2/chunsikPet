using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace ChunsikDesktop {
    public sealed class UndoToast : Window {
        static UndoToast current;
        public static void ShowCompleted(string text, Action undo) {
            if (current != null) current.Close();
            UndoToast win = new UndoToast { Width = 360, Height = 115, WindowStyle = WindowStyle.ToolWindow, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Topmost = true, Title = "할 일 완료", Background = Brushes.LemonChiffon };
            current = win; StackPanel panel = new StackPanel { Margin = new Thickness(12) }; win.Content = panel;
            panel.Children.Add(new TextBlock { Text = "완료했어요 · " + text, TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 14 });
            Button button = new Button { Content = "↶ 되돌리기 (12초)", Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(0, 8, 0, 0) }; panel.Children.Add(button);
            DateTime until = DateTime.UtcNow.AddSeconds(12); DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            timer.Tick += delegate { int left = (int)Math.Ceiling((until - DateTime.UtcNow).TotalSeconds); if (left <= 0) { win.Close(); return; } button.Content = "↶ 되돌리기 (" + left + "초)"; };
            button.Click += delegate { win.Close(); undo(); };
            win.Loaded += delegate { Rect a = SystemParameters.WorkArea; win.Left = a.Right - win.Width - 20; win.Top = a.Bottom - win.Height - 20; };
            win.Closed += delegate { timer.Stop(); if (current == win) current = null; }; timer.Start(); win.Show();
        }
    }
}
