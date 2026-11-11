using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace KIZentraleSetup
{
    /// <summary>Die eigentliche Installation (pro Benutzer, ohne Admin-Rechte).</summary>
    static class Installer
    {
        /// <summary>Version aus Directory.Build.props (z. B. 1.1.0).</summary>
        public static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
        const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\KI-Zentrale";
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string WebView2Guid = "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";

        public static string DefaultDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "KI-Zentrale");

        public static string ExistingInstallDir()
        {
            try { using (var k = Registry.CurrentUser.OpenSubKey(UninstallKey)) return k?.GetValue("InstallLocation") as string; }
            catch { return null; }
        }

        public static bool AutostartWasOn()
        {
            try { using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) return k?.GetValue("KI-Zentrale") != null; }
            catch { return false; }
        }

        /// <summary>WebView2-Laufzeit (zeichnet die Oberflaeche) vorhanden? Bei Windows 11 immer.</summary>
        public static string WebView2Version()
        {
            foreach (var (hive, path) in new[] {
                (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\" + WebView2Guid),
                (Registry.LocalMachine, @"SOFTWARE\Microsoft\EdgeUpdate\Clients\" + WebView2Guid),
                (Registry.CurrentUser, @"Software\Microsoft\EdgeUpdate\Clients\" + WebView2Guid) })
            {
                try
                {
                    using (var k = hive.OpenSubKey(path))
                        if (k?.GetValue("pv") is string v && v != "" && v != "0.0.0.0") return v;
                }
                catch { }
            }
            return null;
        }

        public static void Install(string dir, bool desktop, bool startMenu, bool autostart, bool launch, Action<int, string> progress)
        {
            void P(int pct, string msg) => progress?.Invoke(pct, msg);

            P(5, "Beende laufende KI-Zentrale …");
            foreach (var p in Process.GetProcessesByName("KI-Zentrale"))
            {
                try { p.Kill(); p.WaitForExit(5000); } catch { }
            }

            P(15, "Kopiere Programmdateien …");
            Directory.CreateDirectory(dir);
            string exe = Path.Combine(dir, "KI-Zentrale.exe");
            Extract("payload.KI-Zentrale.exe", exe);
            P(70, "Kopiere Symbole …");
            string monIco = Path.Combine(dir, "monitor.ico");
            Extract("payload.monitor.ico", monIco);

            P(78, "Lege Verknüpfungen an …");
            var desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (desktop)
            {
                Shortcut(Path.Combine(desk, "KI-Zentrale.lnk"), exe, "", exe + ",0", "KI-Zentrale: alle lokalen KI-Tools");
                Shortcut(Path.Combine(desk, "Systemmonitor.lnk"), exe, "--monitor", monIco + ",0", "Systemmonitor: CPU, RAM, GPU");
            }
            var sm = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "KI-Zentrale");
            if (startMenu)
            {
                Directory.CreateDirectory(sm);
                Shortcut(Path.Combine(sm, "KI-Zentrale.lnk"), exe, "", exe + ",0", "KI-Zentrale: alle lokalen KI-Tools");
                Shortcut(Path.Combine(sm, "Systemmonitor.lnk"), exe, "--monitor", monIco + ",0", "Systemmonitor: CPU, RAM, GPU");
                Shortcut(Path.Combine(sm, "KI-Zentrale deinstallieren.lnk"), exe, "--uninstall", exe + ",0", "KI-Zentrale entfernen");
            }

            P(88, "Registriere bei Windows …");
            using (var k = Registry.CurrentUser.CreateSubKey(UninstallKey))
            {
                k.SetValue("DisplayName", "KI-Zentrale");
                k.SetValue("DisplayVersion", Version);
                k.SetValue("Publisher", "KI-Zentrale");
                k.SetValue("DisplayIcon", exe + ",0");
                k.SetValue("InstallLocation", dir);
                k.SetValue("UninstallString", $"\"{exe}\" --uninstall");
                k.SetValue("QuietUninstallString", $"\"{exe}\" --uninstall --quiet");
                k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                k.SetValue("EstimatedSize", (int)(new FileInfo(exe).Length / 1024), RegistryValueKind.DWord);
            }
            using (var r = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (autostart) r.SetValue("KI-Zentrale", $"\"{exe}\"");
                else r.DeleteValue("KI-Zentrale", false);
            }

            if (launch)
            {
                P(96, "Starte KI-Zentrale …");
                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = dir });
            }
            P(100, "Fertig.");
        }

        static void Extract(string resource, string target)
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
            {
                if (s == null) throw new InvalidOperationException("Setup ist unvollständig: " + resource + " fehlt.");
                var tmp = target + ".new";
                using (var f = File.Create(tmp)) s.CopyTo(f);
                if (File.Exists(target)) File.Delete(target);
                File.Move(tmp, target);
            }
        }

        static void Shortcut(string lnk, string target, string args, string icon, string description)
        {
            var t = Type.GetTypeFromProgID("WScript.Shell");
            dynamic shell = Activator.CreateInstance(t);
            dynamic sc = shell.CreateShortcut(lnk);
            sc.TargetPath = target;
            sc.Arguments = args;
            sc.WorkingDirectory = Path.GetDirectoryName(target);
            sc.IconLocation = icon;
            sc.Description = description;
            sc.Save();
        }
    }
}
