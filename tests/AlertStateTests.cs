using System;
using System.IO;
using System.Collections.Generic;
using ChunsikDesktop;
internal static class AlertStateTests {
    static int count;
    static void Check(bool ok, string name) { count++; if (!ok) throw new Exception("FAIL: " + name); }
    public static void Main() {
        string folder = Path.Combine(Path.GetTempPath(), "chunsik-state-" + Guid.NewGuid().ToString("N"));
        string file = Path.Combine(folder, "state.xml");
        try {
            AlertStateStore store = new AlertStateStore(file);
            DateTime due = DateTime.UtcNow.AddMinutes(5), quiet = DateTime.UtcNow.AddMinutes(30);
            NoticeData data = new NoticeData { Label = "다시 알림", Messages = new List<string> { "첫 문구", "다음 문구" }, Seconds = 7, SnoozeMinutes = 5, Acknowledge = delegate { } };
            store.State.Snoozed.Add(new SnoozedNotice { DueUtc = due, Notice = data });
            store.State.Waiting.Add(data); store.State.QuietUntilUtc = quiet;
            Check(store.Save(), "atomic save");
            AlertStateStore restart = new AlertStateStore(file);
            Check(restart.State.Snoozed.Count == 1 && restart.State.Snoozed[0].DueUtc == due, "snooze absolute due survives restart");
            Check(restart.State.Snoozed[0].Notice.Messages.Count == 2, "all snoozed messages survive");
            Check(restart.State.Waiting.Count == 1, "DND waiting queue restored");
            Check(restart.State.QuietUntilUtc == quiet, "DND absolute end survives restart");
            Check(restart.State.Waiting[0].Acknowledge == null, "runtime callback is not serialized");
            restart.State.QuietIndefinite = true; Check(restart.Save(), "indefinite DND save");
            Check(new AlertStateStore(file).State.QuietIndefinite, "indefinite DND restored");
            File.WriteAllText(file, "broken");
            AlertStateStore recovered = new AlertStateStore(file);
            Check(recovered.State.Snoozed.Count == 1 && recovered.Warning != null, "corrupt primary falls back to backup");
            Console.WriteLine("PASS: " + count + " alert recovery assertions");
        } finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
}
