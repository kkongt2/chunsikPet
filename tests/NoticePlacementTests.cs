using System;
using System.Collections.Generic;
using System.Windows;
using ChunsikDesktop;
internal static class NoticePlacementTests {
    static int count;
    static void Check(bool ok, string name) { count++; if (!ok) throw new Exception("FAIL: " + name); }
    static Rect Place(Rect area, Point head, double scale, List<Rect> occupied) {
        Rect r = NoticePlacement.Place(area, head, new Size(310*scale, 200*scale), occupied);
        Check(area.Contains(r), "bubble inside owning monitor " + area);
        foreach (Rect other in occupied) Check(!r.IntersectsWith(other), "bubbles do not overlap");
        occupied.Add(r); return r;
    }
    public static void Main() {
        Rect[] screens = { new Rect(-1920, 0, 1920, 1040), new Rect(0, 0, 2560, 1400), new Rect(2560, -400, 1920, 1040) };
        List<Rect> occupied = new List<Rect>();
        for (int i=0; i<3; i++) {
            Rect a = screens[i];
            Place(a, new Point(a.Left+a.Width/2, a.Bottom-90), 1+i*0.5, occupied);
        }
        // Move the same bubble through all monitors and change DPI in both directions.
        foreach (int i in new int[] { 2, 0, 1, 2, 1, 0 }) {
            Rect a = screens[i]; occupied.Clear();
            Place(a, new Point(a.Right-10, a.Bottom-100), 1+i*0.5, occupied);
            occupied.Clear(); Place(a, new Point(a.Left+10, a.Bottom-100), 1+i*0.5, occupied);
        }
        occupied.Clear(); Rect middle = screens[1];
        for (int i=0; i<3; i++) Place(middle, new Point(1200, 1300), 1.25, occupied);
        occupied.Clear(); Rect above = new Rect(-1080, -1920, 1080, 1880);
        Place(above, new Point(-20, -70), 2, occupied);
        occupied.Clear(); Place(above, new Point(-1000, -1910), 1, occupied);
        occupied.Clear(); Place(middle, new Point(100, middle.Bottom+500), 1, occupied);
        // A bubble on another monitor must not displace this bubble.
        Rect alone = NoticePlacement.Place(screens[0], new Point(-1000, 900), new Size(310, 200), new List<Rect>());
        occupied.Clear(); occupied.Add(new Rect(100, 100, 1000, 1000));
        Check(alone == NoticePlacement.Place(screens[0], new Point(-1000, 900), new Size(310, 200), occupied), "different monitor collision ignored");
        Console.WriteLine("PASS: " + count + " multi-monitor placement assertions");
    }
}
