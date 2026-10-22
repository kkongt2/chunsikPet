using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ChunsikDesktop {
    public class PetWindow : Window {
        static readonly Random Rng = new Random();
        static readonly List<PetWindow> Pets = new List<PetWindow>();
        static BitmapSource[] frames;
        static ImageSource[] seatedScootFrames;
        bool scooting;
        static BitmapSource[] reactionFrames;
        static BitmapSource[] noticeFrames;
        static BitmapSource[] waterFrames;
        static BitmapSource[] stretchFrames;
        static double stretchW, stretchH, stretchBodyH;
        bool stretching;
        static BitmapSource[] paydayFrames;
        static double paydayW, paydayH, paydayBodyH;
        bool paydayPlaying;
        double paydayStarted, paydayRemaining;
        bool noticeAnimating;
        static BitmapSource sleepingFrame;
        static TrayController trayController;
        public static BitmapSource PreviewPose(string kind, int tick) {
            if (kind == "water") return waterFrames[tick % waterFrames.Length];
            if (kind == "stretch") return stretchFrames[tick % stretchFrames.Length];
            if (kind == "lunch") return noticeFrames[tick % 4];
            if (kind == "home") return noticeFrames[4 + tick % 4];
            return frames[tick % 2 == 0 ? 4 : 7];
        }
        public static BitmapSource ToolPose(bool wave) { return frames[wave ? 7 : 4]; }
        public static BitmapSource ToolCelebrationPose(double seconds) {
            int pose = (int)(seconds / 0.24) % 3 == 2 ? 7 : 5;
            return reactionFrames == null ? ToolPose(true) : reactionFrames[pose];
        }
        public static BitmapSource TrayImage { get { return frames[4]; } }
        public static bool AllPaused {
            get { if (Pets.Count == 0) return false; foreach (PetWindow pet in Pets) if (!pet.paused) return false; return true; }
        }
        public static void PauseAll(bool value) { foreach (PetWindow pet in Pets) { pet.EndReaction(); pet.paused = value; } }
        public static void ResizeAll(double value) { foreach (PetWindow pet in Pets) pet.ResizePet(value); }
        public static void SpeedAll(double value) { foreach (PetWindow pet in Pets) pet.speed = value; }
        public static int Count { get { return Pets.Count; } }
        public static void ApplyAppearance() {
            if (PetPreferences.Current == null) return;
            foreach (PetWindow pet in Pets) PresentationPolicy.SetOpacity(pet, 1.0 - PetPreferences.Current.Options.TransparencyPercent / 100.0);
        }
        double fleeUntil;
        int fleeDirection;
        static double maxW, maxH;
        static bool exiting;
        public static bool IsExiting { get { return exiting; } }
        public bool HasClosed { get { return closing; } }
        readonly Image picture = new Image();
        readonly Viewbox parachute = new Viewbox { IsHitTestVisible = false, Visibility = Visibility.Collapsed, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top };
        readonly ScaleTransform clothScale = new ScaleTransform(1, 1);
        readonly RotateTransform clothTilt = new RotateTransform();
        bool falling;
        double fallStart, fallCenter, landedAt = -1;
        Rect fallArea;
        readonly TextBlock sleepMark = new TextBlock { Text = "Z z", FontWeight = FontWeights.Bold, FontSize = 15, Foreground = new SolidColorBrush(Color.FromRgb(100, 89, 140)), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
        readonly TextBlock reactionMark = new TextBlock { FontWeight = FontWeights.Bold, FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(164, 90, 81)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 3, 0, 0), IsHitTestVisible = false, Visibility = Visibility.Collapsed };
        readonly RotateTransform squirm = new RotateTransform();
        readonly TranslateTransform hop = new TranslateTransform();
        readonly Queue<double> recentClicks = new Queue<double>();
        double reactionStart, reactionUntil, pressedAt;
        int reactionKind;
        bool sleeping;
        readonly double sleepPhase = Rng.NextDouble() * 6.28;
        readonly ScaleTransform flip = new ScaleTransform(1, 1);
        readonly DispatcherTimer timer = new DispatcherTimer();
        readonly Stopwatch clock = Stopwatch.StartNew();
        double lastTime, stateEnd, nextFrame, speed = 44, size = 80;
        int frame, direction = 1, state;
        bool paused, dragging, closing;
        System.Windows.Point down, origin;
        bool pressed;

        [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] struct MONITORINFO {
            public int Size; public RECT Monitor, Work; public uint Flags;
        }
        [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
        [DllImport("user32.dll")] static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint="GetWindowLong")] static extern int GetWindowLong32(IntPtr hwnd, int index);
        [DllImport("user32.dll")] static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
        [DllImport("user32.dll", EntryPoint="SetWindowLong")] static extern int SetWindowLong32(IntPtr hwnd, int index, int value);

        static void LoadFrames(string path) {
            BitmapImage raw = new BitmapImage();
            raw.BeginInit(); raw.UriSource = new Uri(path); raw.CacheOption = BitmapCacheOption.OnLoad; raw.EndInit();
            FormatConvertedBitmap bitmap = new FormatConvertedBitmap(raw, PixelFormats.Bgra32, null, 0);
            int w = bitmap.PixelWidth, h = bitmap.PixelHeight, stride = w * 4;
            byte[] pixels = new byte[stride * h]; bitmap.CopyPixels(pixels, stride, 0);
            frames = new BitmapSource[8]; maxW = maxH = 1;
            for (int k = 0; k < 8; k++) {
                int x0 = k % 4 * w / 4, x1 = (k % 4 + 1) * w / 4;
                int y0 = k / 4 * h / 2, y1 = (k / 4 + 1) * h / 2;
                int left = x1, right = x0, top = y1, bottom = y0;
                for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) {
                    if (pixels[y * stride + x * 4 + 3] < 40) continue;
                    left = Math.Min(left, x); right = Math.Max(right, x);
                    top = Math.Min(top, y); bottom = Math.Max(bottom, y);
                }
                if (left > right || top > bottom) throw new InvalidDataException("Empty sprite cell " + k);
                // Keep the alpha edge around each actual sprite, normalize all using one scale.
                left = Math.Max(x0, left - 2); right = Math.Min(x1 - 1, right + 2);
                top = Math.Max(y0, top - 2); bottom = Math.Min(y1 - 1, bottom + 2);
                frames[k] = new CroppedBitmap(bitmap, new Int32Rect(left, top, right-left+1, bottom-top+1));
                frames[k].Freeze(); maxW = Math.Max(maxW, frames[k].PixelWidth); maxH = Math.Max(maxH, frames[k].PixelHeight);
            }
        }

        // Reuse the seated sprite at its original resolution. Only the two feet
        // deform sideways; the head, arms and contact height stay fixed.
        static void BuildSeatedScootFrames() {
            BitmapSource seated = frames[6]; double w = seated.PixelWidth, h = seated.PixelHeight;
            seatedScootFrames = new ImageSource[12];
            for (int i = 0; i < seatedScootFrames.Length; i++) {
                double wave = Math.Sin(i * Math.PI * 2 / seatedScootFrames.Length);
                DrawingGroup drawing = new DrawingGroup();
                using (DrawingContext dc = drawing.Open()) {
                    Rect full = new Rect(0, 0, w, h);
                    // A transparent full-size drawing keeps every frame's layout identical.
                    dc.DrawRectangle(Brushes.Transparent, null, full);
                    double seam = h * 0.80, middle = w * 0.5;
                    for (int foot = 0; foot < 2; foot++) {
                        double x = foot == 0 ? 0 : middle;
                        dc.PushClip(new RectangleGeometry(new Rect(x, seam, w * 0.5, h - seam)));
                        dc.PushTransform(new ScaleTransform(1 + wave * (foot == 0 ? 0.09 : -0.09), 1, middle, seam));
                        dc.DrawImage(seated, full); dc.Pop(); dc.Pop();
                    }
                    // Slight overlap hides the cut while leaving the seated body untouched.
                    dc.PushClip(new RectangleGeometry(new Rect(0, 0, w, seam + 1.5)));
                    dc.DrawImage(seated, full); dc.Pop();
                }
                drawing.Freeze(); DrawingImage image = new DrawingImage(drawing); image.Freeze(); seatedScootFrames[i] = image;
            }
        }
        void ShowSeatedScoot(double now) {
            SetFrame(6); picture.Source = seatedScootFrames[(int)(now * 18) % seatedScootFrames.Length];
            scooting = true; flip.ScaleX = flip.ScaleY = 1; squirm.Angle = 0; hop.X = hop.Y = 0;
        }
        void StopSeatedScoot() { if (scooting) SetFrame(6); }

        static void LoadSleepingFrame(string path) {
            if (!File.Exists(path)) { sleepingFrame = frames[5]; return; }
            BitmapImage raw = new BitmapImage(); raw.BeginInit(); raw.UriSource = new Uri(path); raw.CacheOption = BitmapCacheOption.OnLoad; raw.EndInit();
            FormatConvertedBitmap bitmap = new FormatConvertedBitmap(raw, PixelFormats.Bgra32, null, 0);
            int width = bitmap.PixelWidth, height = bitmap.PixelHeight, stride = width * 4;
            byte[] pixels = new byte[stride * height]; bitmap.CopyPixels(pixels, stride, 0);
            int left = width, top = height, right = -1, bottom = -1;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) {
                if (pixels[y * stride + x * 4 + 3] < 40) continue;
                left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = Math.Max(bottom, y);
            }
            if (right < left) { sleepingFrame = frames[5]; return; }
            left = Math.Max(0, left - 2); top = Math.Max(0, top - 2); right = Math.Min(width - 1, right + 2); bottom = Math.Min(height - 1, bottom + 2);
            sleepingFrame = new CroppedBitmap(bitmap, new Int32Rect(left, top, right-left+1, bottom-top+1)); sleepingFrame.Freeze();
        }
        void ShowSleep(double now) {
            sleeping = true; timer.Interval = TimeSpan.FromMilliseconds(100); picture.Source = sleepingFrame; flip.ScaleX = 1;
            double width = Math.Min(Width - 6, size * 1.1);
            picture.Width = width; picture.Height = width * sleepingFrame.PixelHeight / sleepingFrame.PixelWidth;
            flip.ScaleY = 1 + 0.025 * Math.Sin(now * 1.8 + sleepPhase);
            sleepMark.Visibility = Visibility.Visible;
            // Anchor beside the sleeping head, rather than the standing window's top corner.
            sleepMark.FontSize = Math.Max(11, Math.Min(28, size * 0.17));
            sleepMark.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
            double markLeft = (Width - picture.Width) / 2 + picture.Width * 0.62;
            double markBottom = picture.Height * 0.78 + 2;
            sleepMark.Margin = new Thickness(
                Clamp(markLeft, 2, Width - sleepMark.DesiredSize.Width - 2), 0, 0,
                Clamp(markBottom, 2, Height - sleepMark.DesiredSize.Height - 2));
            sleepMark.Opacity = 0.55 + 0.4 * (0.5 + 0.5 * Math.Sin(now * 1.3 + sleepPhase));
        }
        void Wake() {
            if (!sleeping) return;
            sleeping = false; timer.Interval = TimeSpan.FromMilliseconds(40); flip.ScaleY = 1; sleepMark.Visibility = Visibility.Collapsed;
            state = 1; stateEnd = clock.Elapsed.TotalSeconds + 0.8; SetFrame(4);
        }
        public static void SetCount(int desired) {
            desired = Math.Max(1, Math.Min(20, desired));
            while (Pets.Count > desired) Pets[Pets.Count - 1].Close();
            while (Pets.Count < desired) new PetWindow().Show();
        }
        public readonly bool IsTodoCompanion;
        public static List<PetWindow> TodoPets() { return new List<PetWindow>(Pets); }
        public PetWindow() : this(false) { }
        public PetWindow(bool todoCompanion) {
            IsTodoCompanion = todoCompanion;
            Width = 110; Height = 90;
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true; Background = Brushes.Transparent;
            Topmost = true; ShowInTaskbar = false; ShowActivated = false;
            Title = "춘식이 데스크톱 펫"; UseLayoutRounding = true;
            Grid root = new Grid(); root.Background = Brushes.Transparent;
            picture.Stretch = Stretch.Fill; picture.HorizontalAlignment = HorizontalAlignment.Center;
            picture.VerticalAlignment = VerticalAlignment.Bottom;
            picture.RenderTransformOrigin = new Point(0.5, 1);
            TransformGroup effects = new TransformGroup(); effects.Children.Add(flip); effects.Children.Add(squirm); effects.Children.Add(hop);
            picture.RenderTransform = effects;
            BuildParachute(); root.Children.Add(parachute); root.Children.Add(picture); root.Children.Add(sleepMark); root.Children.Add(reactionMark); Content = root;
            RenderOptions.SetBitmapScalingMode(picture, BitmapScalingMode.HighQuality);
            MouseRightButtonDown += delegate { if (PetPreferences.Current != null) PetPreferences.Current.RefreshIdle(); Wake(); EndReaction(); recentClicks.Clear(); };
            MouseLeftButtonDown += OnDown; MouseMove += OnMove; MouseLeftButtonUp += OnUp;
            LostMouseCapture += delegate { if (pressed) { recentClicks.Clear(); FinishDrag(); } };
            ContextMenu = BuildMenu();
            Loaded += delegate {
                IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                // Tool window + no activation: walking never steals keyboard focus.
                if (IntPtr.Size == 8) SetWindowLongPtr(hwnd, -20, new IntPtr(GetWindowLongPtr(hwnd, -20).ToInt64() | 0x08000080L));
                else SetWindowLong32(hwnd, -20, GetWindowLong32(hwnd, -20) | 0x08000080);
                ResizePet(80);
                if (!IsTodoCompanion && PetPreferences.Current != null) PresentationPolicy.SetOpacity(this, 1.0 - PetPreferences.Current.Options.TransparencyPercent / 100.0);
                Rect area = WorkArea(); Left = area.Left + Rng.NextDouble() * Math.Max(0, area.Width - Width);
                Top = area.Bottom - Height; ChooseState(); timer.Start();
            };
            Closed += delegate { closing = true; timer.Stop(); Pets.Remove(this); if (!IsTodoCompanion && Pets.Count == 0) Quit(); };
            timer.Interval = TimeSpan.FromMilliseconds(40); timer.Tick += Tick;
            if (!IsTodoCompanion) Pets.Add(this);
        }

        void BuildParachute() {
            Canvas cloth = new Canvas { Width = 200, Height = 200 };
            Brush edge = new SolidColorBrush(Color.FromRgb(163, 174, 188));
            // Soft white cloth with a scalloped hem, seam lines and tied corner cords.
            System.Windows.Shapes.Path ropes = new System.Windows.Shapes.Path {
                Data = Geometry.Parse("M 14,77 L 69,177 M 57,86 L 69,177 M 143,86 L 131,177 M 186,77 L 131,177"),
                Stroke = edge, StrokeThickness = 2, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
            };
            cloth.Children.Add(ropes);
            System.Windows.Shapes.Path canopy = new System.Windows.Shapes.Path {
                Data = Geometry.Parse("M 14,77 C 20,38 51,12 100,12 C 149,12 180,38 186,77 Q 165,68 143,86 Q 122,75 100,88 Q 78,75 57,86 Q 35,68 14,77 Z"),
                Fill = new LinearGradientBrush(Colors.White, Color.FromRgb(229, 237, 245), 90), Stroke = edge, StrokeThickness = 2
            };
            cloth.Children.Add(canopy);
            cloth.Children.Add(new System.Windows.Shapes.Path {
                Data = Geometry.Parse("M 100,12 C 75,30 62,54 57,86 M 100,12 L 100,88 M 100,12 C 125,30 138,54 143,86"),
                Stroke = new SolidColorBrush(Color.FromRgb(215, 223, 233)), StrokeThickness = 1.5
            });
            foreach (double x in new double[] { 14, 186 }) {
                System.Windows.Shapes.Ellipse knot = new System.Windows.Shapes.Ellipse { Width = 7, Height = 6, Fill = Brushes.White, Stroke = edge, StrokeThickness = 1 };
                Canvas.SetLeft(knot, x - 3.5); Canvas.SetTop(knot, 74); cloth.Children.Add(knot);
            }
            parachute.Child = cloth; parachute.RenderTransformOrigin = new Point(0.5, 0.9);
            TransformGroup transform = new TransformGroup(); transform.Children.Add(clothScale); transform.Children.Add(clothTilt);
            parachute.RenderTransform = transform;
        }
        void StartFall() {
            double feet = Top + Height, center = Left + Width / 2;
            Wake(); EndReaction(); StopNoticeAnimation();
            falling = true; fallArea = WorkArea(); fallStart = clock.Elapsed.TotalSeconds; landedAt = -1;
            fallCenter = center; timer.Interval = TimeSpan.FromMilliseconds(25);
            LayoutPet(); Left = Clamp(center - Width / 2, fallArea.Left, fallArea.Right - Width);
            fallCenter = Left + Width / 2; Top = feet - Height;
            SetFrame(7); parachute.Visibility = Visibility.Visible;
            clothScale.ScaleX = 0.15; clothScale.ScaleY = 0.1; parachute.Opacity = 0.3;
        }
        void StopFall(bool landed) {
            if (!falling) return;
            double feet = Top + Height, center = Left + Width / 2;
            falling = false; landedAt = -1; parachute.Visibility = Visibility.Collapsed;
            squirm.Angle = 0; hop.X = 0; hop.Y = 0; flip.ScaleX = 1; flip.ScaleY = 1;
            timer.Interval = TimeSpan.FromMilliseconds(40); LayoutPet();
            Left = Clamp(center - Width / 2, fallArea.Left, fallArea.Right - Width);
            Top = landed ? fallArea.Bottom - Height : feet - Height;
            state = 1; stateEnd = clock.Elapsed.TotalSeconds + 0.7; SetFrame(4);
        }
        void AnimateFall(double now, double dt) {
            double elapsed = now - fallStart;
            double opened = Math.Min(1, elapsed / 0.4);
            double wave = Math.Sin(elapsed * 2.5);
            SetFrame(7); squirm.Angle = wave * 4; clothTilt.Angle = wave * 5;
            if (landedAt < 0) {
                clothScale.ScaleX = 0.15 + 0.85 * opened; clothScale.ScaleY = 0.1 + 0.9 * opened;
                parachute.Opacity = 0.3 + 0.7 * opened;
                Left = Clamp(fallCenter - Width / 2 + wave * Math.Min(18, size * 0.10), fallArea.Left, fallArea.Right - Width);
                double target = fallArea.Bottom - Height;
                Top = Math.Min(target, Top + (45 + Math.Min(size * 0.25, 65)) * dt);
                if (Top >= target) { Top = target; landedAt = now; }
            } else {
                double fold = Math.Max(0, 1 - (now - landedAt) / 0.35);
                clothScale.ScaleX = fold; clothScale.ScaleY = fold; parachute.Opacity = fold;
                squirm.Angle = 0; flip.ScaleY = 1 - 0.06 * Math.Sin((1 - fold) * Math.PI);
                if (fold <= 0) StopFall(true);
            }
        }

        Rect WorkArea() { return ScreenLayout.AreaFor(this); }
        public void RecoverDisplay() {
            if (!IsLoaded || closing || pressed || dragging) return;
            falling = false; dragging = false; pressed = false; ReleaseMouseCapture(); parachute.Visibility = Visibility.Collapsed;
            Rect area = WorkArea(); LayoutPet(); Left = Clamp(Left, area.Left, area.Right - Width); Top = Math.Max(area.Top, area.Bottom - Height);
        }

        void LayoutPet() {
            double center = IsLoaded && !Double.IsNaN(Left) ? Left + Width / 2 : SystemParameters.WorkArea.Left + Width / 2;
            Width = Math.Ceiling(size * maxW / maxH + 12 + (reactionKind == 0 ? 0 : size * 0.45));
            Height = size + 6 + (reactionKind == 0 ? 0 : size * 0.22 + 18);
            if (stretching) {
                Width = Math.Max(Width, size * stretchW / stretchBodyH + 12);
                Height = Math.Max(Height, size * stretchH / stretchBodyH + 6);
            }
            if (paydayPlaying) {
                Width = Math.Max(Width, size * paydayW / paydayBodyH + 12);
                Height = Math.Max(Height, size * paydayH / paydayBodyH + 6);
            }
            if (falling) {
                Width = Math.Max(Width, size * 1.72 + 12); Height = size * 2.05 + 6;
                parachute.Width = size * 1.6; parachute.Height = size * 1.6;
            }
            reactionMark.FontSize = Math.Max(11, size * 0.16);
            if (IsLoaded) { Rect area = WorkArea(); Top = area.Bottom - Height; Left = Clamp(center - Width / 2, area.Left, area.Right - Width); }
        }
        void ResizePet(double value) {
            double feet = Top + Height;
            size = value; LayoutPet(); SetFrame(frame);
            if (falling) { Top = Math.Min(feet, fallArea.Bottom) - Height; fallCenter = Left + Width / 2; }
        }
        void StartTickle() {
            double now = clock.Elapsed.TotalSeconds;
            while (recentClicks.Count > 0 && now - recentClicks.Peek() > 2) recentClicks.Dequeue();
            recentClicks.Enqueue(now);
            // The gesture belongs to this pet; clicking another pet does not build its counter.
            reactionKind = recentClicks.Count >= 3 || (reactionKind == 2 && now < reactionUntil) ? 2 : 1;
            reactionStart = now; reactionUntil = now + (reactionKind == 2 ? 3.2 : 1.4);
            reactionMark.Text = reactionKind == 2 ? "ㅋㅋㅋ!" : "";
            reactionMark.Visibility = reactionKind == 2 ? Visibility.Visible : Visibility.Collapsed;
            LayoutPet(); AnimateReaction(now);
        }
        void AnimateReaction(double now) {
            double elapsed = now - reactionStart;
            double fade = Math.Min(1, Math.Max(0, (reactionUntil - now) / 0.35));
            bool laughing = reactionKind == 2;
            // Dedicated squeezed-chevron eyes in both tickle poses; keep the mouthless face.
            int pose = laughing ? (((int)(elapsed / 0.24) % 3 == 2) ? 7 : 5) : (((int)(elapsed / 0.3) % 2 == 0) ? 5 : 7);
            SetFrame(pose);
            double wave = Math.Sin(elapsed * (laughing ? 25 : 18));
            squirm.Angle = wave * (laughing ? 10 : 5) * fade;
            hop.X = wave * size * (laughing ? 0.035 : 0.018) * fade;
            hop.Y = -Math.Abs(Math.Sin(elapsed * (laughing ? 12 : 8))) * size * (laughing ? 0.12 : 0.035) * fade;
            flip.ScaleX = 1 + 0.025 * wave * fade;
            flip.ScaleY = 1 - (laughing ? 0.06 : 0.025) * Math.Abs(wave) * fade;
            reactionMark.Opacity = 0.8 + 0.2 * Math.Abs(wave);
        }
        void EndReaction() {
            if (reactionKind == 0) return;
            reactionKind = 0; squirm.Angle = 0; hop.X = 0; hop.Y = 0; flip.ScaleX = 1; flip.ScaleY = 1;
            reactionMark.Visibility = Visibility.Collapsed; LayoutPet();
            state = 1; stateEnd = clock.Elapsed.TotalSeconds + 0.6; SetFrame(4);
        }
        static double Clamp(double n, double lo, double hi) { return Math.Max(lo, Math.Min(Math.Max(lo, hi), n)); }
        void SetFrame(int index) {
            scooting = false; frame = index;
            picture.Source = reactionKind != 0 && (index == 5 || index == 7) ? reactionFrames[index] : frames[index];
            picture.Width = size * frames[index].PixelWidth / maxH;
            picture.Height = size * frames[index].PixelHeight / maxH;
            flip.ScaleX = index < 4 ? direction : 1;
        }
        void ChooseState() {
            state = Rng.Next(100) < 72 ? 0 : Rng.Next(1, 4);
            if (state == 0) { direction = Rng.Next(2) == 0 ? -1 : 1; SetFrame(0); }
            else SetFrame(state == 1 ? 4 : state == 2 ? 6 : 7);
            stateEnd = clock.Elapsed.TotalSeconds + (state == 0 ? 4 + Rng.NextDouble() * 10 : 2 + Rng.NextDouble() * 4);
        }
        public static void PreviewPayday() {
            foreach (PetWindow pet in Pets) pet.BeginPayday();
        }
        void BeginPayday() {
            // Dragging and parachuting remain under the user's control.
            if (pressed || dragging || falling) return;
            EndReaction(); StopNoticeAnimation(); Wake();
            paydayPlaying = true; paydayRemaining = 12; paydayStarted = clock.Elapsed.TotalSeconds;
            LayoutPet(); AnimatePayday(paydayStarted, 0);
        }
        void EndPayday() {
            if (!paydayPlaying) return;
            paydayPlaying = false; squirm.Angle = 0; hop.X = 0; hop.Y = 0; flip.ScaleX = 1; flip.ScaleY = 1;
            LayoutPet(); state = 1; stateEnd = clock.Elapsed.TotalSeconds + 0.6; SetFrame(4);
        }
        void AnimatePayday(double now, double dt) {
            paydayRemaining -= dt;
            if (paydayRemaining <= 0) { EndPayday(); return; }
            int index = (int)((now - paydayStarted) / 0.24) % 8;
            BitmapSource sprite = paydayFrames[index]; frame = 4; picture.Source = sprite;
            // Use one scale for all frames so flying notes never shrink the body.
            picture.Width = size * sprite.PixelWidth / paydayBodyH;
            picture.Height = size * sprite.PixelHeight / paydayBodyH;
            flip.ScaleX = flip.ScaleY = 1; squirm.Angle = 0; hop.X = hop.Y = 0;
        }
        void StopNoticeAnimation() {
            if (!noticeAnimating) return;
            if (stretching) { stretching = false; LayoutPet(); }
            noticeAnimating = false; squirm.Angle = 0; hop.X = 0; hop.Y = 0; flip.ScaleX = 1; flip.ScaleY = 1;
            SetFrame(4);
        }
        void AnimateNotice(string kind, double now) {
            noticeAnimating = true;
            if (stretching != (kind == "stretch")) { stretching = kind == "stretch"; LayoutPet(); }
            bool lunch = kind == "lunch";
            int index = (int)(now / (lunch ? 0.4 : 0.18)) % 4 + (lunch ? 0 : 4);
            BitmapSource sprite = kind == "stretch" ? stretchFrames[(int)(now / 0.45) % 8] : kind == "water" ? waterFrames[(int)(now / 0.38) % 8] : noticeFrames[index];
            frame = 4; picture.Source = sprite;
            picture.Height = kind == "stretch" ? size * sprite.PixelHeight / stretchBodyH : size * frames[4].PixelHeight / maxH;
            picture.Width = picture.Height * sprite.PixelWidth / sprite.PixelHeight;
            flip.ScaleX = 1; flip.ScaleY = lunch ? 1 - 0.015 * Math.Abs(Math.Sin(now * 4)) : 1;
            squirm.Angle = 0; hop.X = 0;
            hop.Y = (lunch || kind == "water" || kind == "stretch") ? 0 : -Math.Abs(Math.Sin(now * 8)) * Math.Min(3, size * 0.025);
        }
        void Tick(object sender, EventArgs args) {
            double now = clock.Elapsed.TotalSeconds, dt = Math.Min(0.1, now - lastTime); lastTime = now;
            if (closing || pressed || dragging || (ContextMenu != null && ContextMenu.IsOpen)) return;
            if (falling) { AnimateFall(now, dt); return; }
            string noticeKind = ReminderService.Current == null ? "" : ReminderService.Current.AnimationKindFor(this);
            bool specialNotice = noticeKind == "lunch" || noticeKind == "home" || noticeKind == "water" || noticeKind == "stretch";
            if (paydayPlaying) { AnimatePayday(now, dt); return; }
            if (!specialNotice) StopNoticeAnimation();
            if (!specialNotice && PetPreferences.Current != null && PetPreferences.Current.IsIdle) { EndReaction(); recentClicks.Clear(); ShowSleep(now); return; }
            Wake();
            if (reactionKind != 0) {
                if (now < reactionUntil) { AnimateReaction(now); return; }
                EndReaction();
            }
            if (specialNotice) { AnimateNotice(noticeKind, now); return; }
            if (ReminderService.Current != null && ReminderService.Current.IsSpeakingFor(this)) {
                if (frame != 7) SetFrame(7); return;
            }
            if (TodoService.Current != null && TodoService.Current.IsHoldingFor(this)) { if (frame != 4) SetFrame(4); return; }
            if (EvadeCursor(now, dt, paused || state == 2)) return;
            if (paused) { if (frame != 6) SetFrame(6); return; }
            Rect area = WorkArea();
            Top = area.Bottom - Height;
            if (now >= stateEnd) ChooseState();
            if (state == 0) {
                Left = Clamp(Left + direction * speed * dt, area.Left, area.Right - Width);
                if (Left <= area.Left) direction = 1;
                else if (Left >= area.Right - Width) direction = -1;
                if (now >= nextFrame) { SetFrame((frame + 1) % 4); nextFrame = now + 0.14; }
            } else if (state == 1) SetFrame(now % 4 > 3.65 ? 5 : 4);
        }
        bool EvadeCursor(double now, double dt, bool seated) {
            if (IsTodoCompanion || PresentationPolicy.IsSuppressed || PetPreferences.Current == null || !PetPreferences.Current.Options.AvoidCursor) { fleeUntil = 0; StopSeatedScoot(); return false; }
            System.Drawing.Point screen = System.Windows.Forms.Cursor.Position;
            Point cursor = PointFromScreen(new Point(screen.X, screen.Y));
            double dx = Math.Max(0, Math.Max(-cursor.X, cursor.X - Width));
            double dy = Math.Max(0, Math.Max(-cursor.Y, cursor.Y - Height));
            bool near = dx < 70 && dy < 65;
            Rect area = WorkArea();
            if (near) {
                if (now >= fleeUntil) fleeDirection = cursor.X < Width / 2 ? 1 : -1;
                if (fleeDirection < 0 && Left <= area.Left + 2) fleeDirection = 1;
                else if (fleeDirection > 0 && Left + Width >= area.Right - 2) fleeDirection = -1;
                fleeUntil = now + 0.35;
            }
            if (now >= fleeUntil) { StopSeatedScoot(); return false; }
            direction = fleeDirection;
            // Use the same escape speed in both poses; seated pets keep their scoot animation.
            double escapeSpeed = Math.Max(240, speed * 4);
            Left = Clamp(Left + direction * escapeSpeed * dt, area.Left, area.Right - Width);
            Top = area.Bottom - Height;
            if (seated) { state = 2; stateEnd = now + 0.8; ShowSeatedScoot(now); return true; }
            state = 1; stateEnd = now + 0.5;
            if (now >= nextFrame) { SetFrame(frame < 4 ? (frame + 1) % 4 : 0); nextFrame = now + 0.07; }
            return true;
        }
        void OnDown(object sender, MouseButtonEventArgs e) {
            if (!picture.IsMouseOver) return;
            if (PetPreferences.Current != null) PetPreferences.Current.RefreshIdle(); Wake();
            EndPayday(); StopFall(false); StopNoticeAnimation();
            pressed = true; dragging = false; pressedAt = clock.Elapsed.TotalSeconds;
            down = PointToScreen(e.GetPosition(this)); origin = ScreenLayout.BoundsPixels(this).TopLeft; CaptureMouse();
            e.Handled = true;
        }
        void OnMove(object sender, MouseEventArgs e) {
            if (!pressed || e.LeftButton != MouseButtonState.Pressed) return;
            Point at = PointToScreen(e.GetPosition(this));
            PresentationSource src = PresentationSource.FromVisual(this);
            if (src == null || src.CompositionTarget == null) return;
            Vector delta = at - down;
            Vector localDelta = src.CompositionTarget.TransformFromDevice.Transform(delta);
            if (!dragging && (Math.Abs(localDelta.X) >= SystemParameters.MinimumHorizontalDragDistance || Math.Abs(localDelta.Y) >= SystemParameters.MinimumVerticalDragDistance)) {
                dragging = true;
                EndReaction(); recentClicks.Clear();
                // Resizing the reaction window must not shift the drag origin.
                origin = ScreenLayout.BoundsPixels(this).TopLeft;
            }
            if (dragging) { SetFrame(7); ScreenLayout.MovePixels(this, origin.X + delta.X, origin.Y + delta.Y); }
        }
        void OnUp(object sender, MouseButtonEventArgs e) {
            if (!pressed) return;
            bool click = !dragging && clock.Elapsed.TotalSeconds - pressedAt <= 0.7;
            FinishDrag();
            if (click && !falling) StartTickle(); else recentClicks.Clear();
            e.Handled = true;
        }
        void FinishDrag() {
            pressed = false; dragging = false; ReleaseMouseCapture();
            Rect area = WorkArea(); Left = Clamp(Left, area.Left, area.Right - Width);
            if (Top + Height < area.Bottom - 12) { StartFall(); return; }
            Top = area.Bottom - Height;
            if (reactionKind == 0) { state = 1; stateEnd = clock.Elapsed.TotalSeconds + 1; SetFrame(4); }
        }
        MenuItem Item(string label, RoutedEventHandler action) {
            MenuItem item = new MenuItem(); item.Header = label; item.Click += action; return item;
        }
        ContextMenu BuildMenu() {
            ContextMenu menu = new ContextMenu();
            menu.Items.Add(Item("포스트잇 · 할 일 등록/관리", delegate { TodoService.Current.OpenSettings(this); }));
            if (IsTodoCompanion) return menu;
            MenuItem pause = Item("잠깐 쉬기", delegate(object s, RoutedEventArgs e) {
                paused = !paused; ((MenuItem)s).Header = paused ? "다시 걷기" : "잠깐 쉬기"; if (paused) SetFrame(6);
            }); menu.Items.Add(pause);
            menu.Opened += delegate { pause.Header = paused ? "다시 걷기" : "잠깐 쉬기"; };
            MenuItem sizes = new MenuItem(); sizes.Header = "크기";
            sizes.Items.Add(Item("작게 (50)", delegate { ResizePet(50); }));
            sizes.Items.Add(Item("보통 (80)", delegate { ResizePet(80); }));
            sizes.Items.Add(Item("크게 (120)", delegate { ResizePet(120); })); sizes.Items.Add(Item("더 크게 (180)", delegate { ResizePet(180); }));
            sizes.Items.Add(Item("엄청 크게 (270)", delegate { ResizePet(270); }));
            sizes.Items.Add(Item("진격의 춘식이 (500)", delegate { ResizePet(500); })); menu.Items.Add(sizes);
            MenuItem speeds = new MenuItem(); speeds.Header = "걷는 속도";
            speeds.Items.Add(Item("느리게", delegate { speed = 22; }));
            speeds.Items.Add(Item("보통", delegate { speed = 44; }));
            speeds.Items.Add(Item("빠르게", delegate { speed = 80; })); menu.Items.Add(speeds);
            menu.Items.Add(Item("캐릭터 설정 (투명도 · 마우스 회피 · 기타)", delegate { PetPreferences.Current.OpenSettings(); }));
            menu.Items.Add(Item("춘식이 한 마리 추가", delegate { PetPreferences.Current.ChangeCount(Pets.Count + 1); }));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("알림 설정 (점심·퇴근·사용자 지정)", delegate { ReminderService.Current.OpenSettings(); }));
            menu.Items.Add(Item("테스트 알림", delegate { ReminderService.Current.Test(); }));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("이 춘식이 종료", delegate { PetPreferences.Current.ChangeCount(Pets.Count - 1, this); }));
            menu.Items.Add(Item("모두 종료", delegate { Quit(); }));
            return menu;
        }
        public static List<PetWindow> SpeechPets() {
            List<PetWindow> result = new List<PetWindow>();
            foreach (PetWindow pet in Pets) if (!pet.closing && pet.IsLoaded) result.Add(pet);
            result.Sort(delegate(PetWindow a, PetWindow b) { return ScreenLayout.BoundsPixels(a).Left.CompareTo(ScreenLayout.BoundsPixels(b).Left); });
            return result;
        }
        public bool TrySpeechAnchor(out Rect area, out Point head) {
            area = WorkArea();
            head = new Point(Left + Width / 2, Top + Height - picture.Height);
            return !closing && IsLoaded;
        }
        public bool TrySpeechAnchorPixels(out Rect area, out Point head) {
            area = Rect.Empty; head = new Point();
            if (closing || !IsLoaded || !IsVisible || PresentationSource.FromVisual(picture) == null) return false;
            area = ScreenLayout.AreaPixels(this);
            head = picture.PointToScreen(new Point(picture.ActualWidth / 2, 0));
            return true;
        }
        public static void Celebrate() {
            if (PetPreferences.Current != null && PetPreferences.Current.IsIdle) return;
            foreach (PetWindow pet in Pets) {
                if (pet.dragging || pet.pressed || pet.reactionKind != 0) continue;
                pet.state = 3; pet.stateEnd = pet.clock.Elapsed.TotalSeconds + 3; pet.SetFrame(7);
            }
        }
        public static void Quit() {
            if (exiting) return; exiting = true; Application.Current.Shutdown();
        }
        public static void Run(string folder) {
            bool first;
            using (System.Threading.Mutex instance = new System.Threading.Mutex(true, @"Local\ChunsikDesktopPet.v2", out first)) {
                if (!first) { MessageBox.Show("춘식이가 이미 실행 중이에요. 춘식이를 우클릭해 알림을 설정해 주세요.", "춘식이"); return; }
                try { RunApp(folder); }
                finally { instance.ReleaseMutex(); }
            }
        }
        static void RunApp(string folder) {
            PresentationPolicy.Start();
            PaydayCalendar.Load(Path.Combine(folder, "holidays-2025-2040.csv"));
            LoadFrames(Path.Combine(folder, "chunsik-payday.png"));
            paydayFrames = frames; paydayW = maxW; paydayH = maxH; paydayBodyH = frames[0].PixelHeight;
            LoadFrames(Path.Combine(folder, "chunsik-stretch.png"));
            stretchFrames = frames; stretchW = maxW; stretchH = maxH; stretchBodyH = frames[0].PixelHeight;
            LoadFrames(Path.Combine(folder, "chunsik-water.png"));
            waterFrames = frames;
            LoadFrames(Path.Combine(folder, "chunsik-notices.png"));
            noticeFrames = frames;
            LoadFrames(Path.Combine(folder, "chunsik-tickle.png"));
            reactionFrames = frames;
            LoadFrames(Path.Combine(folder, "chunsik-sprites.png"));
            BuildSeatedScootFrames();
            LoadSleepingFrame(Path.Combine(folder, "chunsik-sleep.png"));
            TodoNote.LoadArt(folder);
            Application app = new Application(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            PetPreferences.Current = new PetPreferences();
            ReminderService.Current = new ReminderService();
            TodoService.Current = new TodoService();
            app.Exit += delegate { ScreenLayout.Stop(); if (trayController != null) trayController.Dispose(); TimeToolWindow.StopAll(); TodoService.Current.Stop(); PetPreferences.Current.Stop(); ReminderService.Current.Stop(); };
            app.Startup += delegate {
                ScreenLayout.Start(); SetCount(PetPreferences.Current.Options.PetCount); trayController = new TrayController();
                if (PetPreferences.Current.Options.PaydayEnabled && PaydayCalendar.IsPayday(DateTime.Today, PetPreferences.Current.Options.PaydayDay))
                    app.Dispatcher.BeginInvoke(new Action(PreviewPayday), DispatcherPriority.Loaded);
            };
            app.Run();
        }
    }
}
