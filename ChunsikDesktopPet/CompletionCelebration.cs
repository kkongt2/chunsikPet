using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ChunsikDesktop {
    // Embed the user's original artwork without redrawing, cropping or altering its pixels.
    internal sealed class CompletionCelebration : Viewbox {
        static readonly BitmapSource artwork = LoadArtwork();
        readonly TranslateTransform bounce = new TranslateTransform();
        readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        readonly Stopwatch clock = new Stopwatch();
        internal CompletionCelebration() {
            Height = 154; MaxWidth = 240; Stretch = Stretch.Uniform; IsHitTestVisible = false; Focusable = false;
            Margin = new Thickness(0, 8, 0, 10);
            Grid scene = new Grid { Width = 240, Height = 174, Background = new SolidColorBrush(Color.FromRgb(9, 9, 9)), Clip = new RectangleGeometry(new Rect(0, 0, 240, 174), 12, 12) };
            Image image = new Image { Source = artwork, Stretch = Stretch.Uniform, Width = 240, Height = 166, RenderTransform = bounce };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            scene.Children.Add(image); Child = scene;
            timer.Tick += delegate {
                double seconds = clock.Elapsed.TotalSeconds;
                SetPose(seconds);
                if (seconds >= 3.6) Stop();
            };
            Loaded += delegate { SetPose(0); if (SystemParameters.ClientAreaAnimation) { clock.Restart(); timer.Start(); } };
            Unloaded += delegate { Stop(); };
        }
        static BitmapSource LoadArtwork() {
            using (System.IO.Stream stream = typeof(CompletionCelebration).Assembly.GetManifestResourceStream("Assets.chunsik-completion.jpg")) {
                if (stream == null) throw new InvalidOperationException("Completion artwork resource is missing.");
                BitmapImage image = new BitmapImage();
                image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit(); image.Freeze();
                return image;
            }
        }
        internal void Stop() { timer.Stop(); clock.Stop(); SetPose(3.6); }
        internal void SetPose(double seconds) {
            double cycle = seconds >= 0 && seconds < 3.6 ? seconds % 1.2 / 1.2 : 0;
            // Translate the whole image only: no separate facial/body parts or deformations.
            bounce.Y = -3 * Math.Sin(cycle * Math.PI);
        }
    }
}
