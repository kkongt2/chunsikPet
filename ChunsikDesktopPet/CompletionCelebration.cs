using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ChunsikDesktop {
    // Vector artwork stays crisp at every monitor DPI. Animation belongs only to the undo toast.
    internal sealed class CompletionCelebration : Viewbox {
        readonly Canvas scene = new Canvas { Width = 240, Height = 140, ClipToBounds = true };
        readonly RotateTransform armRotation = new RotateTransform(0, 126, 104);
        readonly TranslateTransform bounce = new TranslateTransform();
        readonly Rectangle[] confetti = new Rectangle[12];
        readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        readonly Stopwatch clock = new Stopwatch();
        static readonly Brush ink = BrushFor("#382B25"), gold = BrushFor("#FFDC79");
        static readonly string[] colors = { "#F3A949", "#8ACD72", "#69BED5", "#EF8D9B" };
        internal CompletionCelebration() {
            Height = 112; MaxWidth = 240; Stretch = Stretch.Uniform; IsHitTestVisible = false; Focusable = false;
            Margin = new Thickness(0, 4, 0, 6); Child = scene;
            Canvas pet = new Canvas { RenderTransform = bounce }; scene.Children.Add(pet);
            AddPath(pet, "M 102,87 Q 93,106 105,129 Q 135,138 173,128 L 179,89 Z", gold);
            AddPath(pet, "M 105,120 Q 139,129 173,118 L 173,129 Q 135,138 105,129 Z", BrushFor("#B98C63"), 0);
            Path arm = AddPath(pet, "M 129,103 Q 107,91 91,74 Q 81,62 73,71 Q 65,80 76,94 Q 96,115 118,120 Z", gold);
            arm.RenderTransform = armRotation;
            // Rounded ears, two tiny crown tufts, blush, white muzzle and happy open mouth.
            AddPath(pet, "M 92,34 Q 89,7 110,12 Q 120,12 124,20 L 144,15 L 149,9 L 155,16 L 161,11 L 168,21 Q 193,13 196,36 Q 209,57 201,82 Q 191,106 150,105 Q 110,107 92,89 Q 76,70 92,34 Z", gold);
            EllipseAt(pet, 94, 63, 28, 23, BrushFor("#F4A08B")); EllipseAt(pet, 170, 63, 28, 23, BrushFor("#F4A08B"));
            EllipseAt(pet, 120, 51, 7, 9, ink); EllipseAt(pet, 163, 51, 7, 9, ink);
            AddPath(pet, "M 137,79 Q 135,99 147,96 Q 158,92 154,79 Z", BrushFor("#E67E7D"), 3);
            AddPath(pet, "M 128,76 Q 125,63 142,62 Q 158,61 163,74 Q 164,84 153,83 L 145,80 Q 131,88 128,76 Z", Brushes.White, 3);
            AddPath(pet, "M 128,69 L 121,65 M 128,78 L 120,80 M 162,70 L 170,67 M 163,78 L 171,82", null, 3);
            // Confetti-filled bowl and the hand holding its rim.
            AddPath(pet, "M 111,109 Q 113,136 162,133 Q 184,129 186,108 Z", BrushFor("#FFFBE9"));
            EllipseAt(pet, 109, 99, 78, 22, BrushFor("#FFFEF5"), 3);
            for (int i = 0; i < 5; i++) {
                Rectangle piece = new Rectangle { Width = 12, Height = 12, RadiusX = 2, RadiusY = 2, Fill = BrushFor(colors[i % 4]), RenderTransform = new RotateTransform(i * 13 - 25, 6, 6) };
                Canvas.SetLeft(piece, 118 + i * 12); Canvas.SetTop(piece, 102 + i % 2 * 2); pet.Children.Add(piece);
            }
            AddPath(pet, "M 111,109 Q 145,127 185,109", null, 3);
            AddPath(pet, "M 182,111 Q 195,109 195,122 Q 192,138 179,133 Q 169,130 174,118 Z", gold, 3);
            for (int i = 0; i < confetti.Length; i++) {
                Rectangle piece = new Rectangle { Width = 8, Height = 10, RadiusX = 1.5, RadiusY = 1.5, Fill = BrushFor(colors[i % 4]), Stroke = Brushes.White, StrokeThickness = 1, Opacity = 0 };
                confetti[i] = piece; scene.Children.Add(piece);
            }
            timer.Tick += delegate {
                double seconds = clock.Elapsed.TotalSeconds;
                SetPose(seconds);
                if (seconds >= 3.6) Stop();
            };
            Loaded += delegate { SetPose(0); if (SystemParameters.ClientAreaAnimation) { clock.Restart(); timer.Start(); } };
            Unloaded += delegate { Stop(); };
        }
        internal void Stop() { timer.Stop(); clock.Stop(); SetPose(3.6); }
        internal void SetPose(double seconds) {
            bool active = seconds >= 0 && seconds < 3.6;
            double cycle = active ? seconds % 1.2 / 1.2 : 0;
            double wave = Math.Sin(cycle * Math.PI);
            armRotation.Angle = -38 * wave;
            bounce.Y = -3 * wave;
            for (int i = 0; i < confetti.Length; i++) {
                double t = (cycle - (i % 4) * 0.045) / 0.82;
                Rectangle piece = confetti[i];
                piece.Opacity = active && t >= 0 && t <= 1 ? Math.Min(1, (1 - t) * 4) : 0;
                t = Math.Max(0, Math.Min(1, t));
                Canvas.SetLeft(piece, 121 - (62 + i * 4) * t);
                Canvas.SetTop(piece, 102 - (190 + i % 3 * 28) * t + 220 * t * t);
                piece.RenderTransform = new RotateTransform(i * 31 + t * 240, 4, 5);
            }
        }
        static Brush BrushFor(string color) { Brush b = (Brush)new BrushConverter().ConvertFromString(color); b.Freeze(); return b; }
        static Path AddPath(Canvas parent, string data, Brush fill, double width = 3.5) {
            Path path = new Path { Data = Geometry.Parse(data), Fill = fill, Stroke = ink, StrokeThickness = width, StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
            parent.Children.Add(path); return path;
        }
        static void EllipseAt(Canvas parent, double x, double y, double w, double h, Brush fill, double stroke = 0) {
            Ellipse ellipse = new Ellipse { Width = w, Height = h, Fill = fill, Stroke = ink, StrokeThickness = stroke };
            Canvas.SetLeft(ellipse, x); Canvas.SetTop(ellipse, y); parent.Children.Add(ellipse);
        }
    }
}
