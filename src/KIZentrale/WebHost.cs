using System.Reflection;
using Microsoft.Web.WebView2.Core;

namespace KIZentrale;

/// <summary>Gemeinsame WebView2-Umgebung (ein Browser-Prozess fuer alle Fenster) und Ressourcen.</summary>
static class WebHost
{
    static Task<CoreWebView2Environment>? env;

    public static Task<CoreWebView2Environment> EnvAsync()
    {
        if (env != null) return env;
        var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KI-Zentrale", "WebView2");
        // Ohne GPU-Beschleunigung: kein Grafikspeicher fuer die Oberflaeche, der bleibt NInfer.
        var opts = new CoreWebView2EnvironmentOptions("--disable-gpu --disable-gpu-compositing");
        return env = CoreWebView2Environment.CreateAsync(null, dataDir, opts);
    }

    public static byte[] Resource(string name)
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)!;
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }
}
