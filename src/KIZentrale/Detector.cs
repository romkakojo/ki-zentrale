using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace KIZentrale;

/// <summary>
/// Erkennt, welche KI-Tools auf diesem PC installiert sind, wo sie liegen und wie sie angebunden sind
/// (Ports, Starter, Logs). Ergebnis: %LOCALAPPDATA%\KI-Zentrale\config.json. Keine festen Pfade.
/// </summary>
public static class Detector
{
    static readonly string Local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    static readonly string Roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public static readonly string DataDir = Path.Combine(Local, "KI-Zentrale");
    public static readonly string ConfigFile = Path.Combine(DataDir, "config.json");

    static readonly string[] SkipDirs = ["Windows", "$Recycle.Bin", "System Volume Information", "ProgramData",
        "Recovery", "PerfLogs", "SteamLibrary", "WindowsApps", "WpSystem", "node_modules", ".git", "Config.Msi"];

    public static JsonObject? Load()
    {
        try { return JsonNode.Parse(File.ReadAllText(ConfigFile)) as JsonObject; } catch { return null; }
    }

    public static void Save(JsonObject cfg)
    {
        Directory.CreateDirectory(DataDir);
        File.WriteAllText(ConfigFile, cfg.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Vollstaendige Erkennung. Muss auf einem STA-Thread laufen (Startmenue-Abfrage per COM).</summary>
    public static JsonObject Detect()
    {
        var running = RunningProcesses();
        var startApps = StartMenuApps();
        var cfg = new JsonObject
        {
            ["detected_at"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ["computer"] = Environment.MachineName,
        };

        // ------------------------------------------------ NInfer
        var ninfer = new JsonObject { ["installed"] = false };
        string? nExe = running.FirstOrDefault(p => p.name == "ninfer-serve").path ?? FindFile("ninfer-serve.exe", 3);
        if (nExe != null)
        {
            ninfer["installed"] = true;
            ninfer["exe"] = nExe;
            var dir = Path.GetDirectoryName(nExe)!;
            var parent = Path.GetDirectoryName(dir) ?? dir;
            string? launcher = new[] { dir, parent }.SelectMany(d => SafeFiles(d, "start-ninfer.ps1")).FirstOrDefault()
                ?? new[] { parent, dir }.SelectMany(d => SafeFiles(d, "*.ps1").Concat(SafeFiles(d, "*.bat")))
                    .FirstOrDefault(f => ReadText(f).Contains("ninfer-serve", StringComparison.OrdinalIgnoreCase));
            if (launcher != null) ninfer["launcher"] = launcher;
            var cmdline = running.FirstOrDefault(p => p.name == "ninfer-serve").cmd ?? "";
            var portSrc = cmdline + "\n" + (launcher != null ? ReadText(launcher) : "");
            var pm = Regex.Match(portSrc, @"--port[ =""']+(\d{2,5})");
            ninfer["port"] = pm.Success ? int.Parse(pm.Groups[1].Value) : 8080;
            var mm = Regex.Match(portSrc, @"[A-Za-z]:\\[^""'\r\n]*?\.ninfer");
            string? model = mm.Success && File.Exists(mm.Value) ? mm.Value : FindFileIn(new[] { Path.Combine(parent, "LLM"), Path.Combine(dir, "models") }.Concat(DriveDirs("LLM")), "*.ninfer");
            if (model != null) ninfer["model"] = model;
            foreach (var d in new[] { parent, dir })
            {
                var log = Path.Combine(d, "server.log.err");
                if (File.Exists(log)) { ninfer["log"] = log; break; }
            }
            var ctx = Path.Combine(parent, "current-context.txt");
            if (File.Exists(ctx)) ninfer["ctxfile"] = ctx;
        }
        cfg["ninfer"] = ninfer;

        // ------------------------------------------------ llama.cpp
        string? lExe = running.FirstOrDefault(p => p.name == "llama-server").path ?? FindFile("llama-server.exe", 2);
        cfg["llama"] = new JsonObject
        {
            ["installed"] = lExe != null, ["exe"] = lExe,
            ["retired"] = nExe != null,   // NInfer vorhanden -> llama.cpp gilt als ausgemustert
        };

        // ------------------------------------------------ Hermes Agent
        var hermesHome = Environment.GetEnvironmentVariable("HERMES_HOME") ?? Path.Combine(Local, "hermes");
        var hExe = Path.Combine(hermesHome, "bin", "hermes.exe");
        var hermes = new JsonObject { ["installed"] = File.Exists(hExe) };
        if (File.Exists(hExe))
        {
            hermes["exe"] = hExe;
            hermes["home"] = hermesHome;
            var env = ReadText(Path.Combine(hermesHome, ".env"));
            string EnvVal(string k) { var m = Regex.Match(env, $@"(?m)^\s*{k}\s*=\s*(.*)$"); return m.Success ? m.Groups[1].Value.Trim() : ""; }
            bool apiOn = EnvVal("API_SERVER_ENABLED").Equals("true", StringComparison.OrdinalIgnoreCase);
            hermes["api_port"] = apiOn ? (int.TryParse(EnvVal("API_SERVER_PORT"), out var ap) ? ap : 8642) : null;
            hermes["api_model"] = apiOn ? (EnvVal("API_SERVER_MODEL_NAME") is { Length: > 0 } mn ? mn : "Hermes") : null;
            hermes["telegram"] = EnvVal("TELEGRAM_BOT_TOKEN").Length > 10;
            hermes["log"] = Path.Combine(hermesHome, "logs", "gateway.log");
        }
        cfg["hermes"] = hermes;

        // ------------------------------------------------ Open WebUI (Desktop-App)
        var owui = new JsonObject { ["installed"] = false };
        string? oExe = UninstallIcon("Open WebUI") ?? FirstExisting(Path.Combine(Local, @"Programs\open-webui\open-webui.exe"));
        if (oExe != null)
        {
            owui["installed"] = true;
            owui["exe"] = oExe;
            int port = 8080;
            try
            {
                if (JsonNode.Parse(ReadText(Path.Combine(Roaming, "open-webui", "config.json"))) is JsonObject oc)
                    port = oc["localServer"]?["port"]?.GetValue<int>() ?? port;
            }
            catch { }
            owui["port"] = port;
            owui["appid"] = startApps.FirstOrDefault(a => a.name.Equals("Open WebUI", StringComparison.OrdinalIgnoreCase)).id;
        }
        cfg["openwebui"] = owui;

        // ------------------------------------------------ OpenCode
        var ocPath = FindInPath("opencode.cmd", "opencode.ps1", "opencode.exe");
        cfg["opencode"] = new JsonObject
        {
            ["installed"] = ocPath != null, ["path"] = ocPath,
            ["config"] = FirstExisting(Path.Combine(Home, ".config", "opencode", "opencode.json")),
        };

        // ------------------------------------------------ Startmenue-Apps
        cfg["claude"] = AppEntry(startApps, ["Claude"], ["claude"]);
        cfg["comfyui"] = AppEntry(startApps, ["Comfy Desktop", "ComfyUI"], ["comfy desktop", "comfyui"]);
        cfg["pinokio"] = AppEntry(startApps, ["Pinokio"], ["pinokio"]);
        cfg["luminar"] = AppEntry(startApps, ["Luminar AI", "Luminar Neo", "Luminar"], ["luminar ai", "luminarai", "luminar neo"]);

        // ------------------------------------------------ Sonstiges
        cfg["terminal"] = FindInPath("wt.exe") != null ? "wt" : "cmd";
        cfg["projects"] = DriveDirs("Projekte").FirstOrDefault() ?? Path.Combine(Home, "Documents");
        return cfg;
    }

    /// <summary>Kurze Zusammenfassung fuer den Nutzer.</summary>
    public static string Summary(JsonObject c)
    {
        var sb = new StringBuilder();
        void Line(string name, JsonNode? n, string detail)
        {
            bool ok = n?["installed"]?.GetValue<bool>() == true;
            sb.AppendLine($"{(ok ? "✔" : "✖")}  {name}{(ok && detail.Length > 0 ? "  –  " + detail : ok ? "" : "  –  nicht gefunden")}");
        }
        Line("NInfer (Modellserver)", c["ninfer"], c["ninfer"]?["installed"]?.GetValue<bool>() == true
            ? $"Port {c["ninfer"]?["port"]}{(c["ninfer"]?["launcher"] != null ? ", Starter gefunden" : ", kein Starter")}" : "");
        Line("Hermes Agent", c["hermes"], c["hermes"]?["api_port"] is JsonNode p ? $"API Port {p}{(c["hermes"]?["telegram"]?.GetValue<bool>() == true ? ", Telegram" : "")}" : "API-Server aus");
        Line("Open WebUI", c["openwebui"], $"Port {c["openwebui"]?["port"]}");
        Line("OpenCode", c["opencode"], "");
        Line("Claude", c["claude"], "");
        Line("ComfyUI", c["comfyui"], "");
        Line("Pinokio", c["pinokio"], "");
        Line("Luminar AI", c["luminar"], "");
        Line("llama.cpp", c["llama"], c["llama"]?["retired"]?.GetValue<bool>() == true ? "ausgemustert (NInfer vorhanden)" : "");
        sb.AppendLine();
        sb.AppendLine($"Projektordner: {c["projects"]}");
        return sb.ToString();
    }

    // ================================================================= Helfer
    static JsonObject AppEntry(List<(string name, string id)> apps, string[] names, string[] procs)
    {
        // Reihenfolge der Namen = Vorrang (z. B. „Comfy Desktop“ vor der ComfyUI-Browser-App)
        (string name, string id) hit = default;
        foreach (var n in names)
        {
            hit = apps.FirstOrDefault(a => a.name.Equals(n, StringComparison.OrdinalIgnoreCase));
            if (hit.id != null) break;
        }
        if (hit.id == null)
            foreach (var n in names)
            {
                hit = apps.FirstOrDefault(a => a.name.StartsWith(n, StringComparison.OrdinalIgnoreCase));
                if (hit.id != null) break;
            }
        return new JsonObject
        {
            ["installed"] = hit.id != null, ["appid"] = hit.id, ["name"] = hit.name,
            ["proc"] = new JsonArray(procs.Select(p => (JsonNode)p).ToArray()),
        };
    }

    /// <summary>Alle Startmenue-Apps (Name + AppUserModelID) ueber shell:AppsFolder.</summary>
    static List<(string name, string id)> StartMenuApps()
    {
        var list = new List<(string, string)>();
        try
        {
            var t = Type.GetTypeFromProgID("Shell.Application");
            if (t == null) return list;
            dynamic shell = Activator.CreateInstance(t)!;
            dynamic folder = shell.NameSpace("shell:AppsFolder");
            foreach (dynamic item in folder.Items())
            {
                try { list.Add(((string)item.Name, (string)item.Path)); } catch { }
            }
        }
        catch { }
        return list;
    }

    /// <summary>Laufende Prozesse mit Pfad und Befehlszeile (fuer Port/Modell der Server).</summary>
    static List<(string name, string? path, string? cmd)> RunningProcesses()
    {
        var res = new List<(string, string?, string?)>();
        var ps = "Get-CimInstance Win32_Process -Filter \\\"Name='ninfer-serve.exe' or Name='llama-server.exe'\\\" | ForEach-Object { $_.Name + '|' + $_.ExecutablePath + '|' + $_.CommandLine }";
        foreach (var line in Run("powershell.exe", $"-NoProfile -Command \"{ps}\"").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = line.Trim().Split('|', 3);
            if (p.Length == 3) res.Add((Path.GetFileNameWithoutExtension(p[0]).ToLowerInvariant(), NullIfEmpty(p[1]), p[2]));
        }
        return res;
    }

    static string? FindFile(string name, int depth)
    {
        foreach (var root in FixedDrives())
        {
            var hit = Bfs(root, name, depth);
            if (hit != null) return hit;
        }
        return null;
    }

    static string? Bfs(string root, string name, int maxDepth)
    {
        var q = new Queue<(string dir, int depth)>();
        q.Enqueue((root, 0));
        var opts = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = false, AttributesToSkip = FileAttributes.System | FileAttributes.ReparsePoint };
        while (q.Count > 0)
        {
            var (dir, d) = q.Dequeue();
            try
            {
                var f = Directory.EnumerateFiles(dir, name, opts).FirstOrDefault();
                if (f != null) return f;
                if (d >= maxDepth) continue;
                foreach (var sub in Directory.EnumerateDirectories(dir, "*", opts))
                    if (!SkipDirs.Contains(Path.GetFileName(sub), StringComparer.OrdinalIgnoreCase)) q.Enqueue((sub, d + 1));
            }
            catch { }
        }
        return null;
    }

    static string? FindFileIn(IEnumerable<string> dirs, string pattern) =>
        dirs.Where(Directory.Exists).SelectMany(d => SafeFiles(d, pattern)).FirstOrDefault();

    static IEnumerable<string> SafeFiles(string dir, string pattern)
    {
        try { return Directory.Exists(dir) ? Directory.GetFiles(dir, pattern) : []; } catch { return []; }
    }

    static IEnumerable<string> FixedDrives() =>
        DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady).Select(d => d.RootDirectory.FullName);

    static IEnumerable<string> DriveDirs(string name) =>
        FixedDrives().Select(r => Path.Combine(r, name)).Where(Directory.Exists);

    static string? FindInPath(params string[] names)
    {
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Append(Path.Combine(Roaming, "npm"));
        foreach (var p in paths)
            foreach (var n in names)
            {
                try { var f = Path.Combine(p.Trim(), n); if (File.Exists(f)) return f; } catch { }
            }
        return null;
    }

    static string? UninstallIcon(string displayName)
    {
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            foreach (var path in new[] { @"Software\Microsoft\Windows\CurrentVersion\Uninstall", @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" })
            {
                try
                {
                    using var k = hive.OpenSubKey(path);
                    if (k == null) continue;
                    foreach (var sub in k.GetSubKeyNames())
                    {
                        using var s = k.OpenSubKey(sub);
                        if (s != null && (s.GetValue("DisplayName") as string)?.StartsWith(displayName, StringComparison.OrdinalIgnoreCase) == true
                            && s.GetValue("DisplayIcon") is string icon)
                        {
                            var exe = icon.Split(',')[0].Trim('"');
                            if (File.Exists(exe)) return exe;
                        }
                    }
                }
                catch { }
            }
        return null;
    }

    static string? FirstExisting(params string[] files) => files.FirstOrDefault(File.Exists);
    static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    static string ReadText(string path)
    {
        try { return File.Exists(path) ? File.ReadAllText(path) : ""; } catch { return ""; }
    }

    static string Run(string file, string args)
    {
        try
        {
            var psi = new ProcessStartInfo(file, args) { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8 };
            using var p = Process.Start(psi)!;
            var o = p.StandardOutput.ReadToEndAsync();
            p.WaitForExit(20000);
            return o.Wait(1000) ? o.Result : "";
        }
        catch { return ""; }
    }
}
