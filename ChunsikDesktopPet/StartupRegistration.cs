using System;
using System.Reflection;
using Microsoft.Win32;
namespace ChunsikDesktop {
    public static class StartupRegistration {
        const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string Name = "ChunsikDesktopPet";
        public static bool Enabled {
            get { try { using (RegistryKey key = Registry.CurrentUser.OpenSubKey(Key)) return key != null && String.Equals(key.GetValue(Name) as string, Command, StringComparison.OrdinalIgnoreCase); } catch { return false; } }
        }
        static string Command { get { return "\"" + Assembly.GetExecutingAssembly().Location + "\""; } }
        public static void Set(bool enabled) {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(Key)) {
                if (enabled) key.SetValue(Name, Command, RegistryValueKind.String); else key.DeleteValue(Name, false);
            }
        }
    }
}
