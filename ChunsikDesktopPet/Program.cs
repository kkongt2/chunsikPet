using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
[assembly: AssemblyTitle("Chunsik Desktop Pet")]
[assembly: AssemblyDescription("Desktop companion, reminders and sticky notes")]
[assembly: AssemblyCompany("kkongt2.kang")]
[assembly: AssemblyProduct("Chunsik Desktop Pet")]
namespace ChunsikDesktop {
    internal static class Program {
        [STAThread]
        private static void Main() {
            Diagnostics.Install();
            try {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChunsikDesktopPet", "assets-v32");
                Directory.CreateDirectory(folder);
                Assembly assembly = Assembly.GetExecutingAssembly();
                foreach (string resource in assembly.GetManifestResourceNames()) {
                    if (!resource.StartsWith("Assets.", StringComparison.Ordinal)) continue;
                    string name = resource.Substring(7);
                    if (Path.GetFileName(name) != name || !(name.EndsWith(".png") || name.EndsWith(".csv"))) throw new InvalidDataException("Unexpected asset");
                    string target = Path.Combine(folder, name);
                    byte[] bytes;
                    using (Stream input = assembly.GetManifestResourceStream(resource))
                    using (MemoryStream output = new MemoryStream()) { input.CopyTo(output); bytes = output.ToArray(); }
                    bool matches = false;
                    if (File.Exists(target)) {
                        using (SHA256 hash = SHA256.Create())
                        using (FileStream existing = File.OpenRead(target))
                            matches = Convert.ToBase64String(hash.ComputeHash(existing)) == Convert.ToBase64String(hash.ComputeHash(bytes));
                    }
                    if (!matches) File.WriteAllBytes(target, bytes);
                }
                PetWindow.Run(folder);
            } catch (Exception error) {
                Diagnostics.ShowError("춘식이 시작·실행 오류", error);
            }
        }
    }
}

