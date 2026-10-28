using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace ChunsikDesktop {
    public partial class PetWindow {
        public const double GiantSize = -1;
        bool giant;
        double giantReactionUntil;
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
        void ShowGiantFrame() {
            frame = 4; picture.Source = frames[4];
            double scale = Math.Min(Math.Max(1, Height - 36) / frames[4].PixelHeight, Math.Max(1, Width - 12) / frames[4].PixelWidth);
            picture.Width = frames[4].PixelWidth * scale; picture.Height = frames[4].PixelHeight * scale;
            flip.ScaleX = flip.ScaleY = 1; squirm.Angle = 0; hop.X = hop.Y = 0;
        }
        void ReactGiant() {
            if (!giant) return;
            giantReactionUntil = clock.Elapsed.TotalSeconds + 1.6;
            giantBadge.Text = "여기 있을게.";
        }
        void TickGiant(double now) {
            ShowGiantFrame();
            if (now < giantReactionUntil) {
                double pulse = Math.Sin((giantReactionUntil - now) * Math.PI * 4);
                flip.ScaleX = flip.ScaleY = 1 - 0.012 * Math.Abs(pulse);
            } else giantBadge.Text = "초대형 · 고정";
        }
    }
}
