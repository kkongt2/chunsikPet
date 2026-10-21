using System;
using ChunsikDesktop;
internal static class ScheduleTests {
    static int count;
    static void Check(bool condition, string name) { count++; if (!condition) throw new Exception("FAIL: " + name); }
    static DateTime D(int day, int hour, int minute) { return new DateTime(2026, 10, day, hour, minute, 0); }
    public static void Main() {
        TimeSpan time; DateTime due;
        Check(ReminderRules.TryTime("23:59", out time), "last minute valid");
        Check(!ReminderRules.TryTime("24:00", out time), "invalid hour rejected");
        Check(!ReminderRules.TryTime("12:60", out time), "invalid minute rejected");
        Reminder r = new Reminder { Time = "12:00", Days = 62 };
        Check(ReminderRules.TryDue(r, D(21,11,59), D(21,12,0), out due), "exact due boundary");
        Check(!ReminderRules.TryDue(r, D(21,12,0), D(21,12,1), out due), "no repeated due");
        Check(!ReminderRules.TryDue(r, D(21,12,0), D(21,11,0), out due), "clock backwards");
        Check(ReminderRules.TryDue(r, D(23,11,0), D(26,10,0), out due) && due == D(23,12,0), "weekend resume recovers Friday");
        r.LastFired = "2026-10-23";
        Check(!ReminderRules.TryDue(r, D(23,11,0), D(26,10,0), out due), "persisted fire deduplicates");
        r.LastFired = ""; r.Enabled = false;
        Check(!ReminderRules.TryDue(r, D(21,11,59), D(21,12,0), out due), "disabled ignored");
        r.Enabled = true; r.ActivityEnabled = true; r.ActiveFrom = "09:00"; r.ActiveUntil = "18:00";
        Check(r.ActivityAllows(D(21,9,0),0), "activity start inclusive");
        Check(!r.ActivityAllows(D(21,18,0),0), "activity end exclusive");
        r.ActiveFrom = "22:00"; r.ActiveUntil = "06:00";
        Check(r.ActivityAllows(D(21,23,0),0) && r.ActivityAllows(D(22,5,59),0), "overnight window");
        Check(!r.ActivityAllows(D(22,6,0),0), "overnight end exclusive");
        r.PauseWhenIdle = true;
        Check(!r.ActivityAllows(D(21,23,0),300), "idle boundary pause");
        Check(r.ActivityAllows(D(21,23,0),299), "active before idle boundary");
        r.ScheduleType = "date"; r.Date = "2026-10-21"; r.HasTime = false;
        Check(ReminderRules.DateDue(r,D(21,10,0),D(21,9,0),false), "startup date notice");
        Check(!ReminderRules.DateDue(r,D(21,10,0),D(20,9,0),false), "untimed date requires same-day startup");
        Check(!ReminderRules.DateDue(r,D(21,10,0),D(21,9,0),true), "untimed once per run");
        r.HasTime = true; r.Time = "12:00";
        Check(!ReminderRules.DateDue(r,D(21,11,59),D(21,9,0),false), "timed date waits");
        Check(ReminderRules.DateDue(r,D(21,12,0),D(21,9,0),false), "timed date boundary");
        r.LastFired = ReminderRules.DateKey(r);
        Check(!ReminderRules.DateDue(r,D(21,12,1),D(21,9,0),false), "timed date persists deduplication");
        Console.WriteLine("PASS: " + count + " scheduling assertions");
    }
}
