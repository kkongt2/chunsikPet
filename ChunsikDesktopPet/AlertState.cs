using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
namespace ChunsikDesktop {
    public sealed class NoticeData {
        public string HistoryId;
        [XmlIgnore] public Action Acknowledge;
        public bool Missed;
        public string ReminderId;
        public bool Interval;
        public string BubbleColor;
        public string Label;
        public string Kind;
        public List<string> Messages;
        public int Seconds;
        public int SnoozeMinutes = 5;
    }

    public class SnoozedNotice {
        public DateTime DueUtc { get; set; }
        public NoticeData Notice { get; set; }
    }
    public class AlertState {
        public DateTime QuietUntilUtc { get; set; }
        public bool QuietIndefinite { get; set; }
        public List<SnoozedNotice> Snoozed { get; set; }
        public List<NoticeData> Waiting { get; set; }
        public AlertState() { Snoozed = new List<SnoozedNotice>(); Waiting = new List<NoticeData>(); }
    }
    public sealed class AlertStateStore {
        static readonly XmlSerializer Serializer = new XmlSerializer(typeof(AlertState));
        public static readonly AlertStateStore Current = new AlertStateStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChunsikDesktopPet", "alert-state.xml"));
        readonly string path;
        public AlertState State { get; private set; }
        public string Warning { get; private set; }
        public AlertStateStore(string filename) {
            path = filename; State = new AlertState();
            if (!File.Exists(path)) return;
            try { State = Read(path); }
            catch {
                try { State = Read(path + ".bak"); Warning = "알림 재개 정보를 이전 백업에서 복원했어요."; }
                catch { Warning = "알림 재개 정보를 읽지 못해 빈 상태로 시작합니다. 기존 파일은 유지합니다."; }
            }
        }
        static bool ValidNotice(NoticeData data) {
            return data != null && data.Messages != null && data.Messages.Count > 0 && data.Messages.Count <= 1000 && data.Seconds >= 3 && data.Seconds <= 60 && data.SnoozeMinutes >= 1 && data.SnoozeMinutes <= 1440;
        }
        static AlertState Read(string filename) {
            AlertState state;
            using (FileStream stream = File.OpenRead(filename)) state = (AlertState)Serializer.Deserialize(stream);
            if (state == null || state.Waiting == null || state.Snoozed == null || state.Waiting.Count > 1000 || state.Snoozed.Count > 1000) throw new InvalidDataException();
            foreach (NoticeData data in state.Waiting) if (!ValidNotice(data)) throw new InvalidDataException();
            foreach (SnoozedNotice snooze in state.Snoozed) if (snooze == null || !ValidNotice(snooze.Notice) || snooze.DueUtc == DateTime.MinValue) throw new InvalidDataException();
            return state;
        }
        public bool Save() {
            string temp = path + ".tmp";
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (FileStream stream = File.Create(temp)) Serializer.Serialize(stream, State);
                if (File.Exists(path)) File.Replace(temp, path, path + ".bak", true); else File.Move(temp, path);
                Warning = null; return true;
            } catch (Exception e) { Warning = "알림 재개 정보 저장 실패: " + e.Message; return false; }
            finally { if (File.Exists(temp)) try { File.Delete(temp); } catch { } }
        }
    }
}
