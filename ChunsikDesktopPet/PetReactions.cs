using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ChunsikDesktop {
    public partial class PetWindow {
        // Short cues use the existing pet timer; higher-priority activities interrupt them.
        int cueKind;
        double cueStarted, nextLookBack;
        bool wasEvading;
        static ImageSource[] clapFrames;
        readonly TextBlock taskStars = new TextBlock { Text = "✦  ✧  ✦", Foreground = Brushes.Goldenrod, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
        void StartCue(int kind) { CancelCue(); cueKind = kind; cueStarted = clock.Elapsed.TotalSeconds; }
        void CancelCue() {
            if (cueKind == 0) return;
            cueKind = 0; taskStars.Visibility = Visibility.Collapsed;
            flip.ScaleX = flip.ScaleY = 1; squirm.Angle = 0; hop.X = hop.Y = 0;
            SetFrame(paused ? 6 : 4);
        }
        void SetRestMode(bool value) {
            if (paused == value) return;
            EndReaction(); paused = value; wasEvading = false;
            StartCue(value ? 2 : 3);
            SetFrame(value ? 7 : 4);
        }
        public static PetWindow TaskCelebrant(double x, double y) {
            PetWindow chosen = null; double distance = Double.MaxValue;
            foreach (PetWindow pet in Pets) {
                if (!pet.IsVisible || pet.closing || pet.dragging || pet.pressed || pet.falling || pet.sleeping || pet.paydayPlaying || pet.reactionKind != 0 || PresentationPolicy.IsSuppressed) continue;
                if (ReminderService.Current != null && ReminderService.Current.IsSpeakingFor(pet)) continue;
                double dx = pet.Left - x, dy = pet.Top - y, d = dx * dx + dy * dy;
                if (d < distance) { chosen = pet; distance = d; }
            }
            return chosen;
        }
        public void CelebrateTask() { StartCue(4); }
        static void BuildClapFrames() {
            if (clapFrames != null) return;
            double w = frames[4].PixelWidth, h = frames[4].PixelHeight;
            Rect full = new Rect(0, 0, w, h);
            Rect left = new Rect(0, h * 0.63, w * 0.28, h * 0.22);
            Rect right = new Rect(w * 0.72, h * 0.63, w * 0.28, h * 0.22);
            Geometry body = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(full), new GeometryGroup { Children = { new RectangleGeometry(left), new RectangleGeometry(right) } });
            clapFrames = new ImageSource[8];
            for (int i = 0; i < clapFrames.Length; i++) {
                double close = (1 - Math.Cos(i * Math.PI * 2 / clapFrames.Length)) / 2;
                DrawingGroup drawing = new DrawingGroup();
                using (DrawingContext dc = drawing.Open()) {
                    dc.DrawRectangle(Brushes.Transparent, null, full);
                    dc.PushClip(body); dc.DrawImage(frames[4], full); dc.Pop();
                    for (int arm = 0; arm < 2; arm++) {
                        dc.PushTransform(new TranslateTransform((arm == 0 ? 1 : -1) * w * (0.08 + close * 0.18), -h * 0.035));
                        dc.PushClip(new RectangleGeometry(arm == 0 ? left : right));
                        dc.DrawImage(frames[4], full); dc.Pop(); dc.Pop();
                    }
                }
                drawing.Freeze(); DrawingImage image = new DrawingImage(drawing); image.Freeze(); clapFrames[i] = image;
            }
        }
        bool AnimateCue(double now) {
            if (cueKind == 0) return false;
            double t = Math.Max(0, now - cueStarted);
            double duration = cueKind == 1 ? 0.65 : cueKind == 2 ? 0.8 : cueKind == 3 ? 1.1 : 1.25;
            if (t >= duration) {
                int done = cueKind; CancelCue();
                if (done == 3 && !paused) { state = 0; stateEnd = now + 4; SetFrame(0); }
                else { state = paused ? 2 : 1; stateEnd = now + 0.6; }
                return false;
            }
            double u = t / duration;
            if (cueKind == 1) {
                SetFrame(paused ? 6 : 0); flip.ScaleX = paused ? 1 : -direction;
                squirm.Angle = -direction * 5 * Math.Sin(u * Math.PI);
            } else if (cueKind == 2) {
                // Hands up to tie the band, then settle into the banded seated pose.
                SetFrame(u < 0.45 ? 7 : 6);
                flip.ScaleY = 1 - 0.08 * Math.Sin(u * Math.PI);
                squirm.Angle = u < 0.45 ? 3 * Math.Sin(u * Math.PI * 4) : 0;
            } else if (cueKind == 3) {
                SetFrame(u < 0.2 || u > 0.85 ? 4 : 7);
                flip.ScaleY = 1 + 0.04 * Math.Sin(u * Math.PI);
                flip.ScaleX = 1 - 0.04 * Math.Sin(u * Math.PI);
            } else {
                BuildClapFrames(); SetFrame(4); picture.Source = clapFrames[(int)(t * 24) % clapFrames.Length];
                taskStars.FontSize = Math.Max(12, size * 0.22); taskStars.Visibility = Visibility.Visible;
                taskStars.Opacity = Math.Sin(u * Math.PI);
                taskStars.RenderTransform = new TranslateTransform(0, (1 - u) * size * 0.12);
                flip.ScaleY = 1 - 0.025 * Math.Abs(Math.Sin(t * 12));
            }
            return true;
        }
    }
}
