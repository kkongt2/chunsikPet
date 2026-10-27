using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
[assembly: AssemblyTitle("Chunsik Desktop Pet")]
[assembly: AssemblyDescription("Desktop companion, reminders and sticky notes")]
[assembly: AssemblyCompany("kkongt2.kang")]
[assembly: AssemblyProduct("Chunsik Desktop Pet")]
[assembly: AssemblyVersion("1.50.0.0")]
[assembly: AssemblyFileVersion("1.50.0.0")]
namespace ChunsikDesktop {
    internal static class Program {
        [STAThread]
        private static void Main() {
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
                MessageBox.Show("춘식이를 시작하지 못했어요.\n\n" + error.ToString(), "춘식이 실행 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
