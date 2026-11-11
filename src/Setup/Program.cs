using System;
using System.Windows.Forms;

namespace KIZentraleSetup
{
    static class Program
    {
        /// <summary>KI-Zentrale-Setup.exe  [/S = ohne Fenster mit Standardoptionen]</summary>
        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (Array.Exists(args, a => a.Equals("/S", StringComparison.OrdinalIgnoreCase) || a.Equals("--silent", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    Installer.Install(Installer.DefaultDir, desktop: true, startMenu: true, autostart: Installer.AutostartWasOn(), launch: false, progress: null);
                    return 0;
                }
                catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
            }
            Application.Run(new SetupForm());
            return 0;
        }
    }
}
