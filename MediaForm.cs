using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

// Oberfläche zum Auswählen des Hintergrunds (Videos/Bilder) und zum Live-Wechseln.
namespace CyberWallpaperFX
{
    // Gespeicherte Auswahl + selbst hinzugefügte Dateien (settings.txt neben der EXE)
    static class Settings
    {
        public static string Current = "";
        public static readonly List<string> Items = new List<string>();

        static string FilePath { get { return Path.Combine(Application.StartupPath, "settings.txt"); } }

        public static void Load()
        {
            Current = ""; Items.Clear();
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (string raw in File.ReadAllLines(FilePath))
                {
                    string line = raw.Trim();
                    if (line.StartsWith("current=")) Current = line.Substring(8).Trim();
                    else if (line.StartsWith("item="))
                    {
                        string p = line.Substring(5).Trim();
                        if (p.Length > 0 && !Contains(p)) Items.Add(p);
                    }
                }
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                var lines = new List<string>();
                lines.Add("# CyberWallpaper - Einstellungen (wird vom Programm verwaltet)");
                lines.Add("current=" + Current);
                foreach (string p in Items) lines.Add("item=" + p);
                File.WriteAllLines(FilePath, lines.ToArray(), System.Text.Encoding.UTF8);
            }
            catch { }
        }

        public static bool Contains(string p)
        {
            foreach (string it in Items) if (Media.SamePath(it, p)) return true;
            return false;
        }
    }

    // Hilfen rund um Mediendateien
    static class Media
    {
        static readonly string[] VideoExt = { ".mp4", ".m4v", ".mov", ".mkv", ".webm" };
        static readonly string[] ImageExt = { ".png", ".jpg", ".jpeg", ".bmp" };

        public static string OriginalBackground { get { return Path.Combine(Application.StartupPath, "background.png"); } }
        public static string VideoDir { get { return Path.Combine(Application.StartupPath, Config.VideoFolder); } }

        static bool HasExt(string path, string[] exts)
        {
            string e = Path.GetExtension(path).ToLowerInvariant();
            foreach (string x in exts) if (e == x) return true;
            return false;
        }
        public static bool IsVideo(string path) { return HasExt(path, VideoExt); }
        public static bool IsImage(string path) { return HasExt(path, ImageExt); }

        public static bool SamePath(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            try { return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }

        public const string FileDialogFilter =
            "Videos und Bilder|*.mp4;*.m4v;*.mov;*.mkv;*.webm;*.png;*.jpg;*.jpeg;*.bmp" +
            "|Videos|*.mp4;*.m4v;*.mov;*.mkv;*.webm" +
            "|Bilder|*.png;*.jpg;*.jpeg;*.bmp";

        // Alles für die Liste: Standard-Bild, Inhalt des videos-Ordners, selbst hinzugefügte Dateien
        public static List<string> Gather()
        {
            var list = new List<string>();
            var seen = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            Action<string> add = p =>
            {
                string full;
                try { full = Path.GetFullPath(p); } catch { return; }
                if (seen.ContainsKey(full)) return;
                seen[full] = true; list.Add(full);
            };
            if (File.Exists(OriginalBackground)) add(OriginalBackground);
            if (Directory.Exists(VideoDir))
            {
                string[] files = Directory.GetFiles(VideoDir);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                foreach (string f in files) if (IsVideo(f) || IsImage(f)) add(f);
            }
            foreach (string it in Settings.Items) add(it);
            return list;
        }
    }

    // Das Fenster
    class MediaForm : Form
    {
        static readonly Color ColBg      = Color.FromArgb(16, 12, 26);
        static readonly Color ColPanel   = Color.FromArgb(24, 19, 38);
        static readonly Color ColText    = Color.FromArgb(226, 224, 240);
        static readonly Color ColDim     = Color.FromArgb(140, 134, 165);
        static readonly Color ColCyan    = Color.FromArgb(0, 214, 255);
        static readonly Color ColMagenta = Color.FromArgb(255, 60, 200);
        static readonly Color ColBad     = Color.FromArgb(255, 110, 110);
        static readonly Color ColBorder  = Color.FromArgb(60, 40, 100);

        readonly WForm wall;
        readonly ListView list;
        readonly Label status;
        readonly Button btnAuto;
        readonly Timer statusTimer;
        string flashMsg; bool flashBad; DateTime flashUntil;
        string lastCurrent = "";

        public MediaForm(WForm wall)
        {
            this.wall = wall;
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "Wallpaper – Hintergrund wählen";
            Icon = SystemIcons.Application;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(780, 480);
            MinimumSize = new Size(560, 360);
            BackColor = ColBg; ForeColor = ColText;
            Font = new Font("Segoe UI", 10f);

            var header = new Label
            {
                Text = "Hintergrund wählen", Dock = DockStyle.Top, Height = 46,
                Padding = new Padding(14, 12, 0, 0), Font = new Font("Segoe UI Semibold", 14f), ForeColor = ColCyan
            };
            var hint = new Label
            {
                Text = "Doppelklick oder Anwenden wechselt sofort · neue Videos in den videos-Ordner legen oder hinzufügen",
                Dock = DockStyle.Top, Height = 30, Padding = new Padding(16, 0, 8, 6),
                ForeColor = ColDim, Font = new Font("Segoe UI", 9f), AutoEllipsis = true
            };

            list = new ListView
            {
                Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false,
                HideSelection = false, BorderStyle = BorderStyle.None, BackColor = ColPanel, ForeColor = ColText,
                HeaderStyle = ColumnHeaderStyle.Nonclickable, OwnerDraw = true
            };
            list.SmallImageList = new ImageList { ImageSize = new Size(1, 30) };   // höhere Zeilen
            list.Columns.Add("Name", 330);
            list.Columns.Add("Typ", 120);
            list.Columns.Add("Größe", 90, HorizontalAlignment.Right);
            list.Columns.Add("Ort", 200);
            list.DrawColumnHeader += (s, e) =>
            {
                using (var b = new SolidBrush(ColBg)) e.Graphics.FillRectangle(b, e.Bounds);
                var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                            (e.Header.TextAlign == HorizontalAlignment.Right ? TextFormatFlags.Right : TextFormatFlags.Left);
                var r = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, e.Header.Text, list.Font, r, ColDim, flags);
            };
            list.DrawItem += (s, e) => { e.DrawDefault = true; };
            list.DrawSubItem += (s, e) => { e.DrawDefault = true; };
            list.SizeChanged += (s, e) => FitLastColumn();   // letzte Spalte füllt die Breite -> kein weißer Kopf-Rest
            list.DoubleClick += (s, e) => ApplySelected();
            list.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { ApplySelected(); e.Handled = true; }
                else if (e.KeyCode == Keys.Delete) { RemoveSelected(); e.Handled = true; }
            };

            var bar = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom, Height = 58, Padding = new Padding(12, 12, 12, 8),
                FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = ColBg
            };
            var btnApply  = MakeButton("Anwenden", ColCyan, true);
            var btnAdd    = MakeButton("Hinzufügen …", ColMagenta, false);
            var btnRemove = MakeButton("Entfernen", ColDim, false);
            var btnFolder = MakeButton("videos-Ordner öffnen", ColDim, false);
            btnAuto       = MakeButton("Mit Windows starten", ColDim, false);
            btnApply.Click  += (s, e) => ApplySelected();
            btnAdd.Click    += (s, e) => AddFiles();
            btnRemove.Click += (s, e) => RemoveSelected();
            btnFolder.Click += (s, e) => OpenFolder();
            btnAuto.Click   += (s, e) => ToggleAutostart();
            bar.Controls.AddRange(new Control[] { btnApply, btnAdd, btnRemove, btnFolder, btnAuto });
            UpdateAutoButton();

            status = new Label
            {
                Dock = DockStyle.Bottom, Height = 32, Padding = new Padding(14, 7, 8, 0),
                ForeColor = ColText, BackColor = ColPanel, AutoEllipsis = true
            };

            // Reihenfolge wichtig fürs Andocken: Füllfläche zuerst, äußerste Ränder zuletzt
            Controls.Add(list);
            Controls.Add(hint);
            Controls.Add(header);
            Controls.Add(bar);
            Controls.Add(status);

            statusTimer = new Timer { Interval = 500 };
            statusTimer.Tick += (s, e) => UpdateStatus();
            VisibleChanged += (s, e) => { statusTimer.Enabled = Visible; };
            // Schließen versteckt nur – das Wallpaper läuft weiter
            FormClosing += (s, e) => { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };

            RefreshList();
        }

        Button MakeButton(string text, Color accent, bool primary)
        {
            var b = new Button
            {
                Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlatStyle = FlatStyle.Flat,
                ForeColor = primary ? ColBg : ColText, BackColor = primary ? accent : ColPanel,
                Padding = new Padding(12, 3, 12, 3), Margin = new Padding(0, 0, 8, 0), Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderColor = accent;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = primary ? ControlPaint.Light(accent) : Color.FromArgb(44, 36, 66);
            if (primary) b.Font = new Font("Segoe UI Semibold", 10f);
            return b;
        }

        // Titelleiste in Programmfarbe (Windows 11)
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            TitleBar.Apply(Handle, ColBg, ColText, ColBorder);
        }

        // ---------- Autostart ----------
        void UpdateAutoButton()
        {
            bool on = Autostart.IsEnabled();
            btnAuto.Text = on ? "Mit Windows starten: An" : "Mit Windows starten: Aus";
            btnAuto.FlatAppearance.BorderColor = on ? ColCyan : ColDim;
            btnAuto.ForeColor = on ? ColCyan : ColText;
        }

        void ToggleAutostart()
        {
            try
            {
                if (Autostart.IsEnabled()) { Autostart.Disable(); Flash("Startet nicht mehr automatisch mit Windows.", false); }
                else { Autostart.Enable(Application.ExecutablePath); Flash("Startet ab jetzt automatisch mit Windows.", false); }
            }
            catch (Exception ex) { Flash("Autostart konnte nicht geändert werden: " + ex.Message, true); }
            UpdateAutoButton();
        }

        void FitLastColumn()
        {
            if (list.Columns.Count == 0) return;
            int used = 0;
            for (int i = 0; i < list.Columns.Count - 1; i++) used += list.Columns[i].Width;
            int rest = list.ClientSize.Width - used;
            ColumnHeader last = list.Columns[list.Columns.Count - 1];
            if (rest > 60 && last.Width != rest) last.Width = rest;
        }

        public void ShowAndActivate()
        {
            UpdateAutoButton();   // könnte sich außerhalb geändert haben (z. B. durch den Installer)
            RefreshList();
            if (!Visible) Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            TopMost = true; TopMost = false;   // sicher nach vorne holen
            Activate();
            UpdateStatus();
        }

        // ---------- Liste ----------
        void RefreshList()
        {
            string selPath = SelectedPath();
            string cur = wall.CurrentMedia;
            list.BeginUpdate();
            list.Items.Clear();
            foreach (string p in Media.Gather())
            {
                bool exists = File.Exists(p);
                bool isCur = Media.SamePath(p, cur);
                var it = new ListViewItem((isCur ? "▶  " : "     ") + Path.GetFileName(p));
                it.Tag = p;
                it.SubItems.Add(KindText(p));
                it.SubItems.Add(exists ? SizeText(p) : "–");
                it.SubItems.Add(exists ? PlaceText(p) : "Datei fehlt");
                if (!exists) it.ForeColor = ColDim;
                if (isCur) { it.ForeColor = ColCyan; it.Font = new Font(list.Font, FontStyle.Bold); }
                list.Items.Add(it);
                if (selPath != null ? Media.SamePath(p, selPath) : isCur) { it.Selected = true; it.Focused = true; }
            }
            list.EndUpdate();
            FitLastColumn();   // eine Scrollleiste ändert die nutzbare Breite
            if (list.SelectedItems.Count > 0) list.SelectedItems[0].EnsureVisible();
            lastCurrent = cur ?? "";
        }

        string SelectedPath() { return list.SelectedItems.Count > 0 ? (string)list.SelectedItems[0].Tag : null; }

        void SelectPath(string p)
        {
            foreach (ListViewItem it in list.Items)
                if (Media.SamePath((string)it.Tag, p)) { it.Selected = true; it.Focused = true; it.EnsureVisible(); list.Focus(); return; }
        }

        static string KindText(string p)
        {
            if (Media.IsVideo(p)) return "Video";
            return Media.SamePath(p, Media.OriginalBackground) ? "Bild + Effekte" : "Bild";
        }

        static string SizeText(string p)
        {
            try { return (new FileInfo(p).Length / 1048576.0).ToString("0.0") + " MB"; } catch { return "–"; }
        }

        static string PlaceText(string p)
        {
            if (Media.SamePath(p, Media.OriginalBackground)) return "Programmordner";
            string dir = Path.GetDirectoryName(p);
            return Media.SamePath(dir, Media.VideoDir) ? "videos-Ordner" : dir;
        }

        // ---------- Aktionen ----------
        void ApplySelected()
        {
            string p = SelectedPath();
            if (p == null) { Flash("Bitte zuerst einen Eintrag auswählen.", true); return; }
            if (!File.Exists(p)) { Flash("Datei nicht gefunden: " + Path.GetFileName(p), true); return; }
            Cursor = Cursors.WaitCursor;
            string err = wall.SwitchTo(p);
            Cursor = Cursors.Default;
            if (err != null) Flash("Wechsel fehlgeschlagen: " + err, true);
            else Flash("Gewechselt zu " + Path.GetFileName(p), false);
            RefreshList();
        }

        void AddFiles()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Videos oder Bilder hinzufügen";
                dlg.Filter = Media.FileDialogFilter;
                dlg.Multiselect = true;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                int added = 0; string first = null;
                List<string> known = Media.Gather();
                foreach (string f in dlg.FileNames)
                {
                    if (!Media.IsVideo(f) && !Media.IsImage(f)) continue;
                    if (first == null) first = f;
                    bool listed = false;
                    foreach (string g in known) if (Media.SamePath(g, f)) { listed = true; break; }
                    if (listed) continue;
                    Settings.Items.Add(Path.GetFullPath(f)); added++;
                }
                Settings.Save();
                RefreshList();
                if (first != null) SelectPath(first);
                Flash(added > 0 ? added + " Datei(en) hinzugefügt – Doppelklick oder Anwenden zum Abspielen."
                                : "Schon in der Liste.", false);
            }
        }

        void RemoveSelected()
        {
            string p = SelectedPath();
            if (p == null) return;
            for (int i = Settings.Items.Count - 1; i >= 0; i--)
            {
                if (Media.SamePath(Settings.Items[i], p))
                {
                    Settings.Items.RemoveAt(i); Settings.Save(); RefreshList();
                    Flash("Aus der Liste entfernt (die Datei selbst bleibt erhalten).", false);
                    return;
                }
            }
            Flash(Media.SamePath(p, Media.OriginalBackground)
                  ? "Das Standard-Bild bleibt immer in der Liste."
                  : "Liegt im videos-Ordner – zum Entfernen dort löschen.", true);
        }

        void OpenFolder()
        {
            try
            {
                Directory.CreateDirectory(Media.VideoDir);
                Process.Start("explorer.exe", "\"" + Media.VideoDir + "\"");
            }
            catch (Exception ex) { Flash("Ordner konnte nicht geöffnet werden: " + ex.Message, true); }
        }

        // ---------- Statuszeile ----------
        void Flash(string msg, bool bad) { flashMsg = msg; flashBad = bad; flashUntil = DateTime.Now.AddSeconds(5); UpdateStatus(); }

        void UpdateStatus()
        {
            if (flashMsg != null && DateTime.Now < flashUntil)
            {
                status.Text = flashMsg;
                status.ForeColor = flashBad ? ColBad : ColText;
            }
            else
            {
                flashMsg = null;
                string st = wall.StatusText;
                status.Text = "Läuft gerade:  " + st;
                status.ForeColor = st.Contains("FEHLER") ? ColBad : ColText;
            }
            if (!string.Equals(lastCurrent, wall.CurrentMedia ?? "", StringComparison.OrdinalIgnoreCase)) RefreshList();
        }
    }
}
