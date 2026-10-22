using System;
using System.IO;
using System.Xml.Serialization;
using ChunsikDesktop;
internal static class TodoSettingsTests {
    static int count;
    static void Check(bool ok, string name) { count++; if (!ok) throw new Exception("FAIL: " + name); }
    public static void Main() {
        XmlSerializer serializer = new XmlSerializer(typeof(TodoSettings));
        TodoSettings settings = new TodoSettings();
        settings.Items.Add(new TodoItem { Text = "", NoteOpacity = 0.45, Minimized = true, FontSize = 34, NoteWidth = 480, NoteHeight = 360 });
        settings.Items.Add(new TodoItem { Text = "첫 줄\n두 번째 줄", FontSize = 13, NoteWidth = 240, NoteHeight = 220 });
        string xml;
        using (StringWriter writer = new StringWriter()) { serializer.Serialize(writer, settings); xml = writer.ToString(); }
        TodoSettings roundtrip; using (StringReader reader = new StringReader(xml)) roundtrip = (TodoSettings)serializer.Deserialize(reader);
        Check(roundtrip.Items[0].NoteOpacity == 0.45 && roundtrip.Items[1].NoteOpacity == 0.6, "opacity persists per note");
        Check(roundtrip.Items[0].Minimized, "minimized state persists");
        Check(!roundtrip.Items[1].Minimized, "new notes default visible");
        Check(!xml.Contains("GhostEnabled"), "removed mode is no longer saved");
        Check(roundtrip.Items.Count == 2, "independent notes retained");
        Check(roundtrip.Items[0].Text == "" && !roundtrip.Items[0].Done, "blank note remains active after reload");
        Check(roundtrip.Items[0].FontSize == 34 && roundtrip.Items[1].FontSize == 13, "font size is per note");
        Check(roundtrip.Items[0].NoteWidth == 480 && roundtrip.Items[1].NoteHeight == 220, "dimensions are per note");
        Check(roundtrip.Items[1].Text == "첫 줄\n두 번째 줄", "inline multiline text retained");
        using (StringReader reader = new StringReader("<TodoSettings><Items><TodoItem><AvoidCursor>true</AvoidCursor><GhostEnabled>true</GhostEnabled><Id>legacy</Id><Text>예전 메모</Text></TodoItem></Items></TodoSettings>")) {
            TodoItem old = ((TodoSettings)serializer.Deserialize(reader)).Items[0];
            Check(!old.Minimized, "legacy minimized default");
            Check(old.NoteOpacity == 0.6, "legacy opacity defaults");
            Check(old.FontSize == 17 && old.NoteWidth == 260 && old.NoteHeight == 280, "old settings receive defaults");
            Check(TodoItem.ValidAppearance(old), "legacy note passes backup validation");
        }
        TodoItem bad = new TodoItem { NoteWidth = Double.NaN }; Check(!TodoItem.ValidAppearance(bad), "NaN size rejected");
        bad.NoteWidth = Double.PositiveInfinity; Check(!TodoItem.ValidAppearance(bad), "infinite size rejected");
        bad.NoteWidth = 260; bad.FontSize = 65; Check(!TodoItem.ValidAppearance(bad), "font bound enforced");
        bad = new TodoItem { NoteOpacity = Double.NaN }; Check(!TodoItem.ValidAppearance(bad), "NaN opacity rejected");
        bad.NoteOpacity = 0.09; Check(!TodoItem.ValidAppearance(bad), "invisible note rejected");
        bad.NoteOpacity = 1.01; Check(!TodoItem.ValidAppearance(bad), "opacity above one rejected");
        bad.NoteOpacity = 0.1; Check(TodoItem.ValidAppearance(bad), "minimum opacity accepted");
        Console.WriteLine("PASS: " + count + " post-it persistence assertions");
    }
}
