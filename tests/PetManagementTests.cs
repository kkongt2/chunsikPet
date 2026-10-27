using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml.Serialization;
using ChunsikDesktop;

internal static class PetManagementTests {
    static void Check(bool condition, string message) { if (!condition) throw new Exception("FAIL management: " + message); }
    static object Call(PetWindow pet, string name, params object[] args) { return typeof(PetWindow).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(pet, args); }
    static object Field(PetWindow pet, string name) { return typeof(PetWindow).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(pet); }
    static void Pump() { Application.Current.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(delegate {})); }
    public static void Run() {
        XmlSerializer serializer = new XmlSerializer(typeof(PetPlacement));
        PetPlacement legacy;
        using (StringReader reader = new StringReader("<PetPlacement><Size>80</Size><CenterX>-500</CenterX><BottomY>1080</BottomY><Paused>true</Paused></PetPlacement>")) legacy = (PetPlacement)serializer.Deserialize(reader);
        Check(legacy.RestoredSpeed == 44 && !legacy.HasRelativePosition && legacy.Paused && legacy.IsValid(), "old XML retains placement and mode with default speed");
        foreach (double invalid in new double[] { 0, -1, Double.NaN, Double.PositiveInfinity }) {
            legacy.Speed = invalid; Check(legacy.RestoredSpeed == 44, "invalid speed falls back without losing placement");
        }
        PetPlacement original = new PetPlacement { Size = 270, Speed = 80, MonitorDevice = "DISPLAY2", RelativeX = 0.3, RelativeY = 1, Paused = true, CenterX = -800, BottomY = 1080 };
        string xml;
        using (StringWriter writer = new StringWriter()) { serializer.Serialize(writer, original); xml = writer.ToString(); }
        using (StringReader reader = new StringReader(xml)) {
            PetPlacement read = (PetPlacement)serializer.Deserialize(reader);
            Check(read.RestoredSpeed == 80 && read.HasRelativePosition && read.RelativeX == 0.3 && read.Paused, "new XML round trip");
        }
        original.RelativeX = Double.NaN; Check(!original.HasRelativePosition, "invalid relative coordinates use legacy fallback");
        Rect negative = new Rect(-2560, -200, 2560, 1400);
        Point at = PlacementGeometry.Position(negative, new Size(200, 300), 0.25, 1);
        Check(at == new Point(-1970, 900), "negative monitor origin and bottom alignment");
        Check(PlacementGeometry.Fraction(at.X, negative.Left, negative.Width - 200) == 0.25, "capture/restore relative round trip");
        Check(PlacementGeometry.Position(new Rect(0, 0, 800, 600), new Size(1000, 900), 1, 1) == new Point(0, 0), "oversized pet remains anchored on small display");
        Check(PlacementGeometry.Position(new Rect(1920, 0, 1920, 1080), new Size(300, 450), 0.5, 1) == new Point(2730, 630), "new DPI pixel dimensions stay within destination");

        List<PetWindow> pool = (List<PetWindow>)typeof(PetWindow).GetField("Pets", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        PetWindow pet = new PetWindow(true), other = new PetWindow(true);
        Window owner = new Window();
        try {
            pet.Show(); other.Show(); Pump();
            ((DispatcherTimer)Field(pet, "timer")).Stop(); ((DispatcherTimer)Field(other, "timer")).Stop();
            pool.Add(pet); pool.Add(other);
            Call(pet, "SetSpeed", 80.0); Call(other, "SetSpeed", 22.0);
            List<PetPlacement> captured = PetWindow.CapturePlacements();
            Check(captured.Count == 2 && captured[0].Speed == 80 && captured[1].Speed == 22 && captured[0].HasRelativePosition, "capture independent speeds and monitor placement");
            Call(pet, "SetSpeed", 10.0); Check((double)Field(pet, "speed") == 80, "unsupported speed rejected");
            PetWindow.SpeedAll(22); Check((double)Field(pet, "speed") == 22 && (double)Field(other, "speed") == 22, "all speed action updates every pet");
            Call(pet, "RestorePlacement", captured[0]); Pump();
            Check((double)Field(pet, "speed") == 80, "restore applies stored speed");
            Call(pet, "SetRestMode", true); Call(pet, "CancelCue");
            object screen = Call(pet, "CurrentScreen");
            double size = (double)Field(pet, "size");
            Call(pet, "MoveToMonitor", screen, 0.5); Pump();
            Rect bounds = ScreenLayout.BoundsPixels(pet), area = ScreenLayout.AreaPixels(pet);
            Check(Math.Abs(bounds.Bottom - area.Bottom) < 2 && Math.Abs((bounds.Left + bounds.Width / 2) - (area.Left + area.Width / 2)) < 2, "explicit move centers pet at working-area bottom");
            Check((bool)Field(pet, "paused") && (double)Field(pet, "speed") == 80 && (double)Field(pet, "size") == size, "move preserves rest mode, speed and size");
            Check(!(bool)Field(pet, "movingMonitor") && !(bool)Field(pet, "falling"), "move finishes without flight");
            captured[0].MonitorDevice = "disconnected-monitor";
            Call(pet, "RestorePlacement", captured[0]); Pump();
            bounds = ScreenLayout.BoundsPixels(pet); area = ScreenLayout.AreaPixels(pet);
            Check(area.Contains(bounds.TopLeft), "missing saved monitor falls back to visible desktop");
            Border card = (Border)PetWindow.BuildManagementPanel(owner);
            StackPanel panel = (StackPanel)card.Child; ListBox list = (ListBox)panel.Children[2];
            Check(list.Items.Count == 2 && ((ListBoxItem)list.Items[0]).Content.ToString().Contains("빠르게"), "manager shows live per-pet state");
            list.SelectedIndex = 1;
            Check(((Border)Field(other, "managementHighlight")).HasAnimatedProperties, "selecting row highlights the correct pet");
            WrapPanel actions = (WrapPanel)panel.Children[3];
            ((Button)actions.Children[4]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check((double)Field(other, "speed") == 80, "manager speed button targets selected pet");
        } finally { pool.Remove(pet); pool.Remove(other); pet.Close(); other.Close(); owner.Close(); }
        Console.WriteLine("PASS: pet management, migration and monitor placement");
    }
}
