using System;
using System.Collections.Generic;
using System.Windows;
namespace ChunsikDesktop {
    public static class TodoAvoidance {
        public static bool Near(Rect note, Point cursor, double margin) {
            note.Inflate(margin, margin); return note.Contains(cursor);
        }
        public static Point Target(Rect note, Rect area, Point cursor, IList<Rect> occupied) {
            const double gap = 110;
            List<Point> candidates = new List<Point>();
            candidates.Add(new Point(cursor.X + gap, note.Top));
            candidates.Add(new Point(cursor.X - gap - note.Width, note.Top));
            candidates.Add(new Point(note.Left, cursor.Y + gap));
            candidates.Add(new Point(note.Left, cursor.Y - gap - note.Height));
            for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++)
                candidates.Add(new Point(area.Left + Math.Max(0, area.Width - note.Width) * x / 2, area.Top + Math.Max(0, area.Height - note.Height) * y / 2));
            Point best = note.TopLeft; double bestScore = Double.MaxValue;
            foreach (Point candidate in candidates) {
                Point p = new Point(Math.Max(area.Left, Math.Min(area.Right - note.Width, candidate.X)), Math.Max(area.Top, Math.Min(area.Bottom - note.Height, candidate.Y)));
                Rect r = new Rect(p, note.Size);
                if (Near(r, cursor, 90)) continue;
                double overlap = 0;
                foreach (Rect other in occupied) { Rect intersection = Rect.Intersect(r, other); if (!intersection.IsEmpty) overlap += intersection.Width * intersection.Height; }
                Vector distance = p - note.TopLeft;
                // Prefer nearby clear space, but a crowded desktop must not disable avoidance.
                double score = distance.LengthSquared + overlap * 1000;
                if (score < bestScore) { bestScore = score; best = p; }
            }
            return best;
        }
    }
}
