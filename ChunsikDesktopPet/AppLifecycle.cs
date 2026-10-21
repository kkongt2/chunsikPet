using System;
using System.Threading;
using System.Windows;
namespace ChunsikDesktop {
    public static class AppLifecycle {
        static readonly EventWaitHandle openSettings = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\ChunsikDesktopPet.OpenSettings.v2");
        static RegisteredWaitHandle listener;
        public static void Prepare() { GC.KeepAlive(openSettings); }
        public static void SignalSettings() { openSettings.Set(); }
        public static void Listen(Application app) {
            listener = ThreadPool.RegisterWaitForSingleObject(openSettings, delegate {
                if (!app.Dispatcher.HasShutdownStarted) app.Dispatcher.BeginInvoke(new Action(delegate { SettingsHub.Open("캐릭터"); }));
            }, null, Timeout.Infinite, false);
            app.Exit += delegate { if (listener != null) listener.Unregister(null); };
        }
    }
}
