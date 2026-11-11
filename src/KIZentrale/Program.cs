namespace KIZentrale;

static class Program
{
    /// <summary>
    /// Start ohne Parameter: KI-Zentrale anzeigen.  Start mit --monitor: Systemmonitor anzeigen.
    /// Laeuft die App schon, holt ein zweiter Start nur das gewuenschte Fenster nach vorn.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase)))
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Uninstaller.Run(args.Any(a => a.Equals("--quiet", StringComparison.OrdinalIgnoreCase)));
            return;
        }
        bool monitorOnly = args.Any(a => a.Equals("--monitor", StringComparison.OrdinalIgnoreCase));
        using var mutex = new Mutex(true, @"Local\KI-Zentrale-App", out bool first);
        var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\KI-Zentrale-Show");
        var monitorEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\KI-Zentrale-Monitor");
        if (!first)
        {
            (monitorOnly ? monitorEvent : showEvent).Set();
            return;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm(showEvent, monitorEvent, monitorOnly));
        GC.KeepAlive(mutex);
    }
}
