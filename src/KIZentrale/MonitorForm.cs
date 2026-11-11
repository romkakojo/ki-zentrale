using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace KIZentrale;

/// <summary>
/// Kleiner Systemmonitor (CPU, RAM, GPU) im KI-Zentrale-Stil. Randlos, ohne Taskleiste.
/// Pin-Symbol: dauerhaft im Vordergrund (TopMost). Position, Pin und Sichtbarkeit werden gespeichert.
/// </summary>
public sealed class MonitorForm : Form
{
    const int WidthDip = 300, HeightDip = 340, MarginDip = 12, GapToWidgetDip = 472;
    const string Host = "https://kiz.app/";
    static readonly string StateFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KI-Zentrale", "monitor.json");

    readonly WebView2 web = new() { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.FromArgb(27, 28, 30) };
    readonly SysInfo sys = new();
    readonly System.Windows.Forms.Timer saveTimer = new() { Interval = 800 };
    readonly System.Windows.Forms.Timer guard = new() { Interval = 4000 };
    bool pinned, placed, ready;
    Point? savedPos;

    public event Action? VisibilityChanged;

    public MonitorForm()
    {
        Text = "KI-Zentrale Systemmonitor";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(27, 28, 30);
        Controls.Add(web);
        LoadState();
        TopMost = pinned;
        saveTimer.Tick += (_, _) => { saveTimer.Stop(); SaveState(); };
        guard.Tick += (_, _) => { if (Visible && !MostlyVisible()) PlaceDefault(); };
        guard.Start();
        Move += (_, _) => { if (placed) { saveTimer.Stop(); saveTimer.Start(); } };
        VisibleChanged += (_, _) => { if (ready) SaveState(); VisibilityChanged?.Invoke(); };
        _ = InitWebAsync();
    }

    public bool ShouldShowAtStart { get; private set; } = true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x80;          // WS_EX_TOOLWINDOW: keine Taskleiste, kein Alt-Tab
            cp.ExStyle &= ~0x40000;
            cp.ClassStyle |= 0x20000;    // Schatten
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int round = 2; DwmSetWindowAttribute(Handle, 33, ref round, sizeof(int));
        int dark = 1; DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (!placed) PlaceStart();
        ready = true;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); return; }
        SaveState();
        base.OnFormClosing(e);
    }

    // ------------------------------------------------------------ Anzeige
    public void ShowMonitor()
    {
        if (!Visible) Show();
        if (!placed || !MostlyVisible()) PlaceStart();
        if (pinned) TopMost = true;
        Activate();
    }

    public void ToggleMonitor()
    {
        if (Visible) Hide(); else ShowMonitor();
    }

    public void SetPinned(bool on)
    {
        pinned = on;
        TopMost = on;
        SaveState();
    }

    void PlaceStart()
    {
        if (savedPos is Point p)
        {
            var scale = DeviceDpi / 96.0;
            SetBounds(p.X, p.Y, (int)(WidthDip * scale), (int)(HeightDip * scale));
            placed = true;
            if (MostlyVisible()) return;
        }
        PlaceDefault();
    }

    /// <summary>Standard: oben rechts, links neben dem KI-Zentrale-Widget.</summary>
    void PlaceDefault()
    {
        var scr = Screen.PrimaryScreen ?? Screen.AllScreens[0];
        var wa = scr.WorkingArea;
        double scale = MonitorScale(scr);
        int w = (int)(WidthDip * scale), h = (int)(HeightDip * scale), m = (int)(MarginDip * scale);
        int x = wa.Right - (int)(GapToWidgetDip * scale) - m - w;
        if (x < wa.Left + m) x = wa.Right - w - m;
        SetBounds(x, wa.Top + m, w, h);
        placed = true;
        SaveState();
    }

    bool MostlyVisible()
    {
        var r = Bounds;
        long area = Math.Max(1L, (long)r.Width * r.Height), best = 0;
        foreach (var s in Screen.AllScreens)
        {
            var i = Rectangle.Intersect(r, s.WorkingArea);
            best = Math.Max(best, (long)i.Width * i.Height);
        }
        return best >= 0.6 * area;
    }

    static double MonitorScale(Screen scr)
    {
        try
        {
            var hmon = MonitorFromPoint(new Point(scr.Bounds.Left + 1, scr.Bounds.Top + 1), 2);
            if (GetDpiForMonitor(hmon, 0, out uint dx, out _) == 0) return dx / 96.0;
        }
        catch { }
        return 1.0;
    }

    // ------------------------------------------------------------ Zustand
    void LoadState()
    {
        try
        {
            if (JsonNode.Parse(File.ReadAllText(StateFile)) is JsonObject o)
            {
                pinned = o["pinned"]?.GetValue<bool>() ?? false;
                ShouldShowAtStart = o["visible"]?.GetValue<bool>() ?? true;
                if (o["x"] != null && o["y"] != null) savedPos = new Point(o["x"]!.GetValue<int>(), o["y"]!.GetValue<int>());
            }
        }
        catch { }
    }

    void SaveState()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StateFile)!);
            var o = new JsonObject { ["pinned"] = pinned, ["visible"] = Visible, ["x"] = Left, ["y"] = Top };
            File.WriteAllText(StateFile, o.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    // ------------------------------------------------------------ WebView2
    async Task InitWebAsync()
    {
        await web.EnsureCoreWebView2Async(await WebHost.EnvAsync());
        var core = web.CoreWebView2;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsZoomControlEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.AreBrowserAcceleratorKeysEnabled = false;
        core.AddWebResourceRequestedFilter(Host + "*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += OnRequest;
        core.WebMessageReceived += (_, e) =>
        {
            string msg;
            try { msg = e.TryGetWebMessageAsString(); } catch { return; }
            switch (msg)
            {
                case "drag": ReleaseCapture(); SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); break;
                case "pin": SetPinned(!pinned); break;
                case "hide": Hide(); break;
            }
        };
        core.Navigate(Host + "monitor.html");
    }

    async void OnRequest(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        var deferral = e.GetDeferral();
        try
        {
            var path = new Uri(e.Request.Uri).AbsolutePath;
            byte[] data; string type;
            if (path == "/monitor.html") { data = WebHost.Resource("monitor.html"); type = "text/html; charset=utf-8"; }
            else if (path == "/api/sys")
            {
                var o = await Task.Run(sys.Read);
                o["pinned"] = pinned;
                data = Encoding.UTF8.GetBytes(o.ToJsonString()); type = "application/json; charset=utf-8";
            }
            else { data = Encoding.UTF8.GetBytes("{}"); type = "application/json"; }
            e.Response = web.CoreWebView2.Environment.CreateWebResourceResponse(
                new MemoryStream(data), 200, "OK", $"Content-Type: {type}\r\nCache-Control: no-store");
        }
        catch { }
        finally { deferral.Complete(); }
    }

    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(Point pt, uint flags);
    [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr hmon, int type, out uint dpiX, out uint dpiY);
}
