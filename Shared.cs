using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

// Helfer, die Programm (CyberWallpaper.exe) und Installer (CyberWallpaper-Setup.exe) gemeinsam nutzen.
namespace CyberWallpaperFX
{
    // Programm-Icon (icon.ico, eingebettet als Ressource "app.ico" mit allen Größen; erzeugt von tools\make-icon.ps1)
    static class AppIcon
    {
        static Icon full;

        // Alle Größen; Fenster suchen sich daraus Titelleisten- und Alt+Tab-Größe selbst aus
        public static Icon Full
        {
            get
            {
                if (full == null)
                {
                    try
                    {
                        using (Stream s = typeof(AppIcon).Assembly.GetManifestResourceStream("app.ico"))
                            if (s != null) full = new Icon(s);
                    }
                    catch { }
                    if (full == null) full = SystemIcons.Application;
                }
                return full;
            }
        }

        // Passende kleine Größe fürs Tray (16 px bei 100 %, 20/24 px bei höherer Skalierung)
        public static Icon Small()
        {
            try { return new Icon(Full, SystemInformation.SmallIconSize); }
            catch { return Full; }
        }
    }

    // Start mit Windows über HKCU\...\Run (erscheint im Task-Manager unter "Autostart-Apps")
    static class Autostart
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string ValueName = "CyberWallpaper";

        // Aktueller Eintrag (z. B. "\"C:\...\CyberWallpaper.exe\" /autostart") oder null
        public static string Command()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                    return k == null ? null : k.GetValue(ValueName) as string;
            }
            catch { return null; }
        }

        public static bool IsEnabled() { return Command() != null || StartupShortcuts().Length > 0; }

        public static void Enable(string exePath)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                k.SetValue(ValueName, "\"" + exePath + "\" /autostart");
            RemoveStartupShortcuts();   // alte Verknüpfung im Autostart-Ordner würde sonst doppelt starten
        }

        public static void Disable()
        {
            try { using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true)) if (k != null) k.DeleteValue(ValueName, false); } catch { }
            RemoveStartupShortcuts();
        }

        // Nur abschalten, wenn der Eintrag in diesen Ordner zeigt (Deinstallation soll fremde Kopien nicht anfassen)
        public static void DisableIfTarget(string dir)
        {
            string cmd = Command();
            if (cmd != null && cmd.IndexOf(dir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) >= 0)
            {
                try { using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true)) if (k != null) k.DeleteValue(ValueName, false); } catch { }
            }
        }

        // Verknüpfungen im Autostart-Ordner (shell:startup), die auf CyberWallpaper zeigen (frühere Anleitung)
        public static string[] StartupShortcuts()
        {
            var res = new List<string>();
            try
            {
                string dir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                if (Directory.Exists(dir))
                    foreach (string f in Directory.GetFiles(dir, "*.lnk"))
                    {
                        string t = ShellLink.GetTarget(f);
                        bool byTarget = t != null && Path.GetFileName(t).Equals("CyberWallpaper.exe", StringComparison.OrdinalIgnoreCase);
                        bool byName = Path.GetFileName(f).IndexOf("CyberWallpaper", StringComparison.OrdinalIgnoreCase) >= 0;
                        if (byTarget || byName) res.Add(f);
                    }
            }
            catch { }
            return res.ToArray();
        }

        static void RemoveStartupShortcuts()
        {
            foreach (string f in StartupShortcuts()) { try { File.Delete(f); } catch { } }
        }
    }

    // .lnk-Verknüpfungen über die Shell-Schnittstelle IShellLinkW
    static class ShellLink
    {
        [ComImport, Guid("00021401-0000-0000-C000-000000000046")] class CShellLink { }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
        interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            void Resolve(IntPtr hwnd, uint fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("0000010b-0000-0000-C000-000000000046")]
        interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            [PreserveSig] int IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
        }

        public static void Create(string lnkPath, string target, string args, string workDir, string description)
        {
            var link = (IShellLinkW)new CShellLink();
            try
            {
                link.SetPath(target);
                if (!string.IsNullOrEmpty(args)) link.SetArguments(args);
                if (!string.IsNullOrEmpty(workDir)) link.SetWorkingDirectory(workDir);
                if (!string.IsNullOrEmpty(description)) link.SetDescription(description);
                link.SetIconLocation(target, 0);
                Directory.CreateDirectory(Path.GetDirectoryName(lnkPath));
                ((IPersistFile)link).Save(lnkPath, true);
            }
            finally { Marshal.ReleaseComObject(link); }
        }

        public static string GetTarget(string lnkPath)
        {
            try
            {
                var link = (IShellLinkW)new CShellLink();
                try
                {
                    ((IPersistFile)link).Load(lnkPath, 0);
                    var sb = new StringBuilder(1024);
                    link.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
                    return sb.ToString();
                }
                finally { Marshal.ReleaseComObject(link); }
            }
            catch { return null; }
        }
    }

    // Titelleiste im Programm-Look: Windows 11 färbt Leiste, Text und Rahmen über DWM ein
    static class TitleBar
    {
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        static int ColorRef(Color c) { return c.R | (c.G << 8) | (c.B << 16); }   // COLORREF = 0x00BBGGRR

        public static void Apply(IntPtr hwnd, Color caption, Color text, Color border)
        {
            try
            {
                int on = 1;               DwmSetWindowAttribute(hwnd, 20, ref on, 4);    // DWMWA_USE_IMMERSIVE_DARK_MODE (dunkle Schaltflächen, Win10-Rückfall)
                int cap = ColorRef(caption); DwmSetWindowAttribute(hwnd, 35, ref cap, 4); // DWMWA_CAPTION_COLOR (Win11)
                int txt = ColorRef(text);    DwmSetWindowAttribute(hwnd, 36, ref txt, 4); // DWMWA_TEXT_COLOR
                int brd = ColorRef(border);  DwmSetWindowAttribute(hwnd, 34, ref brd, 4); // DWMWA_BORDER_COLOR
            }
            catch { }
        }
    }
}
