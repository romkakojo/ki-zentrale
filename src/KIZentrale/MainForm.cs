using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Win32;

namespace KIZentrale;

/// <summary>
/// Randloses Widget-Fenster mit WebView2-Oberflaeche, ohne Taskleisten-Eintrag, mit Tray-Symbol.
/// Die Oberflaeche (widget.html) laeuft unter https://kiz.app/ ; alle /api/*-Aufrufe
/// beantwortet die App selbst (kein Netzwerk-Server, kein offener Port).
/// </summary>
public sealed class MainForm : Form
{
    const int WidthDip = 460, MaxHeightDip = 1000, MarginDip = 12;
    const string Host = "https://kiz.app/";
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    readonly WebView2 web = new() { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.FromArgb(27, 28, 30) };
    readonly NotifyIcon tray = new();
    readonly ToolStripMenuItem miStatus = new("NInfer: …") { Enabled = false };
    readonly ToolStripMenuItem miAutostart = new("Mit Windows starten") { CheckOnClick = true };
    readonly ToolStripMenuItem miMonitor = new("Systemmonitor (CPU/RAM/GPU)");
    readonly MonitorForm monitor = new();
    readonly System.Windows.Forms.Timer watchdog = new() { Interval = 4000 };
    readonly Backend backend;
    readonly EventWaitHandle showEvent, monitorEvent;
    readonly bool monitorOnlyStart;
    string? firstRunSummary;
    bool detecting;
    Icon? iconOn, iconOff;
    bool quitting, placed;

    public MainForm(EventWaitHandle showEvent, EventWaitHandle monitorEvent, bool monitorOnlyStart)
    {
        this.showEvent = showEvent;
        this.monitorEvent = monitorEvent;
        this.monitorOnlyStart = monitorOnlyStart;
        // Erster Start (keine config.json): Tools und Anbindungen selbst erkennen
        var cfg = Detector.Load();
        if (cfg == null)
        {
            cfg = Detector.Detect();
            Detector.Save(cfg);
            firstRunSummary = Detector.Summary(cfg);
        }
        backend = new Backend(this, cfg);
        Text = "KI-Zentrale";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(27, 28, 30);
        MinimumSize = new Size(300, 400);
        Icon = LoadIcon();
        Controls.Add(web);
        SetupTray();
        watchdog.Tick += (_, _) => KeepVisible();
        watchdog.Start();
        new Thread(WaitForShowRequests) { IsBackground = true }.Start();
        new Thread(WaitForMonitorRequests) { IsBackground = true }.Start();
        monitor.VisibilityChanged += () => miMonitor.Checked = monitor.Visible;
        _ = InitWebAsync();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        if (monitorOnlyStart || monitor.ShouldShowAtStart) monitor.ShowMonitor();
        if (firstRunSummary != null)
        {
            var text = firstRunSummary;
            firstRunSummary = null;
            BeginInvoke(() => ShowSummary("Willkommen – das hat die KI-Zentrale auf diesem PC gefunden:", text));
        }
    }

    void ShowSummary(string head, string text) =>
        MessageBox.Show(this, head + "\n\n" + text + "\nNicht gefundene Tools werden ausgeblendet. Nach Änderungen: Tray-Menü → „Tools neu erkennen“.",
            "KI-Zentrale", MessageBoxButtons.OK, MessageBoxIcon.Information);

    /// <summary>Erkennung erneut ausfuehren (STA-Thread), Konfiguration speichern, Oberflaeche neu laden.</summary>
    public void Redetect()
    {
        if (detecting) return;
        detecting = true;
        tray.Text = "KI-Zentrale – erkenne Tools …";
        var t = new Thread(() =>
        {
            var cfg = Detector.Detect();
            Detector.Save(cfg);
            BeginInvoke(() =>
            {
                backend.SetConfig(cfg);
                detecting = false;
                web.CoreWebView2?.Reload();
                ShowSummary("Erkennung abgeschlossen:", Detector.Summary(cfg));
            });
        });
        t.SetApartmentState(ApartmentState.STA);
        t.IsBackground = true;
        t.Start();
    }

    void WaitForMonitorRequests()
    {
        while (true)
        {
            monitorEvent.WaitOne();
            try { BeginInvoke(monitor.ShowMonitor); } catch { return; }
        }
    }

    // Kein Taskleisten-Eintrag, kein Alt-Tab (Werkzeugfenster), mit Schatten.
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x80;          // WS_EX_TOOLWINDOW
            cp.ExStyle &= ~0x40000;      // ohne WS_EX_APPWINDOW
            cp.ClassStyle |= 0x20000;    // CS_DROPSHADOW
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int round = 2; // Windows 11: abgerundete Ecken
        DwmSetWindowAttribute(Handle, 33, ref round, sizeof(int));
        int dark = 1;
        DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        PlaceWindow();
        if (monitorOnlyStart) BeginInvoke(Hide);   // Start ueber die Monitor-Verknuepfung: nur den Monitor zeigen
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!quitting && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;   // Schliessen = in den Tray
            Hide();
            return;
        }
        tray.Visible = false;
        base.OnFormClosing(e);
    }

    // ------------------------------------------------------------ WebView2
    async Task InitWebAsync()
    {
        // Gemeinsame Umgebung mit dem Systemmonitor (ohne GPU-Beschleunigung, siehe WebHost)
        await web.EnsureCoreWebView2Async(await WebHost.EnvAsync());
        var core = web.CoreWebView2;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsZoomControlEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.AreBrowserAcceleratorKeysEnabled = false;
        core.AddWebResourceRequestedFilter(Host + "*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += OnWebResourceRequested;
        core.WebMessageReceived += OnWebMessage;
        core.NewWindowRequested += (_, a) => { a.Handled = true; Backend.OpenUrl(a.Uri); };
        core.Navigate(Host);
    }

    void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string msg;
        try { msg = e.TryGetWebMessageAsString(); } catch { return; }
        if (msg == "drag")
        {
            ReleaseCapture();
            SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);   // WM_NCLBUTTONDOWN, HTCAPTION
        }
        else if (msg == "hide") Hide();
        else if (msg == "monitor") monitor.ToggleMonitor();
    }

    async void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        var deferral = e.GetDeferral();
        try
        {
            var uri = new Uri(e.Request.Uri);
            string path = uri.AbsolutePath;
            string method = e.Request.Method;
            string body = "";
            if (e.Request.Content != null)
            {
                using var sr = new StreamReader(e.Request.Content, Encoding.UTF8);
                body = await sr.ReadToEndAsync();
            }
            if (path == "/" || path == "/index.html")
            {
                e.Response = Respond(LoadResource("widget.html"), "text/html; charset=utf-8");
                return;
            }
            if (path.StartsWith("/api/"))
            {
                // Rechenarbeit (Prozesse, Ports, nvidia-smi) im Hintergrund
                var (code, json) = await Task.Run(() => backend.Handle(method, path, body));
                e.Response = Respond(Encoding.UTF8.GetBytes(json), "application/json; charset=utf-8", code);
                return;
            }
            e.Response = Respond(Encoding.UTF8.GetBytes("nicht gefunden"), "text/plain", 404);
        }
        catch (Exception ex)
        {
            e.Response = Respond(Encoding.UTF8.GetBytes("{\"error\":\"" + ex.Message.Replace("\"", "'") + "\"}"),
                                 "application/json", 500);
        }
        finally { deferral.Complete(); }
    }

    CoreWebView2WebResourceResponse Respond(byte[] data, string ctype, int code = 200)
    {
        var reason = code == 200 ? "OK" : "Error";
        return web.CoreWebView2.Environment.CreateWebResourceResponse(
            new MemoryStream(data), code, reason, $"Content-Type: {ctype}\r\nCache-Control: no-store");
    }

    // ------------------------------------------------------------ Fenster
    public void ShowWidget()
    {
        if (!Visible) Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        if (!placed || !MostlyVisible()) PlaceWindow();
        Activate();
    }

    public void HideWidget() => Hide();

    public void ToggleWidget()
    {
        if (Visible && WindowState != FormWindowState.Minimized) Hide();
        else ShowWidget();
    }

    public void Quit()
    {
        quitting = true;
        tray.Visible = false;
        Application.Exit();
    }

    /// <summary>Rechts am Rand des Hauptbildschirms, Groesse in DIP * Monitor-Skalierung.</summary>
    void PlaceWindow()
    {
        var scr = Screen.PrimaryScreen ?? Screen.AllScreens[0];
        var wa = scr.WorkingArea;                      // physische Pixel (PerMonitorV2)
        double scale = MonitorScale(scr);
        int margin = (int)(MarginDip * scale);
        int w = Math.Min((int)(WidthDip * scale), wa.Width - 2 * margin);
        int h = Math.Min((int)(MaxHeightDip * scale), wa.Height - 2 * margin);
        SetBounds(wa.Right - w - margin, wa.Top + margin, w, h);
        placed = true;
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

    bool MostlyVisible()
    {
        var r = Bounds;
        long area = Math.Max(1L, (long)r.Width * r.Height);
        long best = 0;
        foreach (var s in Screen.AllScreens)
        {
            var i = Rectangle.Intersect(r, s.WorkingArea);
            best = Math.Max(best, (long)i.Width * i.Height);
        }
        return best >= 0.6 * area;
    }

    void KeepVisible()
    {
        if (Visible && WindowState == FormWindowState.Minimized) { Hide(); WindowState = FormWindowState.Normal; return; }
        if (Visible && !MostlyVisible()) PlaceWindow();
        _ = UpdateTrayStatusAsync();
    }

    void WaitForShowRequests()
    {
        while (true)
        {
            showEvent.WaitOne();
            try { BeginInvoke(ShowWidget); } catch { return; }
        }
    }

    // ------------------------------------------------------------ Tray
    void SetupTray()
    {
        iconOn = MakeTrayIcon(Color.FromArgb(255, 122, 26));
        iconOff = MakeTrayIcon(Color.FromArgb(110, 115, 122));
        var menu = new ContextMenuStrip();
        var miShow = new ToolStripMenuItem("Widget anzeigen", null, (_, _) => ShowWidget()) { Font = new Font(SystemFonts.MenuFont!, FontStyle.Bold) };
        menu.Items.Add(miShow);
        menu.Items.Add(new ToolStripMenuItem("Widget ausblenden", null, (_, _) => HideWidget()));
        miMonitor.Click += (_, _) => monitor.ToggleMonitor();
        menu.Items.Add(miMonitor);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(miStatus);
        menu.Items.Add(new ToolStripSeparator());
        miAutostart.Checked = IsAutostart();
        miAutostart.CheckedChanged += (_, _) => SetAutostart(miAutostart.Checked);
        menu.Items.Add(new ToolStripMenuItem("Tools neu erkennen", null, (_, _) => Redetect()));
        menu.Items.Add(new ToolStripMenuItem("Erkannte Tools anzeigen", null, (_, _) =>
            ShowSummary("Erkannte Tools (Stand " + (Detector.Load()?["detected_at"]?.ToString() ?? "?") + "):",
                        Detector.Summary(Detector.Load() ?? new System.Text.Json.Nodes.JsonObject()))));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(miAutostart);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("KI-Zentrale beenden", null, (_, _) => Quit()));
        tray.ContextMenuStrip = menu;
        tray.Icon = iconOn;
        tray.Text = "KI-Zentrale";
        tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ToggleWidget(); };
        tray.Visible = true;
    }

    async Task UpdateTrayStatusAsync()
    {
        var (ready, running, ctx) = await Task.Run(backend.NinferQuick);
        tray.Icon = ready ? iconOn : iconOff;
        miStatus.Text = ready ? $"NInfer: bereit ({ctx / 1024}k Kontext)" : running ? "NInfer: startet …" : "NInfer: aus";
        tray.Text = "KI-Zentrale – " + miStatus.Text;
    }

    static bool IsAutostart()
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunKey);
        return k?.GetValue("KI-Zentrale") is string;
    }

    static void SetAutostart(bool on)
    {
        using var k = Registry.CurrentUser.CreateSubKey(RunKey);
        if (on) k.SetValue("KI-Zentrale", $"\"{Environment.ProcessPath}\"");
        else k.DeleteValue("KI-Zentrale", false);
    }

    static Icon MakeTrayIcon(Color dot)
    {
        var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = RoundRect(new RectangleF(1, 1, 29, 29), 7);
            using var bg = new LinearGradientBrush(new Rectangle(0, 0, 32, 32), Color.FromArgb(72, 75, 80), Color.FromArgb(28, 30, 33), 45f);
            g.FillPath(bg, path);
            using var pen = new Pen(Color.FromArgb(210, 229, 231, 235), 2.2f);
            for (int a = 0; a < 360; a += 45)
            {
                double r = a * Math.PI / 180;
                g.DrawLine(pen, (float)(16 + 7 * Math.Cos(r)), (float)(16 + 7 * Math.Sin(r)), (float)(16 + 11 * Math.Cos(r)), (float)(16 + 11 * Math.Sin(r)));
            }
            using var b = new SolidBrush(dot);
            g.FillEllipse(b, 11, 11, 10, 10);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    static GraphicsPath RoundRect(RectangleF r, float rad)
    {
        var p = new GraphicsPath();
        float d = rad * 2;
        p.AddArc(r.Left, r.Top, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    // ------------------------------------------------------------ Ressourcen
    static byte[] LoadResource(string name)
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)!;
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    static Icon? LoadIcon()
    {
        try { return new Icon(new MemoryStream(LoadResource("kiz.ico"))); } catch { return null; }
    }

    // ------------------------------------------------------------ Win32
    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(Point pt, uint flags);
    [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr hmon, int type, out uint dpiX, out uint dpiY);
}
