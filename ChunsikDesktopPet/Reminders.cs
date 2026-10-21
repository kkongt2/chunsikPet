using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Serialization;

namespace ChunsikDesktop {
    public sealed class NumberField : Border {
        readonly TextBox input;
        readonly int maximum;
        string previous;
        bool restoring;
        public event Action Changed;
        public int Value { get { int n; return Int32.TryParse(input.Text, out n) ? n : 0; } }
        bool Valid(string text) {
            if (text.Length == 0) return true;
            if (text.Length > maximum.ToString().Length) return false;
            foreach (char c in text) if (c < '0' || c > '9') return false;
            int value; return Int32.TryParse(text, out value) && value <= maximum;
        }
        string Candidate(string insert) { return input.Text.Remove(input.SelectionStart, input.SelectionLength).Insert(input.SelectionStart, insert); }
        public NumberField(int value, int max, string name) {
            maximum = max; CornerRadius = new CornerRadius(9); Background = Brushes.White; BorderBrush = new SolidColorBrush(Color.FromRgb(198, 216, 237)); BorderThickness = new Thickness(1); Padding = new Thickness(3); Width = maximum >= 100 ? 90 : 62;
            Grid grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) }); Child = grid;
            input = new TextBox { Text = value.ToString("00"), BorderThickness = new Thickness(0), FontSize = 18, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(0) }; previous = input.Text;
            System.Windows.Automation.AutomationProperties.SetName(input, name + " 0~" + maximum);
            System.Windows.Input.InputMethod.SetIsInputMethodEnabled(input, false);
            grid.Children.Add(input);
            input.PreviewTextInput += delegate(object sender, System.Windows.Input.TextCompositionEventArgs e) { e.Handled = !Valid(Candidate(e.Text)); };
            DataObject.AddPastingHandler(input, delegate(object sender, DataObjectPastingEventArgs e) { if (!e.DataObject.GetDataPresent(DataFormats.UnicodeText) || !Valid(Candidate((string)e.DataObject.GetData(DataFormats.UnicodeText)))) e.CancelCommand(); });
            input.TextChanged += delegate {
                if (restoring) return;
                if (!Valid(input.Text)) { restoring = true; input.Text = previous; input.CaretIndex = input.Text.Length; restoring = false; return; }
                previous = input.Text; if (Changed != null) Changed();
            };
            input.GotKeyboardFocus += delegate { input.SelectAll(); };
            input.LostKeyboardFocus += delegate { input.Text = Value.ToString("00"); };
            input.PreviewKeyDown += delegate(object sender, System.Windows.Input.KeyEventArgs e) {
                if (e.Key == System.Windows.Input.Key.Up || e.Key == System.Windows.Input.Key.Down) { Step(e.Key == System.Windows.Input.Key.Up ? 1 : -1); e.Handled = true; }
            };
            StackPanel arrows = new StackPanel(); Grid.SetColumn(arrows, 1); grid.Children.Add(arrows);
            Button up = new Button { Content = "▴", FontSize = 9, Padding = new Thickness(0), Height = 16, BorderThickness = new Thickness(0), Background = Brushes.Transparent, ToolTip = name + " 증가" };
            Button down = new Button { Content = "▾", FontSize = 9, Padding = new Thickness(0), Height = 16, BorderThickness = new Thickness(0), Background = Brushes.Transparent, ToolTip = name + " 감소" };
            up.Click += delegate { Step(1); }; down.Click += delegate { Step(-1); }; arrows.Children.Add(up); arrows.Children.Add(down);
        }
        void Step(int delta) { input.Text = Math.Max(0, Math.Min(maximum, Value + delta)).ToString("00"); }
    }
    // Real toggle controls: mouse, keyboard and screen readers use the same selected state.
    public sealed class WeekdayPicker : StackPanel {
        static readonly Style DayStyle = (Style)System.Windows.Markup.XamlReader.Parse(@"
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
        public WeekdayPicker() {
            Orientation = Orientation.Horizontal; VerticalAlignment = VerticalAlignment.Center;
            Margin = new Thickness(4, 0, 4, 0);
            DataContextChanged += delegate { Build(); };
            Loaded += delegate { Build(); };
        }
        void Build() {
            Children.Clear();
            Reminder item = DataContext as Reminder;
            if (item == null) return;
            string[] names = { "일", "월", "화", "수", "목", "금", "토" };
            for (int i = 0; i < 7; i++) {
                int day = i;
                System.Windows.Controls.Primitives.ToggleButton button = new System.Windows.Controls.Primitives.ToggleButton {
                    Content = names[i], Style = DayStyle, IsChecked = item.HasDay(i),
                    ToolTip = names[i] + "요일 알림 켜기/끄기"
                };
                System.Windows.Automation.AutomationProperties.SetName(button, names[i] + "요일 반복");
                button.Checked += delegate { item.SetDay(day, true); };
                button.Unchecked += delegate { item.SetDay(day, false); };
                Children.Add(button);
            }
        }
    }

    public class ReminderSettings {
        public bool Sound { get; set; }
        public int MessageSeconds { get; set; }
        public int SnoozeMinutes { get; set; }
        public List<Reminder> Items { get; set; }
        public ReminderSettings() { Sound = true; MessageSeconds = 7; SnoozeMinutes = 5; Items = new List<Reminder>(); }
    }
    public sealed class ReminderService {
        public static ReminderService Current;
        public static readonly string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChunsikDesktopPet", "reminders.xml");
        static readonly XmlSerializer Serializer = new XmlSerializer(typeof(ReminderSettings));
        readonly DispatcherTimer timer = new DispatcherTimer();
        readonly Queue<NoticeData> queue = new Queue<NoticeData>();
        readonly List<Snooze> snoozes = new List<Snooze>();
        readonly Dictionary<string, DateTime> intervalDue = new Dictionary<string, DateTime>();
        readonly List<NoticeGroup> active = new List<NoticeGroup>();
        bool collecting, regrouping;
        readonly DateTime sessionStarted = DateTime.Now;
        readonly HashSet<string> dateSeen = new HashSet<string>();
        DateTime lastCheck = DateTime.Now;
        ReminderSettings settings;
        Window editor;
        public bool SettingsOpen { get { return editor != null; } }
        public ReminderSettings Snapshot() { return settings; }
        public string AnimationKindFor(PetWindow pet) {
            foreach (NoticeGroup group in active) if (group.Contains(pet)) return group.Kind;
            return "";
        }
        public bool IsSpeakingFor(PetWindow pet) {
            foreach (NoticeGroup group in active) if (group.Contains(pet)) return true;
            return false;
        }
        public bool HasWaitingNotices { get { return queue.Count > 0; } }
        string Countdown(string id, DateTime now, out string detail) {
            detail = "PC의 로컬 날짜와 시간을 기준으로 계산합니다.";
            Reminder item = settings.Items.Find(delegate(Reminder r) { return r.Id == id; });
            if (item == null) return "저장 후 계산";
            if (!item.Enabled) return "알림 꺼짐";
            foreach (NoticeGroup group in active) if (group.Data.ReminderId == id)
                return PresentationPolicy.IsSuppressed ? "방해 금지 · 표시 일시 중지" : "지금 알림 표시 중";
            foreach (NoticeData pending in queue) if (pending.ReminderId == id)
                return PresentationPolicy.IsSuppressed ? "방해 금지 · 알림 대기" : "앞선 알림 종료 후 표시";
            foreach (Snooze later in snoozes) if (later.Notice.ReminderId == id) {
                detail = "다시 알림: " + later.Due.ToString("yyyy-MM-dd HH:mm:ss");
                return "다시 알림 · " + ReminderRules.Remaining(now, later.Due);
            }
            DateTime interval; DateTime? nextInterval = intervalDue.TryGetValue(id, out interval) ? (DateTime?)interval : null;
            string state;
            DateTime? due = ReminderRules.NextDue(item, now, lastCheck, sessionStarted, dateSeen.Contains(item.Id + ":" + item.Date), nextInterval,
                PetPreferences.Current == null ? 0 : PetPreferences.Current.InputIdleSeconds, out state);
            if (!due.HasValue) return state;
            detail = "다음 예정: " + due.Value.ToString("yyyy-MM-dd (ddd) HH:mm:ss") + " · PC 로컬 시간";
            return (PresentationPolicy.IsSuppressed ? "방해 금지 · 예정 " : "다음 알림 · ") + ReminderRules.Remaining(now, due.Value);
        }
        public List<Rect> VisibleBounds() {
            List<Rect> result = new List<Rect>();
            foreach (NoticeGroup group in active) group.AddBounds(result);
            return result;
        }
        public void LayoutNotices() {
            List<Rect> occupied = new List<Rect>();
            foreach (NoticeGroup group in active) group.AppendLayout(occupied);
        }
        public void RebalanceNotices() {
            if (stopping || regrouping) return;
            regrouping = true;
            List<NoticeData> pending = new List<NoticeData>();
            foreach (NoticeGroup group in active) pending.Add(group.Data);
            pending.AddRange(queue); queue.Clear();
            foreach (NoticeGroup group in new List<NoticeGroup>(active)) group.Close();
            active.Clear(); foreach (NoticeData data in pending) queue.Enqueue(data);
            regrouping = false; ShowNext();
        }
        bool storageWarningShown, stopping;
        bool resumeWarning;
        void SaveSession() {
            if (stopping || regrouping) return;
            AlertState state = AlertStateStore.Current.State;
            state.Waiting = new List<NoticeData>(queue);
            foreach (NoticeGroup group in active) state.Waiting.Add(group.Data);
            state.Snoozed = new List<SnoozedNotice>();
            foreach (Snooze snooze in snoozes) state.Snoozed.Add(new SnoozedNotice { DueUtc = snooze.Due.ToUniversalTime(), Notice = snooze.Notice });
            if (!AlertStateStore.Current.Save() && !resumeWarning) { resumeWarning = true; MessageBox.Show(AlertStateStore.Current.Warning, "알림 재개 정보"); }
        }
        public bool SoundEnabled { get { return settings.Sound; } }
        public bool IsSnoozed(NoticeData data) { foreach (Snooze snooze in snoozes) if (snooze.Notice == data) return true; return false; }
        class Snooze { public DateTime Due; public NoticeData Notice; }

        public ReminderService() {
            settings = Load();
            ResetIntervals(settings, null, DateTime.Now);
            AlertState restored = AlertStateStore.Current.State;
            foreach (NoticeData data in restored.Waiting) queue.Enqueue(data);
            foreach (SnoozedNotice saved in restored.Snoozed) snoozes.Add(new Snooze { Due = saved.DueUtc.ToLocalTime(), Notice = saved.Notice });
            timer.Interval = TimeSpan.FromSeconds(1); timer.Tick += delegate { Check(); }; timer.Start();
        }
        ReminderSettings Defaults() {
            ReminderSettings value = new ReminderSettings();
            value.Items.Add(new Reminder { Kind = "lunch", Title = "점심시간", Time = "12:00", Repeat = "평일", Messages = new List<string> { "점심시간이에요!", "맛있게 드세요." } });
            value.Items.Add(new Reminder { Kind = "home", Title = "퇴근시간", Time = "18:00", Repeat = "평일", Messages = new List<string> { "퇴근시간이에요!", "오늘도 수고했어요." } });
            value.Items.Add(new Reminder { Kind = "water", Title = "물 마시기", ScheduleType = "interval", IntervalMinutes = 60, Repeat = "평일", Messages = new List<string> { "잠깐 쉬면서 물 한 잔 마셔요!" } });
            value.Items.Add(new Reminder { Kind = "stretch", Title = "스트레칭 하기", ScheduleType = "interval", IntervalMinutes = 60, Repeat = "평일", Messages = new List<string> { "잠깐 쉬어가요! 팔을 쭉 펴고 가볍게 스트레칭해요." } });
            return value;
        }
        void ResetIntervals(ReminderSettings next, ReminderSettings old, DateTime now) {
            HashSet<string> keep = new HashSet<string>();
            foreach (Reminder item in next.Items) {
                if (!item.IsInterval || !item.Enabled) continue;
                keep.Add(item.Id); bool same = false;
                if (old != null) foreach (Reminder prior in old.Items)
                    if (prior.Id == item.Id && prior.IsInterval && prior.Enabled && prior.IntervalMinutes == item.IntervalMinutes && prior.SelectedDays == item.SelectedDays && prior.ActivityEnabled == item.ActivityEnabled && prior.ActiveFrom == item.ActiveFrom && prior.ActiveUntil == item.ActiveUntil && prior.PauseWhenIdle == item.PauseWhenIdle) same = true;
                if (!same || !intervalDue.ContainsKey(item.Id)) intervalDue[item.Id] = now.AddMinutes(item.IntervalMinutes);
            }
            foreach (string id in new List<string>(intervalDue.Keys)) if (!keep.Contains(id)) intervalDue.Remove(id);
        }
        void EnsureBuiltins(ReminderSettings value) {
            foreach (Reminder standard in Defaults().Items) {
                Reminder found = null;
                foreach (Reminder item in value.Items) if (item.Kind == standard.Kind) { found = item; break; }
                if (found == null) foreach (Reminder item in value.Items) {
                    if (String.IsNullOrEmpty(item.Kind) && (item.Title ?? "").Replace(" ", "") == standard.Title) {
                        found = item; found.Kind = standard.Kind; break;
                    }
                }
                // Restore a deleted default without changing any existing user's times or messages.
                if (found == null) value.Items.Add(standard);
                else if (standard.Kind == "water" || standard.Kind == "stretch") found.ScheduleType = "interval";
            }
        }
        ReminderSettings Read(string path) {
            using (FileStream stream = File.OpenRead(path)) {
                ReminderSettings result = (ReminderSettings)Serializer.Deserialize(stream);
                if (result == null || result.Items == null) throw new InvalidDataException("Invalid reminder settings.");
                if (result.MessageSeconds < 3 || result.MessageSeconds > 60) result.MessageSeconds = 7;
                if (result.SnoozeMinutes < 1 || result.SnoozeMinutes > 1440) result.SnoozeMinutes = 5;
                foreach (Reminder item in result.Items) {
                    TimeSpan time;
                    if (item == null || !ReminderRules.TryTime(item.Time, out time) ||
                        (item.Days < -1 || item.Days > 127 || (item.Days == -1 && item.Repeat != "매일" && item.Repeat != "평일" && item.Repeat != "주말")))
                        throw new InvalidDataException("Invalid reminder row.");
                    if (item.Kind == "water" || item.Kind == "stretch") item.ScheduleType = "interval";
                    if (item.ScheduleType != "interval" && item.ScheduleType != "date") item.ScheduleType = "time";
                    if (item.IntervalMinutes < 1 || item.IntervalMinutes > 1439) item.IntervalMinutes = 60;
                    TimeSpan from, until; if (!ReminderRules.TryTime(item.ActiveFrom, out from) || !ReminderRules.TryTime(item.ActiveUntil, out until) || from == until) { item.ActiveFrom = "09:00"; item.ActiveUntil = "18:00"; }
                    item.Days = item.SelectedDays;
                    item.Messages = item.GetMessages();
                }
                EnsureBuiltins(result);
                return result;
            }
        }
        ReminderSettings Load() {
            if (!File.Exists(SettingsPath)) return Defaults();
            try { return Read(SettingsPath); }
            catch {
                try {
                    ReminderSettings backup = Read(SettingsPath + ".bak");
                    MessageBox.Show("알림 설정 파일을 읽을 수 없어 이전 백업을 불러왔어요. 알림 설정에서 확인 후 저장해 주세요.", "춘식이 알림");
                    return backup;
                } catch {
                    MessageBox.Show("알림 설정 파일을 읽을 수 없어 기본값을 불러왔어요. 알림 설정에서 확인 후 저장해 주세요.", "춘식이 알림");
                    return Defaults();
                }
            }
        }
        void Write(ReminderSettings value) {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            string temp = SettingsPath + ".tmp";
            try {
                using (FileStream stream = File.Create(temp)) Serializer.Serialize(stream, value);
                if (File.Exists(SettingsPath)) File.Replace(temp, SettingsPath, SettingsPath + ".bak", true);
                else File.Move(temp, SettingsPath);
            } finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        void SaveFired() {
            try { Write(settings); storageWarningShown = false; }
            catch {
                if (!storageWarningShown) {
                    storageWarningShown = true;
                    Enqueue("알림 기록 저장에 실패했어요. 이번 실행에서는 중복을 막지만, 재실행 시 기록이 유지되지 않을 수 있어요.", false);
                }
            }
        }
        NoticeData FromReminder(Reminder item) {
            return new NoticeData { BubbleColor = item.BubbleColor, ReminderId = item.Id, Interval = item.IsInterval, Kind = item.Kind, Label = (item.IsDate ? item.Date + (item.HasTime ? " " + item.Time : " · 시작할 때") : item.IsInterval ? ReminderRules.IntervalLabel(item.IntervalMinutes) : item.Time) + " · " + item.Title, Messages = item.GetMessages(), Seconds = settings.MessageSeconds, SnoozeMinutes = settings.SnoozeMinutes };
        }
        void Check() {
            DateTime now = DateTime.Now, lastBeforeCheck = lastCheck;
            bool changed = false;
            List<NoticeData> dueNotices = new List<NoticeData>();
            foreach (Reminder item in settings.Items) {
                if (item.IsDate) {
                    string key = item.Id + ":" + item.Date;
                    if (ReminderRules.DateDue(item, now, sessionStarted, dateSeen.Contains(key))) {
                        dateSeen.Add(key);
                        if (item.HasTime) { item.LastFired = ReminderRules.DateKey(item); changed = true; }
                        dueNotices.Add(FromReminder(item));
                    }
                    continue;
                }
                if (item.IsInterval) {
                    DateTime next;
                    if (!item.Enabled || !intervalDue.TryGetValue(item.Id, out next)) continue;
                    if (!item.ActivityAllows(now, PetPreferences.Current.InputIdleSeconds) || !ReminderRules.OnDay(item, now)) { intervalDue[item.Id] = now.AddMinutes(item.IntervalMinutes); continue; }
                    if (now < lastCheck) { intervalDue[item.Id] = now.AddMinutes(item.IntervalMinutes); continue; }
                    if (now >= next) {
                        intervalDue[item.Id] = now.AddMinutes(item.IntervalMinutes);
                        if (ReminderRules.OnDay(item, now)) dueNotices.Add(FromReminder(item));
                    }
                    continue;
                }
                DateTime due;
                if (!ReminderRules.TryDue(item, lastCheck, now, out due)) continue;
                item.LastFired = due.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                dueNotices.Add(FromReminder(item)); changed = true;
            }
            lastCheck = now;
            for (int i = 0; i < snoozes.Count;) {
                if (snoozes[i].Due > now) { i++; continue; }
                dueNotices.Add(snoozes[i].Notice); snoozes.RemoveAt(i);
            }
            // Collect one scheduler tick before assigning pets to any of its reminders.
            collecting = true;
            try {
                if (changed) SaveFired();
                foreach (NoticeData data in dueNotices) { data.Missed = now - lastBeforeCheck > TimeSpan.FromMinutes(5); Enqueue(data, settings.Sound); }
            } finally { collecting = false; }
            ShowNext();
            if (dueNotices.Count > 0) SaveSession();
        }
        void Enqueue(string message, bool sound) {
            Enqueue(new NoticeData { Label = "춘식이 알림", Messages = new List<string> { message }, Seconds = settings.MessageSeconds, SnoozeMinutes = settings.SnoozeMinutes }, sound);
        }
        void Enqueue(NoticeData data, bool sound) {
            if (stopping || data.Messages == null || data.Messages.Count == 0) return;
            if (data.Interval && !String.IsNullOrEmpty(data.ReminderId)) {
                // Allow one pending recurrence even while its previous bubble is still open.
                foreach (NoticeData waiting in queue) if (waiting.ReminderId == data.ReminderId) return;
                foreach (Snooze later in snoozes) if (later.Notice.ReminderId == data.ReminderId) return;
            }
            ReminderHistory.Record(data, data.Missed ? "절전·자리 비움 후 복구" : "대기");
            queue.Enqueue(data); SaveSession();
            if (sound && !PresentationPolicy.IsSuppressed) { try { SystemSounds.Asterisk.Play(); } catch {} }
            ShowNext();
        }
        void ShowNext() {
            if (PresentationPolicy.IsSuppressed || stopping || collecting || regrouping || active.Count != 0 || queue.Count == 0) return;
            int pending = queue.Count;
            while (pending-- > 0) {
                NoticeData data = queue.Dequeue(); bool allowed = true;
                if (data.Interval && !String.IsNullOrEmpty(data.ReminderId)) {
                    Reminder item = settings.Items.Find(delegate(Reminder r) { return r.Id == data.ReminderId; });
                    allowed = item != null && item.Enabled && ReminderRules.OnDay(item, DateTime.Now) && item.ActivityAllows(DateTime.Now, PetPreferences.Current.InputIdleSeconds);
                }
                if (allowed) queue.Enqueue(data); else ReminderHistory.Record(data, "활동 시간·자리 비움으로 생략");
            }
            if (queue.Count == 0) { SaveSession(); return; }
            List<PetWindow> pets = PetWindow.SpeechPets();
            if (pets.Count == 0) return;
            int total = 0; foreach (NoticeData waiting in queue) total += waiting.Messages.Count;
            bool concurrent = queue.Count > 1 && total <= pets.Count;
            int count = concurrent ? queue.Count : 1, offset = 0;
            for (int i = 0; i < count; i++) {
                NoticeData data = queue.Dequeue();
                List<PetWindow> assigned = concurrent ? pets.GetRange(offset, data.Messages.Count) : null;
                offset += data.Messages.Count;
                bool deferred = false;
                ReminderHistory.Record(data, "표시됨");
                NoticeGroup group = new NoticeGroup(data, delegate {
                    deferred = true; ReminderHistory.Record(data, "다시 알림 예약");
                    snoozes.Add(new Snooze { Due = DateTime.Now.AddMinutes(data.SnoozeMinutes), Notice = data });
                }, assigned);
                group.Closed += delegate {
                    active.Remove(group);
                    if (!regrouping && !stopping && !deferred) {
                        ReminderHistory.Record(data, group.AutomaticallyAdvanced ? "다음 알림으로 전환" : "확인");
                        if (!group.AutomaticallyAdvanced && data.Acknowledge != null) data.Acknowledge();
                    }
                    SaveSession(); if (!regrouping) ShowNext();
                };
                active.Add(group);
            }
            foreach (NoticeGroup group in new List<NoticeGroup>(active)) group.Show();
            LayoutNotices(); PetWindow.Celebrate(); SaveSession();
        }
        public void Stop() {
            SaveSession(); stopping = true; timer.Stop(); queue.Clear(); snoozes.Clear();
            foreach (NoticeGroup group in new List<NoticeGroup>(active)) group.Close();
            if (editor != null) editor.Close();
        }
        public void ClearPendingForRestore() {
            regrouping = true;
            foreach (NoticeGroup group in new List<NoticeGroup>(active)) group.Close();
            active.Clear(); queue.Clear(); snoozes.Clear(); regrouping = false; SaveSession();
        }
        public NoticeData NotifyTimer(Action acknowledge) {
            NoticeData data = new NoticeData { Label = "타이머 완료", Kind = "timer", Messages = new List<string> { "시간이 다 됐어요! 수고했어요 ♥ 확인 버튼을 눌러주세요." }, Seconds = settings.MessageSeconds, SnoozeMinutes = settings.SnoozeMinutes, Acknowledge = acknowledge };
            Enqueue(data, settings.Sound); return data;
        }
        public void CancelNotice(NoticeData data) {
            if (data == null) return;
            int count = queue.Count;
            while (count-- > 0) { NoticeData pending = queue.Dequeue(); if (pending != data) queue.Enqueue(pending); }
            snoozes.RemoveAll(delegate(Snooze s) { return s.Notice == data; });
            foreach (NoticeGroup group in new List<NoticeGroup>(active)) if (group.Data == data) group.Close();
            SaveSession();
        }
        public void Replay(NoticeData old) {
            Enqueue(new NoticeData { Label = old.Label, Messages = new List<string>(old.Messages), Kind = old.Kind, BubbleColor = old.BubbleColor, Seconds = settings.MessageSeconds, SnoozeMinutes = settings.SnoozeMinutes }, false);
        }
        public void Test() {
            Enqueue(new NoticeData { Label = "말풍선 테스트", Messages = new List<string> { "첫 번째 메시지예요!", "두 번째 메시지는 다른 춘식이가 맡아요.", "남은 메시지도 자동으로 이어서 보여줘요.", "전환 간격은 알림 설정에서 바꿀 수 있어요.", "다시 알림을 누르면 전체 문구를 다시 알려드려요." }, Seconds = settings.MessageSeconds, SnoozeMinutes = settings.SnoozeMinutes }, settings.Sound);
        }
        static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(0, 100, 168));
        static readonly Brush Line = new SolidColorBrush(Color.FromRgb(222, 231, 240));
        static Button Pill(string text, Action action) {
            Button button = new Button { Content = text, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(3), Foreground = Accent, Cursor = System.Windows.Input.Cursors.Hand };
            button.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border x:Name='box' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' CornerRadius='10' Background='#EDF6FD' BorderBrush='#CCE0F0' BorderThickness='1' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='box' Property='Background' Value='#D9ECFA'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.4'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='box' Property='BorderBrush' Value='#0064A8'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
            button.Click += delegate { action(); }; return button;
        }
        static readonly Style SegmentStyle = (Style)System.Windows.Markup.XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='RadioButton'><Setter Property='Padding' Value='8,7'/><Setter Property='FontSize' Value='11'/><Setter Property='Foreground' Value='#546578'/><Setter Property='Background' Value='Transparent'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='RadioButton'><Border CornerRadius='9' Background='{TemplateBinding Background}' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center'/></Border></ControlTemplate></Setter.Value></Setter><Style.Triggers><Trigger Property='IsChecked' Value='True'><Setter Property='Background' Value='#0064A8'/><Setter Property='Foreground' Value='White'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter Property='Opacity' Value='0.7'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.6'/></Trigger></Style.Triggers></Style>");
        static Grid TableRow() {
            Grid grid = new Grid { Margin = new Thickness(12) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(95) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            for (int i=0; i<7; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            return grid;
        }
        static void Cell(Grid grid, UIElement child, int field) {
            string[] labels = { "사용", "알림 이름", "종류 · 시간", "요일 · 날짜", "말풍선 색상", "메시지", "관리" };
            TextBlock label = new TextBlock { Text = labels[field], Foreground = Brushes.SlateGray, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 10, 8, 10) };
            Grid.SetRow(label, field); grid.Children.Add(label);
            Grid.SetRow(child, field); Grid.SetColumn(child, 1); grid.Children.Add(child);
        }
        public void OpenSettings() {
            if (editor != null) { SettingsHub.Open("알림"); return; }
            Window win = new Window { Title = "춘식이 알림 설정", Width = 1000, Height = 700, MinWidth = 620, MinHeight = 480, Background = new SolidColorBrush(Color.FromRgb(246, 249, 253)), WindowStartupLocation = WindowStartupLocation.CenterScreen };
            editor = win;
            List<Reminder> rows = new List<Reminder>(); foreach (Reminder item in settings.Items) rows.Add(item.Copy());
            DockPanel root = new DockPanel { Margin = new Thickness(22) }; win.Content = root;
            StackPanel intro = new StackPanel(); DockPanel.SetDock(intro, Dock.Top); root.Children.Add(intro);
            intro.Children.Add(new TextBlock { Text = "춘식이가 알려드려요", FontSize = 24, FontWeight = FontWeights.Bold, Foreground = Accent });
            intro.Children.Add(new TextBlock { Text = "요일·시간·메시지를 바꾸면 자동 저장돼요. 점심·퇴근·물 마시기·스트레칭 기본 알림은 스위치로 켜고 끌 수 있어요.", Foreground = Brushes.DimGray, Margin = new Thickness(0, 8, 0, 18) });
            intro.Children.Add(new TextBlock { Text = "간격 알림은 실행·간격 변경 후부터 계산하며, 선택한 요일에만 울려요. 기본 간격: 1시간 (1분~23시간 59분).", Foreground = Brushes.SlateGray, Margin = new Thickness(0, 0, 0, 12) });
            TextBlock schedulerClock = new TextBlock { Foreground = Accent, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10), ToolTip = TimeZoneInfo.Local.DisplayName };
            intro.Children.Add(schedulerClock);
            TextBlock status = new TextBlock { Text = "모든 변경사항 저장됨", Foreground = Accent, Margin = new Thickness(0, 12, 0, 0) };
            StackPanel options = new StackPanel { Margin = new Thickness(0, 14, 0, 0) }; DockPanel.SetDock(options, Dock.Bottom); root.Children.Add(options);
            WrapPanel timing = new WrapPanel(); options.Children.Add(timing);
            CheckBox sound = new CheckBox { Content = "알림 소리", IsChecked = settings.Sound, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 20, 0) }; timing.Children.Add(sound);
            timing.Children.Add(new TextBlock { Text = "문구 전환  ", VerticalAlignment = VerticalAlignment.Center });
            NumberField seconds = new NumberField(settings.MessageSeconds, 60, "문구 전환 초");
            timing.Children.Add(seconds); timing.Children.Add(new TextBlock { Text = " 초 (3~60)     다시 알림  ", VerticalAlignment = VerticalAlignment.Center });
            NumberField snooze = new NumberField(settings.SnoozeMinutes, 1440, "다시 알림 분"); timing.Children.Add(snooze);
            timing.Children.Add(new TextBlock { Text = " 분 (1~1440)", VerticalAlignment = VerticalAlignment.Center }); options.Children.Add(status);
            DispatcherTimer debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
            Dictionary<string, TextBlock> countdowns = new Dictionary<string, TextBlock>();
            DispatcherTimer countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            Action refreshCountdowns = delegate {
                DateTime now = DateTime.Now;
                schedulerClock.Text = "PC 현재 시간 · " + now.ToString("yyyy-MM-dd (ddd) HH:mm:ss");
                foreach (KeyValuePair<string, TextBlock> pair in countdowns) {
                    string detail;
                    if (debounce.IsEnabled) pair.Value.Text = "변경사항 저장 후 계산…";
                    else { pair.Value.Text = Countdown(pair.Key, now, out detail); pair.Value.ToolTip = detail; }
                }
            };
            Func<bool> save = delegate {
                debounce.Stop(); int minutes = snooze.Value;
                if (seconds.Value < 3) { status.Text = "문구 전환 시간은 3~60초로 입력해 주세요."; status.Foreground = Brushes.Firebrick; return false; }
                if (minutes < 1 || minutes > 1440) { status.Text = "다시 알림 시간을 1~1440분으로 입력해 주세요. 아직 저장되지 않았어요."; status.Foreground = Brushes.Firebrick; return false; }
                ReminderSettings updated = new ReminderSettings { Sound = sound.IsChecked == true, MessageSeconds = seconds.Value, SnoozeMinutes = minutes };
                foreach (Reminder row in rows) {
                    TimeSpan time;
                    if (String.IsNullOrWhiteSpace(row.Title) || !ReminderRules.TryTime(row.Time, out time) || (row.IsInterval && (row.IntervalMinutes < 1 || row.IntervalMinutes > 1439))) { status.Text = "알림 이름과 시간을 확인해 주세요. 간격은 1분 이상이어야 해요. 아직 저장되지 않았어요."; status.Foreground = Brushes.Firebrick; return false; }
                    DateTime selectedDate;
                    if (row.IsDate && !ReminderRules.TryDate(row.Date, out selectedDate)) { status.Text = "특정 날짜 알림은 날짜를 반드시 선택해 주세요."; status.Foreground = Brushes.Firebrick; return false; }
                    Reminder copy = row.Copy(); copy.Title = copy.Title.Trim();
                    foreach (Reminder live in settings.Items) if (live.Id == copy.Id)
                        copy.LastFired = ReminderRules.SameSchedule(live, copy) ? live.LastFired : "";
                    updated.Items.Add(copy);
                }
                EnsureBuiltins(updated);
                try { Write(updated); } catch (Exception ex) { status.Text = "저장 실패: " + ex.Message; status.Foreground = Brushes.Firebrick; return false; }
                foreach (Reminder next in updated.Items) foreach (Reminder prior in settings.Items)
                    if (next.Id == prior.Id && !ReminderRules.SameSchedule(prior, next)) dateSeen.RemoveWhere(delegate(string key) { return key.StartsWith(next.Id + ":", StringComparison.Ordinal); });
                ResetIntervals(updated, settings, DateTime.Now);
                settings = updated; status.Text = "모든 변경사항 저장됨"; status.Foreground = Accent; refreshCountdowns(); return true;
            };
            Action changed = delegate { status.Text = "변경사항 저장 중…"; status.Foreground = Accent; debounce.Stop(); debounce.Start(); };
            debounce.Tick += delegate { save(); };
            sound.Checked += delegate { changed(); }; sound.Unchecked += delegate { changed(); };
            seconds.Changed += changed; snooze.Changed += changed;
            StackPanel table = new StackPanel();
            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = table };
            root.Children.Add(new Border { CornerRadius = new CornerRadius(14), Background = Brushes.White, BorderBrush = Line, BorderThickness = new Thickness(1), Padding = new Thickness(10), Child = scroll });
            string selectedId = rows.Count > 0 ? rows[0].Id : null;
            Action render = null;
            render = delegate {
                table.Children.Clear(); countdowns.Clear();
                DockPanel toolbar = new DockPanel { Margin = new Thickness(4, 0, 4, 8) };
                Button add = Pill("＋ 알림 추가", delegate { Reminder added = new Reminder(); rows.Add(added); selectedId = added.Id; render(); changed(); scroll.ScrollToTop(); });
                DockPanel.SetDock(add, Dock.Right); toolbar.Children.Add(add);
                toolbar.Children.Add(new TextBlock { Text = "알림 목록 · " + rows.Count, FontSize = 16, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Foreground = Accent }); table.Children.Add(toolbar);
                if (!rows.Exists(delegate(Reminder r) { return r.Id == selectedId; })) selectedId = rows.Count > 0 ? rows[0].Id : null;
                Grid body = new Grid(); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) }); body.ColumnDefinitions.Add(new ColumnDefinition());
                body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); table.Children.Add(body);
                ListBox choices = new ListBox { Height = 430, BorderThickness = new Thickness(0), Margin = new Thickness(0, 0, 12, 0), HorizontalContentAlignment = HorizontalAlignment.Stretch };
                body.Children.Add(choices);
                foreach (Reminder candidate in rows) {
                    StackPanel summary = new StackPanel { Margin = new Thickness(6, 10, 6, 10) };
                    summary.Children.Add(new TextBlock { Text = candidate.Title, FontSize = 14, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
                    TextBlock next = new TextBlock { FontSize = 11, Foreground = Accent, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 0) };
                    summary.Children.Add(next); countdowns[candidate.Id] = next;
                    ListBoxItem choice = new ListBoxItem { Content = summary, Tag = candidate.Id }; choices.Items.Add(choice);
                    if (candidate.Id == selectedId) choices.SelectedItem = choice;
                }
                choices.SelectionChanged += delegate { ListBoxItem chosen = choices.SelectedItem as ListBoxItem; if (chosen != null && (string)chosen.Tag != selectedId) { selectedId = (string)chosen.Tag; render(); } };
                StackPanel detail = new StackPanel(); Grid.SetColumn(detail, 1); body.Children.Add(detail);
                body.SizeChanged += delegate {
                    bool narrow = body.ActualWidth < 760;
                    body.ColumnDefinitions[0].Width = narrow ? new GridLength(1, GridUnitType.Star) : new GridLength(210);
                    body.ColumnDefinitions[1].Width = narrow ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
                    Grid.SetRow(detail, narrow ? 1 : 0); Grid.SetColumn(detail, narrow ? 0 : 1); choices.Height = narrow ? 150 : 430;
                };
                foreach (Reminder row in rows) {
                    if (row.Id != selectedId) continue;
                    Grid line = TableRow();
                    Border card = new Border { Background = new SolidColorBrush(Color.FromRgb(250, 252, 255)), BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Child = line }; detail.Children.Add(card);
                    System.Windows.Controls.Primitives.ToggleButton toggle = new System.Windows.Controls.Primitives.ToggleButton { Width = 44, Height = 24, IsChecked = row.Enabled, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(5), ToolTip = "알림 켜기/끄기" };
                    toggle.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ToggleButton'><Border x:Name='track' Background='#CCD5DF' CornerRadius='12'><Ellipse x:Name='thumb' Width='18' Height='18' Fill='White' HorizontalAlignment='Left' Margin='3'/></Border><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='track' Property='Background' Value='#0064A8'/><Setter TargetName='thumb' Property='HorizontalAlignment' Value='Right'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='track' Property='BorderBrush' Value='#F6B73C'/><Setter TargetName='track' Property='BorderThickness' Value='2'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
                    System.Windows.Automation.AutomationProperties.SetName(toggle, row.Title + " 알림 사용");
                    toggle.Checked += delegate { row.Enabled = true; changed(); }; toggle.Unchecked += delegate { row.Enabled = false; changed(); }; Cell(line, toggle, 0);
                    StackPanel titleArea = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 4, 10, 4) };
                    if (row.IsBuiltin) titleArea.Children.Add(new TextBlock { Text = "기본 알림", FontSize = 10, Foreground = Accent });
                    TextBox title = new TextBox { Text = row.Title, BorderThickness = new Thickness(0), Background = Brushes.Transparent, Padding = new Thickness(2, 4, 2, 4), FontSize = 14 };
                    title.TextChanged += delegate { row.Title = title.Text; ListBoxItem choice = choices.SelectedItem as ListBoxItem; if (choice != null) ((TextBlock)((StackPanel)choice.Content).Children[0]).Text = row.Title; changed(); }; titleArea.Children.Add(title); Cell(line, titleArea, 1);
                    StackPanel schedule = new StackPanel();
                    StackPanel segments = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 7) };
                    string[] types = { "time", "interval", "date" }, labels = { "매주 시각", "일정 간격", "특정 날짜" };
                    for (int k = 0; k < types.Length; k++) {
                        string selectedType = types[k];
                        RadioButton segment = new RadioButton { Content = labels[k], GroupName = "schedule_" + row.Id, IsChecked = row.ScheduleType == selectedType, IsEnabled = !row.IsBuiltin, Style = SegmentStyle };
                        segment.Checked += delegate { if (row.ScheduleType == selectedType) return; row.ScheduleType = selectedType; render(); changed(); };
                        segments.Children.Add(segment);
                    }
                    schedule.Children.Add(new Border { CornerRadius = new CornerRadius(10), Background = new SolidColorBrush(Color.FromRgb(226, 235, 245)), Child = segments });
                    TimeSpan t; ReminderRules.TryTime(row.Time, out t);
                    NumberField hour = new NumberField(row.IsInterval ? row.IntervalMinutes / 60 : t.Hours, 23, "시간");
                    NumberField minute = new NumberField(row.IsInterval ? row.IntervalMinutes % 60 : t.Minutes, 59, "분");
                    StackPanel clock = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                    clock.Children.Add(hour); clock.Children.Add(new TextBlock { Text = row.IsInterval ? "시간" : "시", Margin = new Thickness(4), VerticalAlignment = VerticalAlignment.Center });
                    clock.Children.Add(minute); clock.Children.Add(new TextBlock { Text = "분", Margin = new Thickness(4), VerticalAlignment = VerticalAlignment.Center });
                    Action timeChanged = delegate { if (row.IsInterval) row.IntervalMinutes = hour.Value * 60 + minute.Value; else row.Time = hour.Value.ToString("00") + ":" + minute.Value.ToString("00"); changed(); };
                    hour.Changed += timeChanged; minute.Changed += timeChanged;
                    if (row.IsDate) {
                        CheckBox specify = new CheckBox { Content = "시간도 지정", IsChecked = row.HasTime, Margin = new Thickness(4, 3, 0, 6) };
                        clock.IsEnabled = row.HasTime;
                        specify.Checked += delegate { row.HasTime = true; clock.IsEnabled = true; changed(); };
                        specify.Unchecked += delegate { row.HasTime = false; clock.IsEnabled = false; changed(); };
                        schedule.Children.Add(specify);
                    }
                    schedule.Children.Add(clock);
                    if (row.IsInterval) schedule.Children.Add(Pill(row.ActivityEnabled ? "활동 " + row.ActiveFrom + "~" + row.ActiveUntil : "활동 시간 · 자리 비움", delegate { EditActivity(win, row, changed); }));
                    Cell(line, new Border { CornerRadius = new CornerRadius(12), Background = new SolidColorBrush(Color.FromRgb(237, 246, 253)), Padding = new Thickness(5), Margin = new Thickness(0, 8, 10, 8), VerticalAlignment = VerticalAlignment.Center, Child = schedule }, 2);
                    if (row.IsDate) {
                        StackPanel dateArea = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8) };
                        DateTime date; DatePicker picker = new DatePicker { SelectedDateFormat = DatePickerFormat.Short, ToolTip = "필수: 알림 날짜를 선택하세요", Language = System.Windows.Markup.XmlLanguage.GetLanguage("ko-KR") };
                        if (ReminderRules.TryDate(row.Date, out date)) picker.SelectedDate = date;
                        picker.SelectedDateChanged += delegate { row.Date = picker.SelectedDate.HasValue ? picker.SelectedDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : ""; changed(); };
                        picker.DateValidationError += delegate(object sender, DatePickerDateValidationErrorEventArgs e) { e.ThrowException = false; picker.SelectedDate = null; row.Date = ""; changed(); };
                        dateArea.Children.Add(picker);
                        dateArea.Children.Add(new TextBlock { Text = "날짜 필수 · 반복 없음\n시간 미지정: 당일 실행 시 알림", FontSize = 11, Foreground = Brushes.SlateGray, Margin = new Thickness(0, 7, 0, 0) }); Cell(line, dateArea, 3);
                    } else {
                        WeekdayPicker days = new WeekdayPicker { DataContext = row };
                        days.AddHandler(System.Windows.Controls.Primitives.ToggleButton.CheckedEvent, new RoutedEventHandler(delegate { changed(); }));
                        days.AddHandler(System.Windows.Controls.Primitives.ToggleButton.UncheckedEvent, new RoutedEventHandler(delegate { changed(); })); Cell(line, days, 3);
                    }
                    Cell(line, BubblePalette.Picker(row.BubbleColor, delegate(string color) { row.BubbleColor = color; changed(); }, 3), 4);
                    Button edit = Pill("메시지 편집", delegate { if (save()) EditMessages(win, row, save); else MessageBox.Show(SettingsHub.OwnerFor(win), status.Text, "입력 확인"); }); edit.VerticalAlignment = VerticalAlignment.Center; Cell(line, edit, 5);
                    StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                    actions.Children.Add(Pill("▶ 보기", delegate { PreviewUI.Reminder(win, row, delegate { return seconds.Value; }); }));
                    actions.Children.Add(Pill("테스트 알림", delegate { Test(); }));
                    Button remove = Pill(row.IsBuiltin ? "기본" : "삭제", delegate { if (row.IsBuiltin) return; rows.Remove(row); render(); changed(); });
                    remove.IsEnabled = !row.IsBuiltin; remove.ToolTip = row.IsBuiltin ? "기본 알림은 삭제할 수 없어요" : "이 알림 삭제"; actions.Children.Add(remove); Cell(line, actions, 6);
                }
                refreshCountdowns();
            };
            render(); refreshCountdowns(); countdownTimer.Tick += delegate { refreshCountdowns(); }; countdownTimer.Start();
            win.Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) { if (!stopping && !save()) { e.Cancel = true; MessageBox.Show(SettingsHub.OwnerFor(win), status.Text, "저장 확인"); } };
            win.Closed += delegate { debounce.Stop(); countdownTimer.Stop(); editor = null; }; SettingsHub.Attach(win, "알림", save);
        }
        sealed class MessageDraft { public string Text; }
        void EditActivity(Window owner, Reminder row, Action changed) {
            Window dialog = new Window { Owner = SettingsHub.OwnerFor(owner), Title = "간격 알림 활동 시간", Width = 440, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            StackPanel content = new StackPanel { Margin = new Thickness(22) }; dialog.Content = content;
            CheckBox enabled = new CheckBox { Content = "선택한 시간대에만 알려주기", IsChecked = row.ActivityEnabled, Margin = new Thickness(0, 0, 0, 14) }; content.Children.Add(enabled);
            TimeSpan a, z; ReminderRules.TryTime(row.ActiveFrom, out a); ReminderRules.TryTime(row.ActiveUntil, out z);
            NumberField ah = new NumberField(a.Hours, 23, "시작 시"), am = new NumberField(a.Minutes, 59, "시작 분"), zh = new NumberField(z.Hours, 23, "종료 시"), zm = new NumberField(z.Minutes, 59, "종료 분");
            WrapPanel times = new WrapPanel(); foreach (UIElement element in new UIElement[] { ah, new TextBlock { Text = ":", Margin = new Thickness(4) }, am, new TextBlock { Text = " ~ ", Margin = new Thickness(4) }, zh, new TextBlock { Text = ":", Margin = new Thickness(4) }, zm }) times.Children.Add(element); content.Children.Add(times);
            CheckBox idle = new CheckBox { Content = "입력이 5분 없으면 간격 알림 잠시 멈추기", IsChecked = row.PauseWhenIdle, Margin = new Thickness(0, 18, 0, 14) }; content.Children.Add(idle);
            content.Children.Add(new TextBlock { Text = @"종료 시각은 포함하지 않아요. 22:00~06:00처럼 자정을 넘길 수 있어요.
활동 시간 복귀·입력 재개 후 간격을 처음부터 계산해요.
요일 선택도 함께 적용하며 중단 중 알림을 쌓지 않아요.", TextWrapping = TextWrapping.Wrap });
            Button apply = Pill("적용", delegate {
                string from = ah.Value.ToString("00") + ":" + am.Value.ToString("00"), until = zh.Value.ToString("00") + ":" + zm.Value.ToString("00");
                if (enabled.IsChecked == true && from == until) { MessageBox.Show(dialog, "시작과 종료 시각을 다르게 선택해 주세요."); return; }
                row.ActivityEnabled = enabled.IsChecked == true; row.ActiveFrom = from; row.ActiveUntil = until; row.PauseWhenIdle = idle.IsChecked == true; changed(); dialog.Close();
            }); content.Children.Add(apply); dialog.ShowDialog();
        }
        void EditMessages(Window owner, Reminder row, Func<bool> persist) {
            owner = SettingsHub.OwnerFor(owner);
            Window dialog = new Window { Owner = owner, Title = "메시지 편집 — " + row.Title, Width = 650, Height = 510, MinWidth = 560, MinHeight = 350, Background = new SolidColorBrush(Color.FromRgb(246, 249, 253)), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            List<MessageDraft> drafts = new List<MessageDraft>(); foreach (string text in row.GetMessages()) drafts.Add(new MessageDraft { Text = text });
            if (drafts.Count == 0) drafts.Add(new MessageDraft { Text = "" });
            DockPanel root = new DockPanel { Margin = new Thickness(20) }; dialog.Content = root;
            StackPanel top = new StackPanel(); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
            top.Children.Add(new TextBlock { Text = "말풍선 메시지", FontSize = 22, FontWeight = FontWeights.Bold, Foreground = Accent });
            top.Children.Add(new TextBlock { Text = "왼쪽 ≡ 손잡이를 끌어 순서를 바꾸세요. 위에서 아래 순서로 표시돼요.\n입력·삭제·순서 변경은 자동 저장됩니다. ↑ ↓ 버튼으로도 옮길 수 있어요.", Margin = new Thickness(0, 8, 0, 14), Foreground = Brushes.DimGray });
            TextBlock status = new TextBlock { Text = "모든 변경사항 저장됨", Foreground = Accent, Margin = new Thickness(0, 10, 0, 0) }; DockPanel.SetDock(status, Dock.Bottom); root.Children.Add(status);
            StackPanel list = new StackPanel(); ScrollViewer scroll = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; root.Children.Add(scroll);
            DispatcherTimer debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
            Func<bool> save = delegate {
                debounce.Stop(); List<string> messages = new List<string>();
                foreach (MessageDraft draft in drafts) {
                    if (String.IsNullOrWhiteSpace(draft.Text)) { status.Text = "빈 메시지를 입력하거나 삭제해 주세요. 아직 저장되지 않았어요."; status.Foreground = Brushes.Firebrick; return false; }
                    messages.Add(draft.Text.Trim());
                }
                row.Messages = messages;
                bool ok = persist(); status.Text = ok ? "모든 변경사항 저장됨" : "알림 설정의 입력 또는 저장 오류를 확인해 주세요."; status.Foreground = ok ? Accent : Brushes.Firebrick; return ok;
            };
            Action changed = delegate { status.Text = "변경사항 저장 중…"; status.Foreground = Accent; debounce.Stop(); debounce.Start(); };
            debounce.Tick += delegate { save(); };
            Action render = null;
            render = delegate {
                list.Children.Clear();
                foreach (MessageDraft draft in drafts) {
                    Grid line = new Grid(); foreach (double w in new double[] { 48, -1, 40, 40, 48 }) line.ColumnDefinitions.Add(new ColumnDefinition { Width = w < 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(w) });
                    Border card = new Border { CornerRadius = new CornerRadius(12), Background = Brushes.White, BorderBrush = Line, BorderThickness = new Thickness(1), Padding = new Thickness(7), Margin = new Thickness(0, 0, 0, 8), Child = line, AllowDrop = true };
                    list.Children.Add(card);
                    TextBlock handle = new TextBlock { Text = "≡ " + (drafts.IndexOf(draft) + 1), VerticalAlignment = VerticalAlignment.Center, Foreground = Accent, Cursor = System.Windows.Input.Cursors.SizeAll, ToolTip = "끌어서 순서 변경" }; Cell(line, handle, 0);
                    Point down = new Point(); handle.MouseLeftButtonDown += delegate(object sender, System.Windows.Input.MouseButtonEventArgs e) { down = e.GetPosition(handle); };
                    handle.MouseMove += delegate(object sender, System.Windows.Input.MouseEventArgs e) {
                        if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;
                        Point at = e.GetPosition(handle);
                        if (Math.Abs(at.Y - down.Y) < SystemParameters.MinimumVerticalDragDistance && Math.Abs(at.X - down.X) < SystemParameters.MinimumHorizontalDragDistance) return;
                        DragDrop.DoDragDrop(handle, new DataObject(typeof(MessageDraft), draft), DragDropEffects.Move);
                    };
                    card.DragOver += delegate(object sender, DragEventArgs e) {
                        if (!e.Data.GetDataPresent(typeof(MessageDraft))) { e.Effects = DragDropEffects.None; return; }
                        e.Effects = DragDropEffects.Move; card.BorderBrush = Accent;
                        card.BorderThickness = e.GetPosition(card).Y < card.ActualHeight / 2 ? new Thickness(1, 3, 1, 1) : new Thickness(1, 1, 1, 3);
                        double y = e.GetPosition(scroll).Y; if (y < 40) scroll.ScrollToVerticalOffset(scroll.VerticalOffset - 16); else if (y > scroll.ActualHeight - 40) scroll.ScrollToVerticalOffset(scroll.VerticalOffset + 16);
                        e.Handled = true;
                    };
                    card.DragLeave += delegate { card.BorderBrush = Line; card.BorderThickness = new Thickness(1); };
                    card.Drop += delegate(object sender, DragEventArgs e) {
                        MessageDraft source = e.Data.GetData(typeof(MessageDraft)) as MessageDraft;
                        if (source == null || source == draft || !drafts.Contains(source)) { card.BorderBrush = Line; card.BorderThickness = new Thickness(1); return; }
                        bool after = e.GetPosition(card).Y >= card.ActualHeight / 2;
                        drafts.Remove(source); int target = drafts.IndexOf(draft) + (after ? 1 : 0); drafts.Insert(target, source);
                        e.Handled = true; render(); changed();
                    };
                    TextBox text = new TextBox { Text = draft.Text, TextWrapping = TextWrapping.Wrap, AcceptsReturn = false, MinHeight = 36, Padding = new Thickness(7), BorderThickness = new Thickness(0), VerticalContentAlignment = VerticalAlignment.Center, FontSize = 15 };
                    text.TextChanged += delegate { draft.Text = text.Text.Replace("\r", " ").Replace("\n", " "); changed(); }; Cell(line, text, 1);
                    Button up = Pill("↑", delegate { int i = drafts.IndexOf(draft); if (i > 0) { drafts.RemoveAt(i); drafts.Insert(i - 1, draft); render(); changed(); } }); up.IsEnabled = drafts.IndexOf(draft) > 0; Cell(line, up, 2);
                    Button next = Pill("↓", delegate { int i = drafts.IndexOf(draft); if (i < drafts.Count - 1) { drafts.RemoveAt(i); drafts.Insert(i + 1, draft); render(); changed(); } }); next.IsEnabled = drafts.IndexOf(draft) < drafts.Count - 1; Cell(line, next, 3);
                    Button remove = Pill("×", delegate { if (drafts.Count > 1) { drafts.Remove(draft); render(); changed(); } }); remove.IsEnabled = drafts.Count > 1; remove.ToolTip = "메시지 삭제"; Cell(line, remove, 4);
                }
                list.Children.Add(Pill("＋ 메시지 추가", delegate { drafts.Add(new MessageDraft { Text = "" }); render(); changed(); scroll.ScrollToBottom(); }));
            };
            render();
            dialog.Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) { if (!stopping && !save()) { e.Cancel = true; MessageBox.Show(dialog, status.Text, "저장 확인"); } };
            dialog.Closed += delegate { debounce.Stop(); }; dialog.ShowDialog();
        }
        static Button ButtonFor(string text, RoutedEventHandler click) {
            Button button = new Button { Content = text, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(5, 0, 0, 0) };
            button.Click += click; return button;
        }
    }

    public sealed class NoticeGroup {
        public string Kind { get { return data.Kind ?? ""; } }
        public bool AutomaticallyAdvanced { get; private set; }
        readonly NoticeData data;
        public NoticeData Data { get { return data; } }
        readonly List<PetWindow> assigned;
        readonly Action snooze;
        readonly List<NoticeWindow> windows = new List<NoticeWindow>();
        readonly DispatcherTimer timer = new DispatcherTimer();
        readonly System.Diagnostics.Stopwatch elapsed = new System.Diagnostics.Stopwatch();
        int start, shown;
        bool closed;
        public event EventHandler Closed;
        public NoticeGroup(NoticeData value, Action later, List<PetWindow> allocation) {
            data = value; snooze = later; assigned = allocation;
            timer.Interval = TimeSpan.FromMilliseconds(100);
            timer.Tick += delegate {
                if (closed) return;
                if (PresentationPolicy.IsSuppressed) { elapsed.Stop(); return; }
                elapsed.Start();
                List<PetWindow> live = PetWindow.SpeechPets();
                if (assigned != null) foreach (PetWindow pet in assigned) {
                    if (!live.Contains(pet)) { ReminderService.Current.RebalanceNotices(); return; }
                }
                List<PetWindow> pets = assigned ?? live;
                bool changed = windows.Count != Math.Min(pets.Count, data.Messages.Count - start);
                foreach (NoticeWindow win in windows) if (!pets.Contains(win.Pet)) changed = true;
                if (changed) { ShowBatch(); return; }
                if (elapsed.Elapsed.TotalSeconds >= Math.Max(3, data.Seconds) && start + shown < data.Messages.Count) {
                    start += shown; ShowBatch(); return;
                }
                // A final bubble may stay open, but must not block later scheduled alarms indefinitely.
                if (elapsed.Elapsed.TotalSeconds >= Math.Max(3, data.Seconds) && ReminderService.Current.HasWaitingNotices) { AutomaticallyAdvanced = true; Close(); return; }
                Layout();
            };
        }
        public bool Contains(PetWindow pet) { foreach (NoticeWindow win in windows) if (win.Pet == pet) return true; return false; }
        public void Show() { ShowBatch(); if (!closed) timer.Start(); }
        void ClearWindows() { foreach (NoticeWindow win in windows) win.CloseForBatch(); windows.Clear(); }
        void ShowBatch() {
            ClearWindows();
            List<PetWindow> pets = assigned ?? PetWindow.SpeechPets();
            if (pets.Count == 0) { Close(); return; }
            shown = Math.Min(pets.Count, data.Messages.Count - start);
            for (int i = 0; i < shown; i++) {
                NoticeWindow win = new NoticeWindow(data, start + i, pets[i], delegate {
                    if (closed) return;
                    snooze(); Close();
                }, Close);
                windows.Add(win); win.Show();
            }
            elapsed.Restart(); Layout();
        }
        void Layout() { ReminderService.Current.LayoutNotices(); }
        public void AddBounds(List<Rect> bounds) {
            foreach (NoticeWindow win in windows) if (win.IsVisible && win.ActualWidth > 0 && win.ActualHeight > 0) bounds.Add(new Rect(win.Left, win.Top, win.ActualWidth, win.ActualHeight));
        }
        public void AppendLayout(List<Rect> occupied) {
            foreach (NoticeWindow win in windows) occupied.Add(win.Reposition(occupied));
        }
        public void Close() {
            if (closed) return;
            closed = true; timer.Stop(); elapsed.Stop(); ClearWindows();
            if (Closed != null) Closed(this, EventArgs.Empty);
        }
    }

    public sealed class NoticeWindow : Window {
        public readonly PetWindow Pet;
        bool batchClosing;
        public void CloseForBatch() { batchClosing = true; Close(); }
        readonly System.Windows.Shapes.Polygon tail = new System.Windows.Shapes.Polygon();
        readonly System.Windows.Shapes.Polyline tailEdge = new System.Windows.Shapes.Polyline();
        public NoticeWindow(NoticeData data, int index, PetWindow pet, Action snooze, Action dismiss) {
            Pet = pet;
            Closed += delegate { if (!batchClosing) dismiss(); };
            Title = "춘식이 말풍선"; Width = 370; SizeToContent = SizeToContent.Height;
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true; Background = Brushes.Transparent; Topmost = true;
            ShowInTaskbar = false; ShowActivated = false;
            Brush fill = BubblePalette.BrushFor(data.BubbleColor);
            Brush outline = new SolidColorBrush(Color.FromRgb(67, 49, 31));
            StackPanel root = new StackPanel(); Content = root;
            Border card = new Border { Background = fill, BorderBrush = outline, BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(16), Padding = new Thickness(14, 11, 14, 10) };
            StackPanel stack = new StackPanel(); card.Child = stack; root.Children.Add(card);
            stack.Children.Add(new TextBlock { Text = data.Label, FontSize = 11, Foreground = Brushes.DimGray, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 0, 8) });
            ScrollViewer scroll = new ScrollViewer { MaxHeight = 180, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            scroll.Content = new TextBlock { Text = data.Messages[index], TextWrapping = TextWrapping.Wrap, FontSize = 18, FontWeight = FontWeights.SemiBold, Foreground = outline };
            stack.Children.Add(scroll);
            DockPanel controls = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
            controls.Children.Add(new TextBlock { Text = (index + 1) + " / " + data.Messages.Count, VerticalAlignment = VerticalAlignment.Center, FontSize = 11, Foreground = Brushes.DimGray });
            WrapPanel buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
            ComboBox delay = new ComboBox { Width = 86, FontSize = 14, Margin = new Thickness(4, 0, 0, 0), VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "다시 알림까지 기다릴 시간" };
            List<int> delays = new List<int> { 5, 10, 30 }; if (!delays.Contains(data.SnoozeMinutes)) delays.Add(data.SnoozeMinutes);
            foreach (int minutes in delays) { ComboBoxItem choice = new ComboBoxItem { Content = minutes + "분 후", Tag = minutes }; delay.Items.Add(choice); if (minutes == data.SnoozeMinutes) delay.SelectedItem = choice; }
            buttons.Children.Add(delay);
            Button later = new Button { Content = "다시 알림", ToolTip = "선택한 시간 뒤 전체 메시지를 다시 알려드려요" };
            later.Click += delegate { data.SnoozeMinutes = (int)((ComboBoxItem)delay.SelectedItem).Tag; snooze(); };
            Button close = new Button { Content = "닫기", ToolTip = "이 알림의 모든 말풍선 닫기" }; close.Click += delegate { dismiss(); };
            foreach (Button button in new Button[] { later, close }) {
                button.Padding = new Thickness(10, 7, 10, 7); button.Margin = new Thickness(4, 0, 0, 0); button.FontSize = 14; buttons.Children.Add(button);
            }
            controls.Children.Add(buttons); stack.Children.Add(controls);
            Canvas tailCanvas = new Canvas { Height = 15, Margin = new Thickness(0, -2, 0, 0) };
            tail.Points = new PointCollection(new Point[] { new Point(0, 0), new Point(12, 14), new Point(24, 0) }); tail.Fill = fill;
            tailEdge.Points = new PointCollection(new Point[] { new Point(0, 0), new Point(12, 14), new Point(24, 0) }); tailEdge.Stroke = outline; tailEdge.StrokeThickness = 2;
            tailCanvas.Children.Add(tail); tailCanvas.Children.Add(tailEdge); root.Children.Add(tailCanvas);
        }
        public Rect Reposition(List<Rect> occupied) {
            Rect area; Point head;
            if (!Pet.TrySpeechAnchorPixels(out area, out head)) return Rect.Empty;
            // The pet and bubble can have different DPI values during monitor moves.
            // Native rectangles keep placement and collision detection in one coordinate space.
            for (int attempt = 0; attempt < 2; attempt++) {
                Rect bounds = ScreenLayout.BoundsPixels(this);
                if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0) return Rect.Empty;
                Rect target = NoticePlacement.Place(area, head, bounds.Size, occupied);
                ScreenLayout.MovePixels(this, target.Left, target.Top);
                UpdateLayout();
            }
            double width = ActualWidth > 0 ? ActualWidth : Width;
            double stem = Math.Max(22, Math.Min(width - 46, PointFromScreen(head).X - 12));
            Canvas.SetLeft(tail, stem); Canvas.SetLeft(tailEdge, stem);
            return ScreenLayout.BoundsPixels(this);
        }
    }
}
