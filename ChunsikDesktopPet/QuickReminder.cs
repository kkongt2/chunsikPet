using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ChunsikDesktop {
    public sealed partial class ReminderService {
        Window quickWindow;
        Action<Reminder, bool> quickListChanged;

        bool SaveQuickReminder(Reminder item, bool add, TextBlock status) {
            List<Reminder> items = new List<Reminder>(settings.Items);
            if (add) items.Add(item);
            else items.RemoveAll(delegate(Reminder r) { return r.Id == item.Id; });
            ReminderSettings next = new ReminderSettings {
                Items = items, Sound = settings.Sound, MessageSeconds = settings.MessageSeconds, SnoozeMinutes = settings.SnoozeMinutes
            };
            try {
                if (SettingsBackup.Restoring) throw new InvalidOperationException("설정 복원이 진행 중입니다.");
                Write(next);
            } catch (Exception ex) {
                status.Text = "저장 실패 · " + ex.Message; return false;
            }
            settings = next;
            if (quickListChanged != null) quickListChanged(item, add);
            return true;
        }

        public void OpenQuickReminder() {
            if (stopping) return;
            if (quickWindow != null) { quickWindow.Activate(); return; }
            Window win = new Window { Title = "빠른 알림", Width = 380, SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = SettingsTheme.Canvas, Topmost = true };
            quickWindow = win;
            StackPanel panel = new StackPanel { Margin = new Thickness(18) }; win.Content = panel;
            TextBox message = new TextBox { Margin = new Thickness(0, 4, 0, 10), Padding = new Thickness(6) };
            panel.Children.Add(new Label { Content = "알림 내용", Target = message }); panel.Children.Add(message);
            WrapPanel presets = new WrapPanel(); panel.Children.Add(presets);
            TextBlock status = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Firebrick };
            DateTime initial = DateTime.Now.AddHours(1);
            DatePicker date = new DatePicker { SelectedDate = initial.Date };
            NumberField hour = new NumberField(initial.Hour, 23, "시"), minute = new NumberField(initial.Minute, 59, "분");
            CheckBox repeat = new CheckBox { Content = "요일 반복", Margin = new Thickness(0, 8, 0, 4) };
            Reminder weekly = new Reminder { Days = 127 };
            WeekdayPicker days = new WeekdayPicker { DataContext = weekly, Visibility = Visibility.Collapsed };
            repeat.Checked += delegate { days.Visibility = Visibility.Visible; date.IsEnabled = false; };
            repeat.Unchecked += delegate { days.Visibility = Visibility.Collapsed; date.IsEnabled = true; };
            DispatcherTimer dismiss = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
            dismiss.Tick += delegate { win.Close(); };
            Action<DateTime, bool> submit = delegate(DateTime due, bool recurring) {
                if (String.IsNullOrWhiteSpace(message.Text)) { status.Text = "알림 내용을 입력해 주세요."; message.Focus(); return; }
                DateTime now = DateTime.Now;
                if (recurring) {
                    if (weekly.SelectedDays == 0) { status.Text = "반복할 요일을 선택해 주세요."; return; }
                    due = now.Date.Add(due.TimeOfDay);
                    while (due <= now || !weekly.HasDay((int)due.DayOfWeek)) due = due.AddDays(1);
                } else if (due <= now) { status.Text = "현재보다 늦은 시각을 선택해 주세요."; return; }
                Reminder item = new Reminder { Title = message.Text.Trim(), ScheduleType = recurring ? "time" : "date",
                    Days = weekly.SelectedDays, Date = due.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    HasTime = true, Time = due.ToString("HH:mm", CultureInfo.InvariantCulture) };
                if (!SaveQuickReminder(item, true, status)) return;
                panel.Children.Clear(); status.Foreground = SettingsTheme.Ink;
                status.Text = due.ToString("M월 d일 HH:mm") + "에 알려드릴게요." + (recurring ? " (요일 반복)" : "");
                panel.Children.Add(status);
                panel.Children.Add(Pill("등록 취소", delegate {
                    dismiss.Stop();
                    if (SaveQuickReminder(item, false, status)) win.Close();
                }));
                dismiss.Start();
            };
            foreach (int delay in new[] { 5, 10, 30, 60 }) {
                int minutes = delay;
                presets.Children.Add(Pill(minutes == 60 ? "1시간 후" : minutes + "분 후", delegate {
                    DateTime due = DateTime.Now.AddMinutes(minutes);
                    submit(due.Date.AddHours(due.Hour).AddMinutes(due.Minute), false);
                }));
            }
            StackPanel details = new StackPanel { Margin = new Thickness(4) };
            details.Children.Add(date);
            StackPanel clock = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            clock.Children.Add(hour); clock.Children.Add(new Label { Content = "시" });
            clock.Children.Add(minute); clock.Children.Add(new Label { Content = "분" });
            details.Children.Add(clock); details.Children.Add(repeat); details.Children.Add(days);
            details.Children.Add(Pill("예약", delegate {
                if (repeat.IsChecked != true && !date.SelectedDate.HasValue) { status.Text = "날짜를 선택해 주세요."; return; }
                submit((date.SelectedDate ?? DateTime.Today).Date.AddHours(hour.Value).AddMinutes(minute.Value), repeat.IsChecked == true);
            }));
            panel.Children.Add(new Expander { Header = "날짜·시간 지정 / 반복 설정", Content = details, Margin = new Thickness(0, 8, 0, 0) });
            panel.Children.Add(status);
            win.ContentRendered += delegate { message.Focus(); };
            win.PreviewKeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) { e.Handled = true; win.Close(); } };
            win.Closed += delegate { dismiss.Stop(); quickWindow = null; };
            win.Show(); win.Activate();
        }
    }
}
