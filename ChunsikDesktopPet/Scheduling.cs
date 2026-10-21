using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Serialization;
namespace ChunsikDesktop {
    public class Reminder {
        public string Id { get; set; }
        public string Kind { get; set; }
        public string BubbleColor { get; set; }
        [XmlIgnore] public bool IsBuiltin { get { return Kind == "lunch" || Kind == "home" || Kind == "water" || Kind == "stretch"; } }
        public string ScheduleType { get; set; }
        public int IntervalMinutes { get; set; }
        public bool ActivityEnabled { get; set; }
        public string ActiveFrom { get; set; }
        public string ActiveUntil { get; set; }
        public bool PauseWhenIdle { get; set; }
        public bool ActivityAllows(DateTime now, double idleSeconds) {
            if (PauseWhenIdle && idleSeconds >= 300) return false;
            if (!ActivityEnabled) return true;
            TimeSpan from, until;
            if (!ReminderRules.TryTime(ActiveFrom, out from) || !ReminderRules.TryTime(ActiveUntil, out until) || from == until) return false;
            TimeSpan t = now.TimeOfDay;
            return from < until ? t >= from && t < until : t >= from || t < until;
        }
        public string Date { get; set; }
        public bool HasTime { get; set; }
        [XmlIgnore] public bool IsDate { get { return ScheduleType == "date"; } }
        [XmlIgnore] public bool IsInterval { get { return ScheduleType == "interval"; } }
        public bool Enabled { get; set; }
        public string Title { get; set; }
        public string Time { get; set; }
        public string Repeat { get; set; } // Legacy XML compatibility.
        public int Days { get; set; } // Sunday bit 0 through Saturday bit 6; -1 migrates legacy Repeat.
        [XmlIgnore] public int SelectedDays {
            get { return Days >= 0 ? Days : Repeat == "매일" ? 127 : Repeat == "주말" ? 65 : 62; }
        }
        public bool HasDay(int day) { return (SelectedDays & (1 << day)) != 0; }
        public void SetDay(int day, bool enabled) {
            int mask = SelectedDays;
            Days = enabled ? mask | (1 << day) : mask & ~(1 << day);
        }
        public string LastFired { get; set; }
        public List<string> Messages { get; set; }
        [XmlIgnore] public int MessageCount { get { return GetMessages().Count; } }
        public List<string> GetMessages() {
            List<string> result = new List<string>();
            if (Messages != null) foreach (string message in Messages)
                if (!String.IsNullOrWhiteSpace(message)) result.Add(message.Trim());
            if (result.Count == 0 && !String.IsNullOrWhiteSpace(Title)) result.Add(Title.Trim());
            return result;
        }
        public Reminder() { Id = Guid.NewGuid().ToString("N"); ScheduleType = "time"; IntervalMinutes = 60; ActiveFrom = "09:00"; ActiveUntil = "18:00"; Enabled = true; Title = "새 알림"; Time = "15:00"; Repeat = "평일"; Days = -1; LastFired = ""; Messages = new List<string>(); }
        public Reminder Copy() {
            Reminder copy = (Reminder)MemberwiseClone(); copy.Messages = GetMessages(); copy.Days = SelectedDays; return copy;
        }
    }
    public static class ReminderRules {
        public static bool SameSchedule(Reminder a, Reminder b) {
            if (a.ScheduleType != b.ScheduleType) return false;
            if (a.IsDate) return a.Date == b.Date && a.HasTime == b.HasTime && (!a.HasTime || a.Time == b.Time);
            if (a.IsInterval) return a.IntervalMinutes == b.IntervalMinutes;
            return a.Time == b.Time;
        }
        public static string Remaining(DateTime now, DateTime due) {
            long seconds = (long)Math.Ceiling(Math.Max(0, (due-now).TotalSeconds));
            return (seconds / 3600).ToString("00") + "시간 " + (seconds / 60 % 60).ToString("00") + "분 " + (seconds % 60).ToString("00") + "초";
        }
        public static DateTime? NextDue(Reminder item, DateTime now, DateTime previous, DateTime started, bool seen, DateTime? interval, double idle, out string state) {
            state = "다음 알림";
            if (!item.Enabled) { state = "알림 꺼짐"; return null; }
            DateTime date; TimeSpan time;
            if (item.IsDate) {
                if (!TryDate(item.Date, out date)) { state = "날짜를 선택하세요"; return null; }
                if (date < now.Date) { state = "지난 날짜 · 알림 종료"; return null; }
                if (!item.HasTime) {
                    if (DateDue(item, now, started, seen)) return now;
                    state = date == now.Date && seen ? "이번 실행에서 알림 완료" : item.Date + " 프로그램 시작 시";
                    return null;
                }
                if (!TryTime(item.Time, out time)) { state = "시간을 확인하세요"; return null; }
                if (item.LastFired == DateKey(item)) { state = "알림 완료"; return null; }
                return date.Add(time) > now ? date.Add(time) : now;
            }
            if (item.SelectedDays == 0) { state = "반복 요일 미선택"; return null; }
            if (item.IsInterval) {
                if (!OnDay(item, now)) { state = "오늘은 반복 요일이 아니에요"; return null; }
                if (item.PauseWhenIdle && idle >= 300) { state = "자리 비움 · 입력 재개 후 계산"; return null; }
                if (!item.ActivityAllows(now, idle)) { state = "활동 시간 밖 · 복귀 후 계산"; return null; }
                if (!interval.HasValue) { state = "간격 알림 준비 중"; return null; }
                return interval.Value > now ? interval.Value : now;
            }
            DateTime missed;
            if (TryDue(item, previous, now, out missed)) return now;
            if (!TryTime(item.Time, out time)) { state = "시간을 확인하세요"; return null; }
            for (int i=0; i<=7; i++) {
                DateTime candidate = now.Date.AddDays(i).Add(time);
                if (candidate >= now && OnDay(item, candidate) && item.LastFired != candidate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) return candidate;
            }
            state = "다음 일정 없음"; return null;
        }
        public static bool TryTime(string value, out TimeSpan time) {
            return TimeSpan.TryParseExact(value == null ? "" : value.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out time)
                && time >= TimeSpan.Zero && time < TimeSpan.FromDays(1);
        }
        public static bool TryDate(string value, out DateTime date) {
            return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }
        public static string DateKey(Reminder item) { return "date:" + item.Date + ":" + item.Time; }
        public static bool DateDue(Reminder item, DateTime now, DateTime started, bool seen) {
            DateTime date; TimeSpan time;
            if (!item.Enabled || !item.IsDate || !TryDate(item.Date, out date) || date.Date != now.Date) return false;
            if (!item.HasTime) return started.Date == date.Date && !seen;
            return TryTime(item.Time, out time) && now >= date.Add(time) && item.LastFired != DateKey(item);
        }
        public static bool OnDay(Reminder item, DateTime day) {
            return item.HasDay((int)day.DayOfWeek);
        }
        // Recover the latest eligible occurrence per reminder after a gap, up to seven days.
        public static bool TryDue(Reminder item, DateTime previous, DateTime now, out DateTime due) {
            due = now;
            if (item.IsDate || item.IsInterval || !item.Enabled || now < previous) return false;
            TimeSpan time;
            if (!TryTime(item.Time, out time)) return false;
            if (now - previous > TimeSpan.FromDays(7)) previous = now.AddDays(-7);
            due = now.Date.Add(time);
            if (due > now) due = due.AddDays(-1);
            for (int i = 0; i < 7 && due > previous; i++, due = due.AddDays(-1))
                if (OnDay(item, due) && due <= now && item.LastFired != due.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) return true;
            return false;
        }
    }

}
