using System;
using System.Windows;

namespace ChunsikDesktop {
    public partial class PetWindow {
        // Short cues use the existing pet timer; higher-priority activities interrupt them.
        int cueKind;
        double cueStarted, nextLookBack;
        bool wasEvading;
        void StartCue(int kind) { CancelCue(); cueKind = kind; cueStarted = clock.Elapsed.TotalSeconds; }
        void CancelCue() {
            if (cueKind == 0) return;
            cueKind = 0;
            flip.ScaleX = flip.ScaleY = 1; squirm.Angle = 0; hop.X = hop.Y = 0;
            SetFrame(paused ? 6 : 4);
        }
        void SetRestMode(bool value) {
            if (paused == value) return;
            Wake(); EndReaction(); paused = value; wasEvading = false;
            StartCue(value ? 2 : 3);
            SetFrame(value ? 7 : 4);
        }
        bool AnimateCue(double now) {
            if (cueKind == 0) return false;
            double t = Math.Max(0, now - cueStarted);
            double duration = cueKind == 1 ? 0.65 : cueKind == 2 ? 0.8 : 1.1;
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
            }
            return true;
        }
    }
}
