using System;
using System.Threading;
using System.Windows;
namespace ChunsikDesktop {
    public static class AppLifecycle {
        static readonly EventWaitHandle openSettings = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\ChunsikDesktopPet.OpenSettings.v2");
        static RegisteredWaitHandle listener;
        public static void Prepare() { GC.KeepAlive(openSettings); }
        public static void SignalSettings() { openSettings.Set(); }
        public static bool WaitForPrior(string[] args) {
            if (args.Length != 2 || args[0] != "--wait-for-exit") return true;
            int pid; if (!Int32.TryParse(args[1], out pid) || pid == System.Diagnostics.Process.GetCurrentProcess().Id) return false;
            try { using (System.Diagnostics.Process prior = System.Diagnostics.Process.GetProcessById(pid)) return prior.WaitForExit(30000); }
            catch (ArgumentException) { return true; }
        }
        public static void Restart() {
            string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                FileName = exe, Arguments = "--wait-for-exit " + System.Diagnostics.Process.GetCurrentProcess().Id,
                WorkingDirectory = System.IO.Path.GetDirectoryName(exe), UseShellExecute = false
            });
            PetWindow.Quit();
        }
        public static void Listen(Application app) {
            listener = ThreadPool.RegisterWaitForSingleObject(openSettings, delegate {
                if (!app.Dispatcher.HasShutdownStarted) app.Dispatcher.BeginInvoke(new Action(delegate { SettingsHub.Open("캐릭터"); }));
            }, null, Timeout.Infinite, false);
            app.Exit += delegate { if (listener != null) listener.Unregister(null); };
        }
    }
}
