using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Xml;
using System.Xml.Serialization;
namespace ChunsikDesktop {
    public class SettingsBundle {
        public int Format { get; set; } public string Version { get; set; }
        public ReminderSettings Reminders { get; set; } public TodoSettings Todos { get; set; } public PetOptions Pets { get; set; }
        public bool Fullscreen { get; set; }
        public uint HotkeyModifiers { get; set; }
        public uint HotkeyKey { get; set; }
    }
    public static class SettingsBackup {
        public static bool Restoring { get; private set; }
        static readonly string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChunsikDesktopPet");
        static bool Ready() {
            SettingsHub.Close();
            if (ReminderService.Current.SettingsOpen || TodoService.Current.SettingsOpen || PetPreferences.Current.SettingsOpen) { MessageBox.Show("설정 창을 닫아 변경사항을 저장한 뒤 다시 실행해 주세요.", "설정 백업·복원"); return false; } return true;
        }
        static byte[] Bytes(object value) { using (MemoryStream m = new MemoryStream()) { new XmlSerializer(value.GetType()).Serialize(m, value); return m.ToArray(); } }
        public static void Export() {
            if (!Ready()) return;
            Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog { Filter = "춘식이 설정 백업|*.chunsik", FileName = "Chunsik-settings-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".chunsik" };
            if (dialog.ShowDialog() != true) return;
            try { SettingsBundle bundle = new SettingsBundle { Format = 2, HotkeyModifiers = TodoHotKey.SavedModifiers, HotkeyKey = TodoHotKey.SavedKey, Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(), Reminders = ReminderService.Current.Snapshot(), Todos = TodoService.Current.Snapshot(), Pets = PetPreferences.Current.Options, Fullscreen = PresentationPolicy.FullscreenEnabled };
                File.WriteAllBytes(dialog.FileName, Bytes(bundle)); MessageBox.Show("알림·포스트잇(완료 보관함 포함)·캐릭터·단축키 설정을 백업했어요.");
            } catch (Exception e) { Diagnostics.Record("설정 백업", e); MessageBox.Show("백업 실패: " + e.Message); }
        }
        static void Validate(SettingsBundle b) {
            if (b == null || (b.Format != 1 && b.Format != 2) || b.Reminders == null || b.Todos == null || b.Pets == null || !PetPreferences.Valid(b.Pets)) throw new InvalidDataException("지원하지 않거나 잘못된 백업입니다.");
            if (b.Reminders.Items == null || b.Reminders.Items.Count > 500 || b.Reminders.MessageSeconds < 3 || b.Reminders.MessageSeconds > 60 || b.Reminders.SnoozeMinutes < 1 || b.Reminders.SnoozeMinutes > 1440) throw new InvalidDataException("알림 설정 범위 오류");
            if (b.Format == 2 && !TodoHotKey.Valid(b.HotkeyModifiers, b.HotkeyKey)) throw new InvalidDataException("잘못된 단축키 설정");
            HashSet<string> ids = new HashSet<string>();
            foreach (Reminder r in b.Reminders.Items) {
                TimeSpan time, from, until; DateTime date;
                if (r == null || String.IsNullOrEmpty(r.Id) || !ids.Add(r.Id) || String.IsNullOrWhiteSpace(r.Title) || !ReminderRules.TryTime(r.Time, out time) || r.SelectedDays < 0 || r.SelectedDays > 127 || (r.ScheduleType != "time" && r.ScheduleType != "interval" && r.ScheduleType != "date") || (r.IsInterval && (r.IntervalMinutes < 1 || r.IntervalMinutes > 1439)) || (r.IsDate && !ReminderRules.TryDate(r.Date, out date))) throw new InvalidDataException("잘못된 알림 항목");
                if (r.ActivityEnabled && (!ReminderRules.TryTime(r.ActiveFrom, out from) || !ReminderRules.TryTime(r.ActiveUntil, out until) || from == until)) throw new InvalidDataException("활동 시간 오류");
            }
            if (b.Todos.Items == null || !b.Todos.ValidCounts()) throw new InvalidDataException("할 일 개수 오류"); ids.Clear();
            foreach (TodoItem t in b.Todos.Items) if (t == null || String.IsNullOrEmpty(t.Id) || !ids.Add(t.Id) || t.Text == null || t.Text.Length > 160 || !TodoItem.ValidAppearance(t) || t.EveryMinutes < 1 || t.EveryMinutes > 1440 || t.DisplaySeconds < 1 || t.DisplaySeconds > 3600 || Double.IsNaN(t.NoteLeft) || Double.IsInfinity(t.NoteLeft) || Double.IsNaN(t.NoteTop) || Double.IsInfinity(t.NoteTop)) throw new InvalidDataException("잘못된 할 일 항목");
        }
        public static string Preview(SettingsBundle b) {
            Validate(b);
            return "백업 버전: " + b.Version + "\n알림: " + b.Reminders.Items.Count + "개\n진행 중 포스트잇: " + b.Todos.CountItems(false) + "개\n완료 보관함: " + b.Todos.CountItems(true) + "개\n캐릭터: " + b.Pets.PetCount + "마리\n전체 화면 자동 숨김: " + (b.Fullscreen ? "켜짐" : "꺼짐") +
                "\n단축키: " + (b.Format == 2 ? TodoHotKey.Format(b.HotkeyModifiers, b.HotkeyKey) : "미포함 · 현재 단축키 유지") +
                "\n\n현재 설정을 위 내용으로 바꿉니다. 알림 이력과 실행 중인 타이머는 복원하지 않습니다.\n복원 후 프로그램이 종료되므로 EXE를 다시 실행해 주세요.\n단축키 충돌 여부는 재실행 시 확인합니다. 계속할까요?";
        }
        static Dictionary<string, byte[]> BuildFiles(SettingsBundle bundle) {
            Validate(bundle);
            Dictionary<string, byte[]> files = new Dictionary<string, byte[]> { { "reminders.xml", Bytes(bundle.Reminders) }, { "todos.xml", Bytes(bundle.Todos) }, { "pets.xml", Bytes(bundle.Pets) }, { "fullscreen.txt", System.Text.Encoding.UTF8.GetBytes(bundle.Fullscreen ? "on" : "off") } };
            if (bundle.Format == 2) files["hotkey.txt"] = System.Text.Encoding.UTF8.GetBytes(bundle.HotkeyModifiers + "," + bundle.HotkeyKey);
            return files;
        }
        public static void Import() {
            if (!Ready()) return;
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog { Filter = "춘식이 설정 백업|*.chunsik" };
            if (dialog.ShowDialog() != true) return;
            try {
                if (new FileInfo(dialog.FileName).Length > 10 * 1024 * 1024) throw new InvalidDataException("백업 파일이 너무 큽니다.");
                SettingsBundle bundle;
                using (XmlReader reader = XmlReader.Create(dialog.FileName, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null })) bundle = (SettingsBundle)new XmlSerializer(typeof(SettingsBundle)).Deserialize(reader);
                Validate(bundle);
                if (MessageBox.Show(Preview(bundle), "설정 복원", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                Dictionary<string, byte[]> files = BuildFiles(bundle);
                Dictionary<string, byte[]> originals = new Dictionary<string, byte[]>(); Directory.CreateDirectory(folder);
                foreach (string name in files.Keys) originals[name] = File.Exists(Path.Combine(folder, name)) ? File.ReadAllBytes(Path.Combine(folder, name)) : null;
                string backup = Path.Combine(folder, "before-restore-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")); Directory.CreateDirectory(backup);
                foreach (string name in originals.Keys) if (originals[name] != null) File.WriteAllBytes(Path.Combine(backup, name), originals[name]);
                try { foreach (string name in files.Keys) { string target = Path.Combine(folder, name); File.WriteAllBytes(target + ".restore", files[name]); if (File.Exists(target)) File.Replace(target + ".restore", target, target + ".bak"); else File.Move(target + ".restore", target); } }
                catch { foreach (string name in originals.Keys) { string target = Path.Combine(folder, name); if (originals[name] == null) File.Delete(target); else File.WriteAllBytes(target, originals[name]); } throw; }
                Restoring = true; // Prevent live timers and shutdown saves from overwriting restored files.
                MessageBox.Show("설정을 복원했어요. EXE를 다시 실행해 주세요.\n이전 설정은 " + backup + "에 보관했어요."); PetWindow.Quit();
            } catch (Exception e) { Diagnostics.Record("설정 복원", e); MessageBox.Show("복원 실패: " + e.Message); }
        }
    }
}

