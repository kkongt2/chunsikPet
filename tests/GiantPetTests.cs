using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml.Serialization;
using ChunsikDesktop;

internal static class GiantPetTests {
    static void Check(bool ok, string message) { if (!ok) throw new Exception("FAIL giant: " + message); }
    static object Call(PetWindow p, string name, params object[] args) { return typeof(PetWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(p, args); }
    static object Field(PetWindow p, string name) { return typeof(PetWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(p); }
    static void Pump() { Application.Current.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(delegate {})); }
    public static void Run() {
        PetWindow pet = new PetWindow(true);
        List<PetWindow> pool = (List<PetWindow>)typeof(PetWindow).GetField("Pets", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        try {
            pet.Show(); Pump(); ((DispatcherTimer)Field(pet, "timer")).Stop();
            pool.Add(pet);
            Call(pet, "ResizePet", PetWindow.GiantSize); Pump();
            Rect area = (Rect)Call(pet, "WorkArea");
            Check(Math.Abs(pet.Height - area.Height) < 2 && Math.Abs(pet.Top - area.Top) < 2, "fits working-area height");
            Check(pet.Width <= area.Width && (bool)Field(pet, "giant"), "fits width and enters giant mode");
            Point before = new Point(pet.Left, pet.Top);
            Call(pet, "BeginGrab", new Point(200, 200), new Point(0.5, 0.5));
            Call(pet, "StartFall");
            Call(pet, "Tick", null, EventArgs.Empty);
            Check(!(bool)Field(pet, "pressed") && !(bool)Field(pet, "dragging") && !(bool)Field(pet, "falling"), "cannot grab or fly");
            Check(new Point(pet.Left, pet.Top) == before, "reaction does not move pet");
            Check(((TextBlock)Field(pet, "giantBadge")).Text.Contains("여기 있을게."), "unique reaction appears");
            List<PetPlacement> saved = PetWindow.CapturePlacements();
            Check(saved.Count == 1 && saved[0].Giant && saved[0].IsValid(), "saved giant placement remains valid");
            XmlSerializer serializer = new XmlSerializer(typeof(PetPlacement));
            using (StringWriter writer = new StringWriter()) {
                serializer.Serialize(writer, saved[0]);
                using (StringReader reader = new StringReader(writer.ToString()))
                    Check(((PetPlacement)serializer.Deserialize(reader)).Giant, "giant flag survives XML round trip");
            }
            Call(pet, "MoveToMonitor", Call(pet, "CurrentScreen"), 0.5); Pump();
            area = (Rect)Call(pet, "WorkArea");
            Check(Math.Abs(pet.Height - area.Height) < 2 && !(bool)Field(pet, "falling"), "monitor move refits without flight");
            Call(pet, "ResizePet", 80.0); Pump();
            Check(!(bool)Field(pet, "giant") && (double)Field(pet, "size") == 80 && ((TextBlock)Field(pet, "giantBadge")).Visibility == Visibility.Collapsed, "return to normal size");
            Call(pet, "StartFall");
            Check((bool)Field(pet, "falling"), "normal flight restored");
        } finally { pool.Remove(pet); pet.Close(); }
        Console.WriteLine("PASS: giant fit, immobility, reaction, persistence and normal-size recovery");
    }
}
