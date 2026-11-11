using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace KIZentraleSetup
{
    /// <summary>Installations-Assistent im Anthrazit-/Carbon-Stil der KI-Zentrale.</summary>
    sealed class SetupForm : Form
    {
        static readonly Color Bg = Color.FromArgb(27, 28, 30), Panel = Color.FromArgb(36, 38, 41), Line = Color.FromArgb(58, 61, 66),
            Txt = Color.FromArgb(233, 234, 236), Dim = Color.FromArgb(163, 167, 173), Accent = Color.FromArgb(255, 122, 26);

        readonly TextBox dirBox = new TextBox();
        readonly CheckBox cbDesktop = Check("Verknüpfungen auf dem Desktop (KI-Zentrale, Systemmonitor)", true);
        readonly CheckBox cbStart = Check("Einträge im Startmenü", true);
        readonly CheckBox cbAuto = Check("Mit Windows starten (nur im Infobereich)", true);
        readonly CheckBox cbLaunch = Check("KI-Zentrale nach der Installation starten", true);
        readonly Label status = new Label { ForeColor = Dim, AutoSize = false };
        readonly ProgressPanel bar = new ProgressPanel();
        readonly PillButton btnInstall = new PillButton("Installieren", true);
        readonly PillButton btnCancel = new PillButton("Abbrechen", false);
        readonly PillButton btnBrowse = new PillButton("…", false);
        bool done;

        public SetupForm()
        {
            // Alle Masse sind fuer 96 DPI angegeben und werden unten einmal mit der Anzeige-Skalierung multipliziert.
            // Schriften sind in Punkt angegeben und skalieren von selbst.
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.None;
            Text = "KI-Zentrale – Installation";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Bg;
            ForeColor = Txt;
            Font = new Font("Segoe UI", 9.5f);
            ClientSize = new Size(580, 492);

            var title = new Label { Text = "KI-Zentrale", Font = new Font("Segoe UI Semibold", 18f), ForeColor = Txt, AutoSize = true, Location = new Point(96, 26), BackColor = Color.Transparent };
            var sub = new Label { Text = $"Version {Installer.Version} · alle lokalen KI-Tools und ein Systemmonitor in einem Widget", ForeColor = Dim, AutoSize = true, Location = new Point(98, 64), BackColor = Color.Transparent };
            var logo = new PictureBox { Image = Logo(56), Size = new Size(56, 56), Location = new Point(28, 24), BackColor = Color.Transparent };

            var info = new Label
            {
                Text = "Nach der Installation erkennt die KI-Zentrale selbst, welche Tools auf diesem PC vorhanden sind " +
                       "(NInfer, llama.cpp, Hermes Agent, Open WebUI, OpenCode, Claude, ComfyUI, Pinokio, Luminar AI) " +
                       "und wie sie angebunden sind (Ports, Starter, Logs). Nicht gefundene Tools werden ausgeblendet.",
                ForeColor = Dim, Location = new Point(28, 100), Size = new Size(524, 74), BackColor = Color.Transparent,
            };

            var lblDir = new Label { Text = "Installationsordner", ForeColor = Txt, AutoSize = true, Location = new Point(28, 182), BackColor = Color.Transparent };
            dirBox.Text = Installer.ExistingInstallDir() ?? Installer.DefaultDir;
            dirBox.BorderStyle = BorderStyle.FixedSingle;
            dirBox.BackColor = Panel; dirBox.ForeColor = Txt;
            dirBox.Location = new Point(28, 206); dirBox.Size = new Size(476, 28); dirBox.SelectionStart = 0;
            btnBrowse.Location = new Point(512, 203); btnBrowse.Size = new Size(40, 30);
            btnBrowse.Click += (s, e) =>
            {
                using (var d = new FolderBrowserDialog { SelectedPath = dirBox.Text, Description = "Installationsordner wählen" })
                    if (d.ShowDialog(this) == DialogResult.OK)
                        dirBox.Text = Path.GetFileName(d.SelectedPath).Equals("KI-Zentrale", StringComparison.OrdinalIgnoreCase)
                            ? d.SelectedPath : Path.Combine(d.SelectedPath, "KI-Zentrale");
            };

            cbAuto.Checked = Installer.AutostartWasOn() || Installer.ExistingInstallDir() == null;
            int y = 250;
            foreach (var cb in new[] { cbDesktop, cbStart, cbAuto, cbLaunch }) { cb.Location = new Point(28, y); y += 30; }

            var wv = Installer.WebView2Version();
            var req = new LinkLabel
            {
                Text = wv != null ? $"✔ WebView2-Laufzeit vorhanden ({wv})" : "⚠ WebView2-Laufzeit fehlt – hier herunterladen (kostenlos von Microsoft)",
                ForeColor = wv != null ? Color.FromArgb(155, 231, 180) : Color.FromArgb(255, 192, 138),
                LinkColor = Accent, ActiveLinkColor = Accent, AutoSize = true, Location = new Point(28, y + 4), BackColor = Color.Transparent,
                LinkArea = wv != null ? new LinkArea(0, 0) : new LinkArea(30, 29),
            };
            req.LinkClicked += (s, e) => Process.Start("https://go.microsoft.com/fwlink/p/?LinkId=2124703");

            bar.Location = new Point(28, 414); bar.Size = new Size(524, 6);
            status.Location = new Point(28, 426); status.Size = new Size(300, 40);
            btnCancel.Location = new Point(344, 432); btnCancel.Size = new Size(96, 38);
            btnInstall.Location = new Point(448, 432); btnInstall.Size = new Size(104, 38);
            btnCancel.Click += (s, e) => Close();
            btnInstall.Click += async (s, e) => await DoInstall();
            AcceptButton = btnInstall;

            Controls.AddRange(new Control[] { logo, title, sub, info, lblDir, dirBox, btnBrowse, cbDesktop, cbStart, cbAuto, cbLaunch, req, bar, status, btnCancel, btnInstall });
            logo.SizeMode = PictureBoxSizeMode.Zoom;
            float f;
            using (var g = CreateGraphics()) f = g.DpiX / 96f;
            if (f > 1.01f) Scale(new SizeF(f, f));   // Positionen und Groessen einmalig hochskalieren
            ResumeLayout(false);
            PerformLayout();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ((PictureBox)Controls[0]).Image = Logo(Controls[0].Height);   // Logo scharf in Zielgroesse
        }

        async Task DoInstall()
        {
            if (done) { Close(); return; }
            var dir = dirBox.Text.Trim();
            if (dir.Length < 4 || !Path.IsPathRooted(dir)) { MessageBox.Show(this, "Bitte einen gültigen Ordner angeben.", Text); return; }
            foreach (Control c in Controls) if (c is CheckBox || c is TextBox || c == btnBrowse || c == btnCancel) c.Enabled = false;
            btnInstall.Enabled = false;
            bool d = cbDesktop.Checked, sm = cbStart.Checked, au = cbAuto.Checked, la = cbLaunch.Checked;
            try
            {
                await Task.Run(() => Installer.Install(dir, d, sm, au, la, (pct, msg) => BeginInvoke(new Action(() => { bar.Value = pct; status.Text = msg; }))));
                done = true;
                status.ForeColor = Color.FromArgb(155, 231, 180);
                status.Text = la ? "Installiert – die KI-Zentrale startet und erkennt deine Tools." : "Installation abgeschlossen.";
                btnInstall.Text = "Fertig";
                btnInstall.Enabled = true;
            }
            catch (Exception ex)
            {
                status.ForeColor = Color.FromArgb(255, 155, 155);
                status.Text = "Fehler: " + ex.Message;
                btnInstall.Enabled = true; btnCancel.Enabled = true;
            }
        }

        // Carbon-Hintergrund
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            var g = e.Graphics;
            using (var lg = new LinearGradientBrush(ClientRectangle, Color.FromArgb(42, 44, 47), Color.FromArgb(23, 24, 26), 90f)) g.FillRectangle(lg, ClientRectangle);
            using (var hatch = new HatchBrush(HatchStyle.SmallCheckerBoard, Color.FromArgb(10, 255, 255, 255), Color.Transparent)) g.FillRectangle(hatch, ClientRectangle);
            int y = bar.Top - (bar.Height * 2);
            using (var p = new Pen(Line)) g.DrawLine(p, bar.Left, y, bar.Right, y);
        }

        static CheckBox Check(string text, bool on) => new CheckBox
        {
            Text = text, Checked = on, AutoSize = true, ForeColor = Color.FromArgb(233, 234, 236), BackColor = Color.Transparent,
            FlatStyle = FlatStyle.Standard, Cursor = Cursors.Hand,
        };

        static Bitmap Logo(int s)
        {
            var bmp = new Bitmap(s, s);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float k = s / 32f;
                using (var path = Round(new RectangleF(1 * k, 1 * k, 29 * k, 29 * k), 7 * k))
                using (var bg = new LinearGradientBrush(new Rectangle(0, 0, s, s), Color.FromArgb(78, 82, 88), Color.FromArgb(26, 28, 31), 45f))
                    g.FillPath(bg, path);
                using (var pen = new Pen(Color.FromArgb(225, 229, 231, 235), 2.2f * k) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    for (int a = 0; a < 360; a += 45)
                    {
                        double r = a * Math.PI / 180, c = 16 * k;
                        g.DrawLine(pen, (float)(c + 7.5 * k * Math.Cos(r)), (float)(c + 7.5 * k * Math.Sin(r)), (float)(c + 11 * k * Math.Cos(r)), (float)(c + 11 * k * Math.Sin(r)));
                    }
                using (var b = new SolidBrush(Color.FromArgb(255, 122, 26))) g.FillEllipse(b, 11 * k, 11 * k, 10 * k, 10 * k);
            }
            return bmp;
        }

        internal static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath(); float d = rad * 2;
            p.AddArc(r.Left, r.Top, d, d, 180, 90); p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure(); return p;
        }
    }

    /// <summary>Taste im 3D-Stil der KI-Zentrale mit Druck-Animation.</summary>
    sealed class PillButton : Button
    {
        readonly bool primary; bool down, hover;
        public PillButton(string text, bool primary)
        {
            Text = text; this.primary = primary;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent; Cursor = Cursors.Hand; Font = new Font("Segoe UI Semibold", 9.5f); FlatStyle = FlatStyle.Flat;
        }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? Color.Black);
            int sh = Math.Max(4, Height / 9), off = 0;
            var face = new RectangleF(1, 1 + (down ? sh - 1 : 0), Width - 2, Height - sh - 2);
            var baseR = new RectangleF(1, 1 + sh - 1, Width - 2, Height - sh - 2);
            Color top, bot, edge, fg;
            if (primary) { top = Color.FromArgb(255, 143, 58); bot = Color.FromArgb(232, 101, 15); edge = Color.FromArgb(138, 58, 6); fg = Color.FromArgb(26, 13, 2); }
            else { top = Color.FromArgb(58, 60, 64); bot = Color.FromArgb(38, 40, 43); edge = Color.FromArgb(13, 14, 15); fg = Color.FromArgb(229, 231, 235); }
            if (!Enabled) { top = Color.FromArgb(90, top); bot = Color.FromArgb(90, bot); fg = Color.FromArgb(120, fg); }
            if (hover && Enabled && !down) { top = ControlPaint.Light(top, .08f); bot = ControlPaint.Light(bot, .08f); }
            float rad = Height * 0.26f;
            using (var pe = SetupForm.Round(baseR, rad)) using (var be = new SolidBrush(edge)) if (!down) g.FillPath(be, pe);
            using (var pf = SetupForm.Round(face, rad)) using (var bf = new LinearGradientBrush(new RectangleF(0, face.Top, 1, face.Height + 1), top, bot, 90f)) g.FillPath(bf, pf);
            TextRenderer.DrawText(g, Text, Font, Rectangle.Round(face), fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            _ = off;
        }
    }

    /// <summary>Schmaler Fortschrittsbalken Graphit -> Orange.</summary>
    sealed class ProgressPanel : Control
    {
        int value;
        public int Value { get => value; set { this.value = Math.Max(0, Math.Min(100, value)); Invalidate(); } }
        public ProgressPanel() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.FromArgb(30, 31, 33));
            using (var p = SetupForm.Round(new RectangleF(0, 0, Width - 1, Height - 1), Height / 2f)) using (var b = new SolidBrush(Color.FromArgb(14, 15, 16))) g.FillPath(b, p);
            float w = (Width - 1) * value / 100f;
            if (w < Height) return;
            using (var p = SetupForm.Round(new RectangleF(0, 0, w, Height - 1), Height / 2f))
            using (var b = new LinearGradientBrush(new RectangleF(0, 0, Width, Height), Color.FromArgb(107, 112, 120), Color.FromArgb(255, 122, 26), 0f)) g.FillPath(b, p);
        }
    }
}
