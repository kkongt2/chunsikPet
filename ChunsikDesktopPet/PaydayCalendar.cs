using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ChunsikDesktop {
    public static class PaydayCalendar {
        static readonly HashSet<DateTime> Holidays = new HashSet<DateTime>();
        public const int FirstYear = 2026, LastYear = 2040;
        public static void Load(string path) {
            Holidays.Clear();
            foreach (string line in File.ReadAllLines(path)) {
                if (String.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                DateTime day;
                if (!DateTime.TryParseExact(line.Split(',')[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day))
                    throw new InvalidDataException("Invalid offline holiday date.");
                Holidays.Add(day.Date);
            }
            if (Holidays.Count < 300) throw new InvalidDataException("Offline holiday calendar is incomplete.");
        }
        public static bool IsWorkday(DateTime day) {
            return day.DayOfWeek != DayOfWeek.Saturday && day.DayOfWeek != DayOfWeek.Sunday && !Holidays.Contains(day.Date);
        }
        public static bool TryPayday(int year, int month, int configuredDay, out DateTime payday) {
            payday = DateTime.MinValue;
            if (year < FirstYear || year > LastYear || month < 1 || month > 12 || configuredDay < 1 || configuredDay > 31) return false;
            DateTime day = new DateTime(year, month, Math.Min(configuredDay, DateTime.DaysInMonth(year, month)));
            while (!IsWorkday(day)) day = day.AddDays(-1);
            payday = day; return true;
        }
        public static bool IsPayday(DateTime today, int configuredDay) {
            today = today.Date;
            if (today.Year < FirstYear - 1 || today.Year > LastYear) return false;
            DateTime first = new DateTime(today.Year, today.Month, 1);
            // A payday on the first can move into the preceding month/year.
            for (int offset = 0; offset <= 1; offset++) {
                DateTime month = first.AddMonths(offset), payday;
                if (TryPayday(month.Year, month.Month, configuredDay, out payday) && payday == today) return true;
            }
            return false;
        }
        public static string Describe(DateTime today, int configuredDay) {
            DateTime current;
            if (!TryPayday(today.Year, today.Month, configuredDay, out current)) return "공휴일 달력 지원 범위: 2026~2040년";
            string label = "이번 달 월급일: " + current.ToString("yyyy-MM-dd (ddd)", CultureInfo.GetCultureInfo("ko-KR"));
            DateTime month = new DateTime(today.Year, today.Month, 1);
            for (int i = 0; i <= 12; i++) {
                DateTime next;
                DateTime target = month.AddMonths(i);
                if (TryPayday(target.Year, target.Month, configuredDay, out next) && next >= today.Date)
                    return label + "\n다가오는 월급일: " + next.ToString("yyyy-MM-dd (ddd)", CultureInfo.GetCultureInfo("ko-KR"));
            }
            return label;
        }
    }
}
