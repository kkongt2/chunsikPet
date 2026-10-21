using System;
using System.Collections.Generic;
using System.Windows;
namespace ChunsikDesktop {
    // All arguments and results are physical desktop pixels, including negative origins.
    public static class NoticePlacement {
        public static Rect Place(Rect area, Point head, Size size, IList<Rect> occupied) {
            double width = size.Width, height = size.Height;
            double left = Math.Max(area.Left + 4, Math.Min(area.Right - width - 4, head.X - width / 2));
            double top = Math.Max(area.Top + 4, Math.Min(area.Bottom - height - 4, head.Y - height - 3));
            Rect candidate = new Rect(left, top, width, height);
            for (int pass = 0; pass < occupied.Count; pass++) {
                bool moved = false;
                foreach (Rect other in occupied) {
                    if (other.IsEmpty || !other.IntersectsWith(area) || !candidate.IntersectsWith(other)) continue;
                    double above = other.Top - height - 6;
                    if (above >= area.Top + 4) { candidate.Y = above; moved = true; break; }
                    double right = other.Right + 6, besideLeft = other.Left - width - 6;
                    if (right + width <= area.Right - 4) { candidate.X = right; moved = true; break; }
                    if (besideLeft >= area.Left + 4) { candidate.X = besideLeft; moved = true; break; }
                }
                if (!moved) break;
            }
            return candidate;
        }
    }
}
