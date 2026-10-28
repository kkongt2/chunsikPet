using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace ChunsikDesktop {
    public partial class PetWindow {
        public const double GiantSize = -1;
        bool giant;
        double SelectedSize { get { return giant ? GiantSize : size; } }
        readonly TextBlock giantBadge = new TextBlock {
            Text = "초대형 · 고정", FontSize = 17, FontWeight = FontWeights.Bold,
            Background = Brushes.LemonChiffon, Foreground = Brushes.SaddleBrown,
            Padding = new Thickness(12, 5, 12, 5), HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false, Visibility = Visibility.Collapsed
        };
        void FitGiant() {
            Rect area = WorkArea();
            double center = Double.IsNaN(Left) ? area.Left + area.Width / 2 : Left + Width / 2;
            Height = Math.Max(40, area.Height);
            double bodyHeight = Math.Max(1, Height - 36);
            Width = Math.Min(area.Width, Math.Ceiling(bodyHeight * frames[4].PixelWidth / frames[4].PixelHeight + 12));
            size = bodyHeight * maxH / frames[4].PixelHeight;
            Left = Clamp(center - Width / 2, area.Left, area.Right - Width); Top = area.Bottom - Height;
            giantBadge.Visibility = Visibility.Visible;
            ShowGiantFrame();
        }
        void ShowGiantFrame(int index = 4) {
            frame = index;
            picture.Source = reactionKind != 0 && (index == 5 || index == 7) ? reactionFrames[index] : frames[index];
            // Leave room for the shared tickle rotation and hopping inside the fixed window.
            double room = reactionKind == 0 ? 1 : 0.75;
            double scale = room * Math.Min(Math.Max(1, Height - 36) / maxH, Math.Max(1, Width - 12) / maxW);
            if (reactionKind == 0)
                scale = Math.Min(Math.Max(1, Height - 36) / frames[index].PixelHeight, Math.Max(1, Width - 12) / frames[index].PixelWidth);
            picture.Width = frames[index].PixelWidth * scale; picture.Height = frames[index].PixelHeight * scale;
            reactionMark.Margin = new Thickness(0, 0, 0, Math.Max(0, Height - 70));
            flip.ScaleX = flip.ScaleY = 1; squirm.Angle = 0; hop.X = hop.Y = 0;
        }
        void TickGiant(double now) {
            if (ContextMenu != null && ContextMenu.IsOpen) return;
            if (reactionKind != 0) {
                if (now < reactionUntil) { AnimateReaction(now); return; }
                EndReaction();
            }
            ShowGiantFrame();
        }
    }
}
