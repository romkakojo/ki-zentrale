using System.Diagnostics;
using Microsoft.Win32;

namespace KIZentrale;

/// <summary>Deinstallation (Aufruf: KI-Zentrale.exe --uninstall, eingetragen vom Setup unter „Installierte Apps“).</summary>
static class Uninstaller
{
    const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\KI-Zentrale";
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static void Run(bool quiet)
    {
        if (!quiet && MessageBox.Show("KI-Zentrale wirklich deinstallieren?\n\nDeine KI-Tools (NInfer, Hermes, Open WebUI, …) bleiben unverändert.",
                "KI-Zentrale deinstallieren", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        // Laufende Instanz beenden
        foreach (var p in Process.GetProcessesByName("KI-Zentrale"))
        {
            if (p.Id == Environment.ProcessId) continue;
            try { p.Kill(true); p.WaitForExit(3000); } catch { }
        }

        string? installDir = null;
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(UninstallKey);
            installDir = k?.GetValue("InstallLocation") as string;
        }
        catch { }

        // Verknuepfungen (Desktop + Startmenue)
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        foreach (var lnk in new[] { "KI-Zentrale.lnk", "Systemmonitor.lnk" })
            TryDelete(Path.Combine(desktop, lnk));
        var startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "KI-Zentrale");
        try { if (Directory.Exists(startMenu)) Directory.Delete(startMenu, true); } catch { }

        // Autostart und Eintrag in „Installierte Apps“
        try { using var r = Registry.CurrentUser.OpenSubKey(RunKey, true); r?.DeleteValue("KI-Zentrale", false); } catch { }
        try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); } catch { }

        // Einstellungen (Layout, erkannte Tools, Fensterpositionen) optional entfernen
        if (!quiet && MessageBox.Show("Auch die Einstellungen löschen (Kachel-Anordnung, erkannte Tools, Fensterpositionen)?",
                "KI-Zentrale deinstallieren", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            try { Directory.Delete(Detector.DataDir, true); } catch { }
        }

        // Programmordner nach dem Beenden loeschen – nur den vom Setup eingetragenen Installationsordner
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath)!;
        if (installDir != null && string.Equals(Path.GetFullPath(installDir).TrimEnd('\\'), exeDir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)
            && Path.GetFileName(exeDir).Equals("KI-Zentrale", StringComparison.OrdinalIgnoreCase))
        {
            Process.Start(new ProcessStartInfo("cmd.exe", $"/c ping 127.0.0.1 -n 3 >nul & rmdir /s /q \"{exeDir}\"")
            { CreateNoWindow = true, UseShellExecute = false });
        }

        if (!quiet) MessageBox.Show("KI-Zentrale wurde deinstalliert.", "KI-Zentrale", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    static void TryDelete(string f) { try { if (File.Exists(f)) File.Delete(f); } catch { } }
}
