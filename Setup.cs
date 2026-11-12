using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;
using CyberWallpaperFX;

// Installer und Deinstaller für Cyber Wallpaper: eine einzelne EXE, Programmdateien sind eingebettet.
// Installiert nur für den aktuellen Benutzer -> keine Administratorrechte nötig.
//
//   CyberWallpaper-Setup.exe                                     Installationsfenster
//   CyberWallpaper-Setup.exe /silent [/dir:Pfad] [/noautostart] [/desktop] [/nostart]
//   Deinstallieren.exe /uninstall [/silent] [/purge] [/dir:Pfad]  (/purge löscht auch Videos + Einstellungen)
namespace CyberWallpaperSetup
{
    static class Product
    {
        public const string Name = "Cyber Wallpaper";
        public const string ExeName = "CyberWallpaper.exe";
        public const string UninstallerName = "Deinstallieren.exe";
        public const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\CyberWallpaper";
        public static readonly string[] Payload = { "CyberWallpaper.exe", "background.png", "city_mask.png", "cloud_mask.png", "LIESMICH.md" };

        public static string DefaultDir
        {
            get { return Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"), "CyberWallpaper"); }
        }
        public static string StartMenuLnk { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), Name + ".lnk"); } }
        public static string DesktopLnk   { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Name + ".lnk"); } }

        public static string Version
        {
            get
            {
                try
                {
                    using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("version.txt"))
                    using (var r = new StreamReader(s)) return r.ReadToEnd().Trim();
                }
                catch { return "dev"; }
            }
        }

        // Ordner einer bestehenden Installation (aus dem Eintrag in "Apps & Features") oder null
        public static string InstalledDir()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(UninstallKey))
                {
                    string d = k == null ? null : k.GetValue("InstallLocation") as string;
                    return (d != null && Directory.Exists(d)) ? d : null;
                }
            }
            catch { return null; }
        }

        public static string InstalledVersion()
        {
            try { using (RegistryKey k = Registry.CurrentUser.OpenSubKey(UninstallKey)) return k == null ? null : k.GetValue("DisplayVersion") as string; }
            catch { return null; }
        }
    }

    static class Installer
    {
        public static bool SamePath(string a, string b)
        {
            try { return string.Equals(Path.GetFullPath(a).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }

        static void TryDelete(string f) { try { if (File.Exists(f)) File.Delete(f); } catch { } }

        // Laufendes Wallpaper beenden: nur aus diesem Ordner (dir) oder alle (dir = null)
        public static void StopRunning(string dir)
        {
            foreach (Process p in Process.GetProcessesByName("CyberWallpaper"))
            {
                try
                {
                    if (dir == null || SamePath(Path.GetDirectoryName(p.MainModule.FileName), dir)) { p.Kill(); p.WaitForExit(5000); }
                }
                catch { }
            }
        }

        public static void Install(string dir, bool autostart, bool desktop, Action<string> step)
        {
            dir = Path.GetFullPath(dir.Trim());
            Directory.CreateDirectory(dir);
            step("Laufendes Wallpaper aus diesem Ordner beenden …");
            StopRunning(dir);

            Assembly asm = Assembly.GetExecutingAssembly();
            foreach (string f in Product.Payload)
            {
                step("Kopiere " + f + " …");
                using (Stream s = asm.GetManifestResourceStream("payload." + f))
                {
                    if (s == null) throw new InvalidOperationException("Im Installer fehlt die Datei " + f + ".");
                    using (FileStream o = File.Create(Path.Combine(dir, f))) s.CopyTo(o);
                }
            }
            Directory.CreateDirectory(Path.Combine(dir, "videos"));

            step("Deinstallationsprogramm anlegen …");
            string uninst = Path.Combine(dir, Product.UninstallerName);
            if (!SamePath(Application.ExecutablePath, uninst)) File.Copy(Application.ExecutablePath, uninst, true);

            string exe = Path.Combine(dir, Product.ExeName);
            step("Verknüpfungen anlegen …");
            const string desc = "Animiertes Wallpaper hinter den Desktop-Icons";
            ShellLink.Create(Product.StartMenuLnk, exe, null, dir, desc);
            if (desktop) ShellLink.Create(Product.DesktopLnk, exe, null, dir, desc);
            else TryDelete(Product.DesktopLnk);
            if (autostart) Autostart.Enable(exe); else Autostart.DisableIfTarget(dir);

            step("In „Apps & Features“ eintragen …");
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(Product.UninstallKey))
            {
                k.SetValue("DisplayName", Product.Name);
                k.SetValue("DisplayVersion", Product.Version);
                k.SetValue("Publisher", Product.Name);
                k.SetValue("InstallLocation", dir);
                k.SetValue("DisplayIcon", exe);
                k.SetValue("UninstallString", "\"" + uninst + "\" /uninstall");
                k.SetValue("QuietUninstallString", "\"" + uninst + "\" /uninstall /silent");
                k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                k.SetValue("EstimatedSize", (int)(DirBytes(dir) / 1024), RegistryValueKind.DWord);
            }
        }

        // Startet das installierte Wallpaper. Es darf nur eins laufen -> ein anderes laufendes wird ersetzt.
        public static void StartApp(string dir)
        {
            StopRunning(null);
            Process.Start(new ProcessStartInfo(Path.Combine(dir, Product.ExeName)) { WorkingDirectory = dir, UseShellExecute = true });
        }

        public static void Uninstall(string dir, bool purge)
        {
            dir = Path.GetFullPath(dir);
            StopRunning(dir);
            Autostart.DisableIfTarget(dir);
            // Verknüpfungen nur entfernen, wenn sie in diesen Ordner zeigen
            foreach (string l in new[] { Product.StartMenuLnk, Product.DesktopLnk })
            {
                if (!File.Exists(l)) continue;
                string t = ShellLink.GetTarget(l);
                if (t == null || t.StartsWith(dir, StringComparison.OrdinalIgnoreCase)) TryDelete(l);
            }
            try { Registry.CurrentUser.DeleteSubKeyTree(Product.UninstallKey, false); } catch { }

            // Nur bekannte Dateien löschen – nie blind ganze Ordner
            foreach (string f in Product.Payload) TryDelete(Path.Combine(dir, f));
            TryDelete(Path.Combine(dir, "CyberWallpaper.log"));
            string videos = Path.Combine(dir, "videos");
            if (purge)
            {
                TryDelete(Path.Combine(dir, "settings.txt"));
                if (Directory.Exists(videos))
                {
                    foreach (string f in Directory.GetFiles(videos)) TryDelete(f);
                    try { Directory.Delete(videos, false); } catch { }
                }
            }
            else
            {
                try { if (Directory.Exists(videos) && Directory.GetFileSystemEntries(videos).Length == 0) Directory.Delete(videos, false); } catch { }
            }
            ScheduleSelfDelete(dir);
        }

        // Das laufende Deinstallationsprogramm kann sich nicht selbst löschen -> kurz nach dem Beenden per cmd.
        // "rd" ohne /s entfernt den Ordner nur, wenn er leer ist (behaltene Videos bleiben also erhalten).
        static void ScheduleSelfDelete(string dir)
        {
            string uninst = Path.Combine(dir, Product.UninstallerName);
            var cmd = new StringBuilder("/c ping 127.0.0.1 -n 3 > nul");
            if (SamePath(Application.ExecutablePath, uninst)) cmd.Append(" & del /f /q \"" + uninst + "\"");
            else TryDelete(uninst);
            cmd.Append(" & rd \"" + dir + "\"");
            string tmp = Path.GetTempPath();
            Environment.CurrentDirectory = tmp;
            Process.Start(new ProcessStartInfo("cmd.exe", cmd.ToString())
            {
                WorkingDirectory = tmp, UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden
            });
        }

        static long DirBytes(string dir)
        {
            long n = 0;
            try { foreach (string f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories)) n += new FileInfo(f).Length; } catch { }
            return n;
        }
    }

    // Das Installationsfenster
    class InstallForm : Form
    {
        static readonly Color ColBg     = Color.FromArgb(16, 12, 26);
        static readonly Color ColPanel  = Color.FromArgb(24, 19, 38);
        static readonly Color ColText   = Color.FromArgb(226, 224, 240);
        static readonly Color ColDim    = Color.FromArgb(140, 134, 165);
        static readonly Color ColCyan   = Color.FromArgb(0, 214, 255);
        static readonly Color ColBad    = Color.FromArgb(255, 110, 110);
        static readonly Color ColBorder = Color.FromArgb(60, 40, 100);

        readonly TextBox txtDir;
        readonly CheckBox chkAuto, chkDesktop, chkStart;
        readonly Button btnInstall, btnCancel, btnBrowse;
        readonly Label lblStatus;
        bool done;

        public InstallForm(string dirArg)
        {
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            string installed = Product.InstalledDir();
            string oldVer = Product.InstalledVersion();
            Text = Product.Name + " – " + (installed != null ? "Aktualisieren" : "Installation");
            Icon = AppIcon.Full;
            FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(560, 410);
            BackColor = ColBg; ForeColor = ColText;
            Font = new Font("Segoe UI", 10f);

            var title = new Label { Text = Product.Name, Location = new Point(24, 16), AutoSize = true,
                                    Font = new Font("Segoe UI Semibold", 16f), ForeColor = ColCyan };
            var ver = new Label { Location = new Point(27, 56), AutoSize = true, ForeColor = ColDim, Font = new Font("Segoe UI", 9f),
                                  Text = "Version " + Product.Version + (installed != null ? "   ·   installiert: " + (oldVer ?? "?") : "") };
            var desc = new Label { Location = new Point(27, 88), Size = new Size(506, 46), ForeColor = ColText,
                                   Text = "Animiertes Live-Wallpaper hinter den Desktop-Icons (Bild oder Video).\n" +
                                          "Wird nur für dich installiert – keine Administratorrechte nötig." };
            var lblDir = new Label { Text = "Installationsordner", Location = new Point(27, 146), AutoSize = true, ForeColor = ColDim };
            txtDir = new TextBox { Location = new Point(28, 170), Size = new Size(420, 28), BackColor = ColPanel, ForeColor = ColText,
                                   BorderStyle = BorderStyle.FixedSingle,
                                   Text = dirArg ?? installed ?? Product.DefaultDir };
            btnBrowse = MakeButton("Ändern …", ColDim, false);
            btnBrowse.AutoSize = false; btnBrowse.Location = new Point(456, 168); btnBrowse.Size = new Size(78, 30);
            btnBrowse.Click += (s, e) => Browse();

            chkAuto    = MakeCheck("Mit Windows starten", 214, installed != null ? Autostart.IsEnabled() : true);
            chkDesktop = MakeCheck("Verknüpfung auf dem Desktop", 244, File.Exists(Product.DesktopLnk));
            chkStart   = MakeCheck("Nach der Installation starten (ersetzt ein laufendes Wallpaper)", 274, true);

            lblStatus = new Label { Location = new Point(27, 316), Size = new Size(506, 40), ForeColor = ColDim,
                                    Text = installed != null ? "Videos und Einstellungen im Programmordner bleiben erhalten." : "" };

            btnInstall = MakeButton(installed != null ? "Aktualisieren" : "Installieren", ColCyan, true);
            btnInstall.AutoSize = false; btnInstall.Location = new Point(290, 362); btnInstall.Size = new Size(136, 34);
            btnCancel = MakeButton("Abbrechen", ColDim, false);
            btnCancel.AutoSize = false; btnCancel.Location = new Point(434, 362); btnCancel.Size = new Size(100, 34);
            btnInstall.Click += (s, e) => { if (done) Close(); else DoInstall(); };
            btnCancel.Click += (s, e) => Close();
            AcceptButton = btnInstall; CancelButton = btnCancel;

            Controls.AddRange(new Control[] { title, ver, desc, lblDir, txtDir, btnBrowse, chkAuto, chkDesktop, chkStart, lblStatus, btnInstall, btnCancel });
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            TitleBar.Apply(Handle, ColBg, ColText, ColBorder);   // Titelleiste in Programmfarbe
        }

        Button MakeButton(string text, Color accent, bool primary)
        {
            var b = new Button
            {
                Text = text, AutoSize = true, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
                ForeColor = primary ? ColBg : ColText, BackColor = primary ? accent : ColPanel
            };
            b.FlatAppearance.BorderColor = accent;
            b.FlatAppearance.MouseOverBackColor = primary ? ControlPaint.Light(accent) : Color.FromArgb(44, 36, 66);
            if (primary) b.Font = new Font("Segoe UI Semibold", 10f);
            return b;
        }

        CheckBox MakeCheck(string text, int y, bool on)
        {
            var c = new CheckBox { Text = text, Location = new Point(28, y), AutoSize = true, Checked = on,
                                   FlatStyle = FlatStyle.Flat, ForeColor = ColText, Cursor = Cursors.Hand };
            c.FlatAppearance.BorderColor = ColCyan;
            c.FlatAppearance.CheckedBackColor = ColPanel;
            return c;
        }

        void Browse()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Ordner für Cyber Wallpaper wählen";
                dlg.SelectedPath = txtDir.Text;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    txtDir.Text = Path.GetFileName(dlg.SelectedPath).Equals("CyberWallpaper", StringComparison.OrdinalIgnoreCase)
                        ? dlg.SelectedPath : Path.Combine(dlg.SelectedPath, "CyberWallpaper");
            }
        }

        void DoInstall()
        {
            if (txtDir.Text.Trim().Length == 0) { Status("Bitte einen Installationsordner angeben.", true); return; }
            SetBusy(true);
            Cursor = Cursors.WaitCursor;
            try
            {
                string dir = Path.GetFullPath(txtDir.Text.Trim());
                Installer.Install(dir, chkAuto.Checked, chkDesktop.Checked, s => Status(s, false));
                if (chkStart.Checked) { Status("Starte Cyber Wallpaper …", false); Installer.StartApp(dir); }
                Status("Fertig! " + (chkStart.Checked
                        ? "Das Wallpaper läuft. Rechtsklick aufs Tray-Icon → „Hintergrund wählen …“."
                        : "Start über das Startmenü: „Cyber Wallpaper“."), false);
                lblStatus.ForeColor = ColCyan;
                btnInstall.Text = "Schließen"; done = true;
                btnInstall.Enabled = true;
            }
            catch (Exception ex)
            {
                Status("Fehler: " + ex.Message, true);
                SetBusy(false);
            }
            finally { Cursor = Cursors.Default; }
        }

        void SetBusy(bool busy)
        {
            foreach (Control c in new Control[] { txtDir, btnBrowse, chkAuto, chkDesktop, chkStart, btnInstall, btnCancel }) c.Enabled = !busy;
        }

        void Status(string text, bool bad)
        {
            lblStatus.Text = text;
            lblStatus.ForeColor = bad ? ColBad : ColText;
            lblStatus.Refresh();
        }
    }

    static class SetupProgram
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

        [STAThread]
        static int Main(string[] args)
        {
            bool silent = false, uninstall = false, purge = false, noAuto = false, desktop = false, noStart = false;
            string dir = null;
            foreach (string a in args)
            {
                string l = a.ToLowerInvariant();
                if (l == "/silent") silent = true;
                else if (l == "/uninstall") uninstall = true;
                else if (l == "/purge") purge = true;
                else if (l == "/noautostart") noAuto = true;
                else if (l == "/desktop") desktop = true;
                else if (l == "/nostart") noStart = true;
                else if (l.StartsWith("/dir:")) dir = a.Substring(5).Trim().Trim('"');
            }

            SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                if (uninstall) return RunUninstall(dir, silent, purge);
                if (silent)
                {
                    string d = dir ?? Product.InstalledDir() ?? Product.DefaultDir;
                    Installer.Install(d, !noAuto, desktop, s => { });
                    if (!noStart) Installer.StartApp(d);
                    return 0;
                }
                Application.Run(new InstallForm(dir));
                return 0;
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "CyberWallpaper-Setup.log"), DateTime.Now + " " + ex); } catch { }
                if (!silent) MessageBox.Show(ex.Message, Product.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        static int RunUninstall(string dirArg, bool silent, bool purge)
        {
            string dir = dirArg ?? Product.InstalledDir() ?? Path.GetDirectoryName(Application.ExecutablePath);
            bool looksInstalled = File.Exists(Path.Combine(dir, Product.ExeName)) || File.Exists(Path.Combine(dir, Product.UninstallerName));
            if (!looksInstalled && Product.InstalledDir() == null)
            {
                if (!silent) MessageBox.Show("Es wurde keine Installation von " + Product.Name + " gefunden.", Product.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 2;
            }
            if (!silent)
            {
                if (MessageBox.Show(Product.Name + " wirklich deinstallieren?\n\n" + dir, Product.Name,
                                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return 0;
                purge = MessageBox.Show("Auch deine Videos (videos-Ordner) und Einstellungen löschen?\n\nNein = sie bleiben im Ordner erhalten.",
                                        Product.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
            }
            Installer.Uninstall(dir, purge);
            if (!silent) MessageBox.Show(Product.Name + " wurde deinstalliert.", Product.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }
    }
}
