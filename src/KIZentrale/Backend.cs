using System.Diagnostics;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace KIZentrale;

/// <summary>Status aller KI-Tools und die Aktionen der Kacheln. Pfade/Ports kommen aus der Erkennung (config.json).</summary>
public sealed class Backend
{
    static readonly string LayoutFile = Path.Combine(Detector.DataDir, "layout.json");
    static readonly string[] TileIds = ["hermes", "openwebui", "opencode", "claude", "comfyui", "pinokio", "luminar", "llama"];
    static readonly string[] DefAgents = ["hermes", "openwebui", "opencode", "claude"];
    static readonly string[] DefCreative = ["comfyui", "pinokio", "luminar", "llama"];
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(1.5) };

    readonly MainForm form;
    JsonObject cfg;
    int? opencodePort;
    DateTime opencodeChecked = DateTime.MinValue;

    public Backend(MainForm form, JsonObject cfg) { this.form = form; this.cfg = cfg; }
    public void SetConfig(JsonObject c) { cfg = c; opencodePort = null; }

    // ------------------------------------------------------------ Konfiguration lesen
    bool Has(string tool) => cfg[tool]?["installed"]?.GetValue<bool>() == true;
    string? S(string tool, string key) => cfg[tool]?[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
    int? I(string tool, string key) => cfg[tool]?[key] is JsonValue v && v.TryGetValue<int>(out var i) ? i : null;
    int NinferPort => I("ninfer", "port") ?? 8080;
    string Projects => (cfg["projects"] as JsonValue)?.GetValue<string>() ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    // ------------------------------------------------------------ Routing
    public (int code, string json) Handle(string method, string path, string body)
    {
        JsonObject req;
        try { req = string.IsNullOrWhiteSpace(body) ? new JsonObject() : (JsonNode.Parse(body) as JsonObject ?? new JsonObject()); }
        catch { req = new JsonObject(); }

        switch (path)
        {
            case "/api/status": return (200, Status().ToJsonString());
            case "/api/config": return (200, PublicConfig().ToJsonString());
            case "/api/ping": return (200, "{\"ok\":true}");
            case "/api/layout" when method == "GET": return (200, LoadLayout().ToJsonString());
            case "/api/layout":
                SaveLayout(req);
                return (200, "{\"ok\":true}");
            case "/api/redetect":
                form.BeginInvoke(form.Redetect);
                return (200, "{\"ok\":true}");
            case "/api/window":
                var cmd = req["cmd"]?.GetValue<string>() ?? "toggle";
                form.BeginInvoke(() =>
                {
                    if (cmd == "hide") form.HideWidget();
                    else if (cmd == "show") form.ShowWidget();
                    else form.ToggleWidget();
                });
                return (200, "{\"ok\":true}");
            case "/api/quit":
                form.BeginInvoke(form.Quit);
                return (200, "{\"ok\":true}");
            case "/api/action":
                string tool = req["tool"]?.GetValue<string>() ?? "", act = req["action"]?.GetValue<string>() ?? "";
                var msg = Action(tool, act);
                return msg == null
                    ? (400, "{\"error\":\"unbekannte Aktion\"}")
                    : (200, new JsonObject { ["ok"] = true, ["message"] = msg }.ToJsonString());
        }
        return (404, "{\"error\":\"unbekannt\"}");
    }

    /// <summary>Fuer die Oberflaeche: welche Tools vorhanden sind (ohne Pfade).</summary>
    JsonObject PublicConfig()
    {
        var o = new JsonObject();
        foreach (var t in TileIds.Append("ninfer")) o[t] = Has(t);
        o["llama_retired"] = cfg["llama"]?["retired"]?.GetValue<bool>() == true;
        o["ninfer_port"] = NinferPort;
        o["openwebui_port"] = I("openwebui", "port");
        o["hermes_api"] = I("hermes", "api_port");
        o["projects"] = Projects;
        o["detected_at"] = (cfg["detected_at"] as JsonValue)?.GetValue<string>();
        return o;
    }

    // ------------------------------------------------------------ Helfer
    static string Run(string file, string args, int timeoutMs = 10000)
    {
        try
        {
            var psi = new ProcessStartInfo(file, args)
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
                CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8,
            };
            using var p = Process.Start(psi)!;
            var outTask = p.StandardOutput.ReadToEndAsync();
            if (!p.WaitForExit(timeoutMs)) { try { p.Kill(true); } catch { } }
            return outTask.Wait(1000) ? outTask.Result.Trim() : "";
        }
        catch { return ""; }
    }

    static void Spawn(string file, string args, string? cwd = null)
    {
        try { Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = cwd ?? "" }); }
        catch { }
    }

    static void SpawnVisible(string file, string args, string? cwd = null)
    {
        try { Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = true, WorkingDirectory = cwd ?? "" }); }
        catch { }
    }

    public static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }

    static void OpenApp(string id) => SpawnVisible("explorer.exe", $"shell:AppsFolder\\{id}");

    static bool PortOpen(int? port)
    {
        if (port is not int p) return false;
        try
        {
            using var c = new TcpClient();
            return c.ConnectAsync("127.0.0.1", p).Wait(300) && c.Connected;
        }
        catch { return false; }
    }

    static JsonNode? GetJson(string url)
    {
        try { return JsonNode.Parse(Http.GetStringAsync(url).GetAwaiter().GetResult()); }
        catch { return null; }
    }

    static HashSet<string> ProcessNames()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in Process.GetProcesses()) { try { set.Add(p.ProcessName); } catch { } finally { p.Dispose(); } }
        return set;
    }

    static string[] Tail(string? path, int lines)
    {
        if (path == null) return [];
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            fs.Seek(Math.Max(0, fs.Length - 200_000), SeekOrigin.Begin);
            using var sr = new StreamReader(fs, Encoding.UTF8);
            var all = sr.ReadToEnd().Split('\n');
            return all.Skip(Math.Max(0, all.Length - lines)).ToArray();
        }
        catch { return []; }
    }

    // ------------------------------------------------------------ Status
    public (bool ready, bool running, int ctx) NinferQuick()
    {
        if (!Has("ninfer")) return (false, false, 0);
        bool running = Process.GetProcessesByName("ninfer-serve").Length > 0;
        var m = running ? GetJson($"http://127.0.0.1:{NinferPort}/v1/models") : null;
        bool ready = m?["data"] is JsonArray a && a.Count > 0;
        int ctx = ready ? (m!["data"]![0]!["context_window"]?.GetValue<int>() ?? ReadCtx()) : ReadCtx();
        return (ready, running, ctx);
    }

    int ReadCtx()
    {
        try { return int.Parse(File.ReadAllText(S("ninfer", "ctxfile")!).Trim()); } catch { return 0; }
    }

    JsonObject NinferStatus(HashSet<string> procs)
    {
        var o = new JsonObject { ["installed"] = Has("ninfer"), ["port"] = NinferPort };
        bool running = procs.Contains("ninfer-serve");
        var models = running ? GetJson($"http://127.0.0.1:{NinferPort}/v1/models") : null;
        bool ready = models?["data"] is JsonArray arr && arr.Count > 0;
        int ctx = ready ? models!["data"]![0]!["context_window"]?.GetValue<int>() ?? 0 : 0;
        if (ctx == 0) ctx = ReadCtx();
        double? tps = null; int? busy = null; string? since = null;
        foreach (var line in Tail(S("ninfer", "log"), 300).Reverse())
        {
            if (tps == null && line.Contains(" done ") && line.Contains("decode "))
            {
                var mm = Regex.Match(line, @"decode ([\d.]+) tok/s");
                if (mm.Success) tps = double.Parse(mm.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            }
            if (busy == null && line.Contains("| running "))
            {
                var mm = Regex.Match(line, @"running (\d+)");
                if (mm.Success) busy = int.Parse(mm.Groups[1].Value);
            }
            if (since == null && line.Contains("starting engine") && line.Length > 16) since = line.Substring(11, 5);
            if (tps != null && busy != null && since != null) break;
        }
        double? used = null, total = null;
        var smi = Run("nvidia-smi", "--query-gpu=memory.used,memory.total --format=csv,noheader,nounits", 5000).Split(',');
        if (smi.Length >= 2 && double.TryParse(smi[0].Trim(), out var u) && double.TryParse(smi[1].Trim(), out var t))
        { used = Math.Round(u / 1024, 1); total = Math.Round(t / 1024, 1); }
        o["running"] = running; o["ready"] = ready; o["context"] = ctx == 0 ? null : ctx;
        o["tps"] = tps; o["busy"] = busy; o["since"] = since; o["vram_used"] = used; o["vram_total"] = total;
        o["model"] = models?["data"]?[0]?["id"]?.GetValue<string>();
        return o;
    }

    JsonObject HermesStatus()
    {
        var api = I("hermes", "api_port");
        bool apiUp = PortOpen(api);
        bool telegram = false;
        foreach (var line in Tail(S("hermes", "log"), 400).Reverse())
        {
            if (line.Contains("telegram connected")) { telegram = true; break; }
            if (line.Contains("Gateway stopped") || line.Contains("drained")) break;
        }
        return new JsonObject
        {
            ["installed"] = Has("hermes"), ["running"] = apiUp || telegram, ["api"] = apiUp, ["api_port"] = api,
            ["telegram"] = telegram, ["dashboard"] = PortOpen(9119),
        };
    }

    JsonObject OpencodeStatus()
    {
        if (Has("opencode") && ((DateTime.Now - opencodeChecked).TotalSeconds > 60 || opencodePort == null))
        {
            var o = Run("cmd.exe", "/c opencode service status", 15000);
            var m = Regex.Match(o, @"127\.0\.0\.1:(\d+)");
            opencodePort = m.Success ? int.Parse(m.Groups[1].Value) : null;
            opencodeChecked = DateTime.Now;
        }
        return new JsonObject { ["installed"] = Has("opencode"), ["running"] = PortOpen(opencodePort), ["port"] = opencodePort };
    }

    JsonObject Status()
    {
        var procs = ProcessNames();
        var st = new JsonObject
        {
            ["ninfer"] = NinferStatus(procs),
            ["hermes"] = HermesStatus(),
            ["openwebui"] = new JsonObject { ["installed"] = Has("openwebui"), ["running"] = PortOpen(I("openwebui", "port")), ["port"] = I("openwebui", "port") },
            ["opencode"] = OpencodeStatus(),
            ["llama"] = new JsonObject { ["installed"] = Has("llama"), ["running"] = procs.Contains("llama-server") },
            ["time"] = DateTime.Now.ToString("HH:mm:ss"),
        };
        foreach (var key in new[] { "claude", "comfyui", "pinokio", "luminar" })
        {
            var names = (cfg[key]?["proc"] as JsonArray)?.Select(n => n!.GetValue<string>()) ?? [];
            st[key] = new JsonObject { ["installed"] = Has(key), ["running"] = names.Any(procs.Contains) };
        }
        if (Has("comfyui") && PortOpen(8000)) st["comfyui"]!["running"] = true;
        return st;
    }

    // ------------------------------------------------------------ Aktionen
    static void StopNinfer()
    {
        Run("taskkill.exe", "/F /IM ninfer-serve.exe");
        Run("taskkill.exe", "/F /IM llama-server.exe");
    }

    void StartNinfer()
    {
        var launcher = S("ninfer", "launcher");
        if (launcher != null && launcher.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
            Spawn("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{launcher}\"");
        else if (launcher != null)
            SpawnVisible("cmd.exe", $"/c \"{launcher}\"", Path.GetDirectoryName(launcher));
        else if (S("ninfer", "exe") is string exe && S("ninfer", "model") is string model)
            Spawn(exe, $"\"{model}\" --host 127.0.0.1 --port {NinferPort}", Path.GetDirectoryName(exe));
    }

    void OpenTerminal(string dir, string command)
    {
        if ((cfg["terminal"] as JsonValue)?.GetValue<string>() == "wt") SpawnVisible("wt.exe", $"-d \"{dir}\" {command}");
        else SpawnVisible("cmd.exe", $"/k {command}", dir);
    }

    string? Action(string tool, string act)
    {
        if (tool is "ninfer" or "hermes" or "openwebui" or "opencode" or "claude" or "comfyui" or "pinokio" or "luminar" && !Has(tool))
            return "Dieses Tool wurde auf diesem PC nicht gefunden (Tray-Menü → „Tools neu erkennen“).";
        switch (tool, act)
        {
            case ("ninfer", "start"): StartNinfer(); return "NInfer wird gestartet …";
            case ("ninfer", "stop"): StopNinfer(); return "NInfer gestoppt. Grafikspeicher ist frei.";
            case ("ninfer", "restart"): StopNinfer(); Thread.Sleep(4000); StartNinfer(); return "NInfer startet neu …";
            case ("ninfer", "monitor"): OpenUrl($"http://127.0.0.1:{NinferPort}/monitor"); return "Monitor geöffnet.";

            case ("hermes", "chat"):
                if (!Has("openwebui")) return "Für den Hermes-Chat wird Open WebUI benötigt – nicht gefunden.";
                var owPort = I("openwebui", "port");
                if (!PortOpen(owPort)) { SpawnVisible(S("openwebui", "exe")!, ""); return "Open WebUI startet – danach „Hermes“ als Modell wählen."; }
                OpenUrl($"http://127.0.0.1:{owPort}/?models={Uri.EscapeDataString(S("hermes", "api_model") ?? "Hermes")}");
                return "Hermes-Chat in Open WebUI geöffnet.";
            case ("hermes", "dashboard"):
                if (PortOpen(9119)) OpenUrl("http://127.0.0.1:9119");
                else Spawn(S("hermes", "exe")!, "dashboard --skip-build");
                return "Hermes-Dashboard wird geöffnet …";
            case ("hermes", "restart"): Spawn(S("hermes", "exe")!, "gateway restart"); return "Hermes-Gateway startet neu …";

            case ("openwebui", "open"): SpawnVisible(S("openwebui", "exe")!, ""); return "Open WebUI wird geöffnet.";

            case ("opencode", "terminal"): OpenTerminal(Projects, "opencode"); return $"OpenCode im Terminal geöffnet ({Projects}).";
            case ("opencode", "folder"):
                form.BeginInvoke(() =>
                {
                    using var dlg = new FolderBrowserDialog { SelectedPath = Projects, Description = "Projektordner für OpenCode", UseDescriptionForTitle = true };
                    if (dlg.ShowDialog(form) == DialogResult.OK) OpenTerminal(dlg.SelectedPath, "opencode");
                });
                return "Projektordner wählen …";

            case ("claude" or "comfyui" or "pinokio" or "luminar", "open" or "start"):
                OpenApp(S(tool, "appid")!);
                return tool == "comfyui" ? "ComfyUI startet – braucht viel Grafikspeicher, NInfer ggf. vorher stoppen." : "Wird geöffnet …";

            case ("llama", "info"):
                return cfg["llama"]?["retired"]?.GetValue<bool>() == true
                    ? "llama.cpp ist ausgemustert – ersetzt durch NInfer." : "llama.cpp wurde gefunden.";

            case ("all", "redetect"):
                form.BeginInvoke(form.Redetect);
                return "Erkenne Tools neu … (dauert einige Sekunden)";
            case ("all", "vram"): StopNinfer(); return "Modellserver gestoppt – Grafikspeicher freigegeben.";
            case ("all", "logs"):
                if (S("ninfer", "log") is string log) { SpawnVisible("notepad.exe", $"\"{log}\""); return "NInfer-Log geöffnet."; }
                return "Kein NInfer-Log gefunden.";
            case ("all", "stopall"):
                StopNinfer(); Run("taskkill.exe", "/F /T /IM \"Comfy Desktop.exe\"");
                return "Modellserver und ComfyUI gestoppt.";
        }
        return null;
    }

    // ------------------------------------------------------------ Layout
    static JsonObject DefaultLayout() => new()
    {
        ["agents"] = new JsonArray(DefAgents.Select(x => (JsonNode)x).ToArray()),
        ["creative"] = new JsonArray(DefCreative.Select(x => (JsonNode)x).ToArray()),
        ["hidden"] = new JsonArray(),
    };

    static JsonObject Clean(JsonObject? data)
    {
        if (data == null) return DefaultLayout();
        var seen = new HashSet<string>();
        var outp = new JsonObject();
        foreach (var key in new[] { "agents", "creative", "hidden" })
        {
            var list = new JsonArray();
            if (data[key] is JsonArray a)
                foreach (var n in a)
                {
                    var id = n?.GetValue<string>();
                    if (id != null && TileIds.Contains(id) && seen.Add(id)) list.Add(id);
                }
            outp[key] = list;
        }
        foreach (var id in DefAgents.Concat(DefCreative))
            if (seen.Add(id)) ((JsonArray)outp["agents"]!).Add(id);
        return outp;
    }

    static JsonObject LoadLayout()
    {
        try { if (File.Exists(LayoutFile)) return Clean(JsonNode.Parse(File.ReadAllText(LayoutFile)) as JsonObject); } catch { }
        return DefaultLayout();
    }

    static void SaveLayout(JsonObject data)
    {
        Directory.CreateDirectory(Detector.DataDir);
        var tmp = LayoutFile + ".tmp";
        File.WriteAllText(tmp, Clean(data).ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, LayoutFile, true);
    }
}
