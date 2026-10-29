using System;
using System.IO;
using System.Reflection;
using ChunsikDesktop;
internal static class DiagnosticsTests {
    static void Check(bool ok, string message) { if (!ok) throw new Exception("FAIL diagnostics: " + message); }
    public static void Run() {
        string expected = File.ReadAllText("VERSION").Trim();
        Check(Diagnostics.Version == expected, "display version comes from generated assembly");
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Check(!Diagnostics.Sanitize(profile + "\\private.txt").Contains(profile), "profile path redacted");
        Check(Diagnostics.Sanitize(new string('x', 20000)).Length < 16100, "error length bounded");
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChunsikDesktopPet", "last-error.txt");
        byte[] prior = File.Exists(path) ? File.ReadAllBytes(path) : null;
        try {
            Diagnostics.Record("test save", new InvalidOperationException("diagnostic-test-marker"));
            typeof(Diagnostics).GetField("lastError", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
            string report = Diagnostics.BuildReport();
            Check(report.Contains("v" + expected) && report.Contains("CLR:") && report.Contains("DPI="), "environment report contains version runtime and monitor DPI");
            Check(report.Contains("test save") && report.Contains("diagnostic-test-marker"), "latest error loads from disk after memory reset");
            Diagnostics.Record("second", new Exception("second-marker"));
            report = Diagnostics.BuildReport();
            Check(report.Contains("second-marker") && !report.Contains("diagnostic-test-marker"), "only most recent error retained");
        } finally {
            if (prior == null) File.Delete(path); else File.WriteAllBytes(path, prior);
            typeof(Diagnostics).GetField("lastError", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
        }
        Console.WriteLine("PASS: diagnostics environment, version, redaction, bounded persistent error");
    }
}
