using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

// Cyberpunk-Live-Wallpaper: Hintergrundbild + animierte Effekte (Regen, flackernde
// Stadtlichter, Neon-Schimmer, Lichtzucken, Vignette, Funken), hinter den Desktop-Icons.
// Rendert per Direct3D11 + Direct2D auf eine DXGI-Flip-Swapchain im WorkerW-Kindfenster.
namespace CyberWallpaperFX
{
    // ===================== EINSTELLUNGEN =====================
    static class Config
    {
        public const int Fps = 30;

        // --- Regen ---
        public const bool  Rain          = true;
        public const int   RainCount     = 340;
        public const float RainSpeedMin  = 900f;
        public const float RainSpeedMax  = 1500f;
        public const float RainWind      = -120f;   // px/s horizontal (negativ = nach links)
        public const float RainLenMin    = 12f;
        public const float RainLenMax    = 26f;
        public static readonly float[] RainColor = { 0.60f, 0.80f, 1.00f }; // kühles Hellblau

        // --- Stadtlichter als Lichtquellen (unteres Band) ---
        public const bool  Sparkles      = true;
        public const float CityBandTop    = 0.70f;    // Bruchteil der Höhe (inkl. Wolke)
        public const float CityBandBottom = 0.98f;
        public const int   CityLightMinLum = 52;       // Mindest-Helligkeit eines Lichtpixels
        public const int   CityLightCell   = 16;       // Raster (px): max. ein Licht pro Zelle -> gleichmäßig, keine Hot-Spots
        // Echte blaue Stadtlichter (B>=R) im Band zählen zusätzlich als Stadt – auch außerhalb der Stadt-Maske (z. B. rechts)
        public const float BlueFallbackTop    = 0.72f;
        public const float BlueFallbackBottom = 0.90f;
        public const int   BlueFallbackMinLum = 70;
        // Pink-Lichter nur in der Wolke (unterer Bereich) + an den Rändern; die zentrale/obere Stadt bleibt blau
        public const float CloudLineY  = 0.85f;   // ab dieser Höhe (Bruchteil) ist Pink erlaubt (Wolke)
        public const float EdgeLeftX   = 0.14f;   // linker Rand: Pink erlaubt (x <= dies)
        public const float EdgeRightX  = 0.86f;   // rechter Rand: Pink erlaubt (x >= dies)

        // --- Aufsteigende Funken/Glimmer ---
        public const bool  Embers        = false;
        public const int   EmberCount    = 20;

        // --- Neon-Schimmer (atmender Farbstich) ---
        public const bool  NeonPulse     = true;
        public const float NeonPulseMax  = 0.022f;  // max. Deckkraft (dezent)

        // --- Lichtzucken (gelegentlicher Blitz) ---
        public const bool  Lightning     = false;
        public const float LightningEveryMinSec = 9f;
        public const float LightningEveryMaxSec = 20f;

        // --- Sonne am Horizont (lebendiger Schein + rotierende Strahlen) ---
        public const bool  Sun           = true;
        public const float SunFx         = 0.669f;  // Position als Bruchteil des Bildes (x)
        public const float SunFy         = 0.606f;  // (y)
        public const float SunGlowRadius = 150f;
        public const int   SunRayCount   = 16;
        public const float SunRayLen     = 280f;
        public static readonly float[] SunColor = { 1.0f, 0.55f, 0.2f };

        // --- Vignette ---
        public const bool  Vignette      = true;
        public const float VignetteStrength = 0.55f;

        // --- Video-Hintergrund (statt Bild) ---
        // Ist die Datei vorhanden, läuft das Video (stumm, in Schleife); sonst das Bild-Wallpaper.
        // Bild-spezifische Effekte (Sonne, Stadt-/Wolkenlichter) sind im Videomodus aus.
        public const bool   UseVideo       = true;
        public const string VideoFile      = "";                   // fester Pfad (relativ zum Programmordner); leer = automatisch
        public const string VideoFolder    = "videos";             // automatisch: neuestes Video in diesem Ordner
        public const bool   VideoRain      = false;                // Regen auch über dem Video
        public const bool   VideoNeonPulse = false;                // Neon-Schimmer auch über dem Video
    }

    class Drop { public float X, Y, Spd, Len, A; }
    class Spark { public float X, Y, Base, Phase, Freq, R; public float[] C; }
    class Ember { public float X, Y, Vy, Drift, R, A; public float[] C; }

    [StructLayout(LayoutKind.Sequential)] struct SwapChainDesc1 {
        public uint Width, Height; public int Format, Stereo;
        public uint SampleCount, SampleQuality, BufferUsage, BufferCount;
        public int Scaling, SwapEffect, AlphaMode; public uint Flags;
    }
    [StructLayout(LayoutKind.Sequential)] struct ColorF { public float R, G, B, A; }
    [StructLayout(LayoutKind.Sequential)] struct Ellipse { public float X, Y, Rx, Ry; }
    [StructLayout(LayoutKind.Sequential)] struct D2Point { public float X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct RectF { public float L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] struct SizeU { public uint W, H; }
    [StructLayout(LayoutKind.Sequential)] struct BitmapProps { public int Format, Alpha; public float DpiX, DpiY; }
    [StructLayout(LayoutKind.Sequential)] struct GradStop { public float Pos; public ColorF Color; }
    [StructLayout(LayoutKind.Sequential)] struct RTProps { public int Type, Format, Alpha; public float DpiX, DpiY; public int Usage, MinLevel; }
    [StructLayout(LayoutKind.Sequential)] struct RadialProps { public D2Point Center, Offset; public float Rx, Ry; }

    // --- Video (Media Foundation) ---
    [StructLayout(LayoutKind.Sequential)] struct Tex2DDesc {
        public uint Width, Height, MipLevels, ArraySize; public int Format;
        public uint SampleCount, SampleQuality; public int Usage; public uint BindFlags, CPUAccessFlags, MiscFlags;
    }
    [StructLayout(LayoutKind.Sequential)] struct NormRect { public float L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] struct IRect { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] struct MFARGB { public byte B, G, R, A; }

    // Rückruf-Schnittstelle der Media Engine (muss public + ComVisible sein, damit .NET einen COM-Wrapper erzeugt)
    [ComVisible(true), Guid("fee7c112-e776-42b5-9bbf-0048524e2bd5"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMFMediaEngineNotify { [PreserveSig] int EventNotify(uint evt, UIntPtr param1, uint param2); }

    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    public class EngineNotify : IMFMediaEngineNotify
    {
        public volatile int LastEvent, ErrorCode, ErrorHr;
        public int EventNotify(uint evt, UIntPtr param1, uint param2)
        {
            LastEvent = (int)evt;
            if (evt == 5) { ErrorCode = (int)param1.ToUInt32(); ErrorHr = unchecked((int)param2); }   // MF_MEDIA_ENGINE_EVENT_ERROR
            return 0;
        }
    }

    static class Native
    {
        [DllImport("user32.dll")] public static extern IntPtr FindWindow(string c, string n);
        [DllImport("user32.dll")] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string c, string n);
        [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint cmd);
        [DllImport("user32.dll")] public static extern int GetClassName(IntPtr h, StringBuilder s, int m);
        [DllImport("user32.dll")] public static extern IntPtr SetParent(IntPtr c, IntPtr p);
        [DllImport("user32.dll")] public static extern IntPtr SendMessageTimeout(IntPtr h, uint m, IntPtr w, IntPtr l, uint f, uint t, out IntPtr r);
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
        [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr h, int i, int v);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
        [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern bool AllowSetForegroundWindow(int processId);
        public struct RECT { public int L, T, R, B; }
        static string Cls(IntPtr h) { var s = new StringBuilder(64); GetClassName(h, s, 64); return s.ToString(); }
        public static bool IsClass(IntPtr h, string name) { return Cls(h) == name; }
        // Sucht die Desktop-Ebene hinter den Icons, ohne etwas zu verändern (IntPtr.Zero = Explorer läuft nicht).
        public static IntPtr FindHost() {
            IntPtr pm = FindWindow("Progman", null);
            if (pm == IntPtr.Zero) return IntPtr.Zero;
            // Neues Layout (Windows 11 24H2+): WorkerW ist ein Kind von Progman
            IntPtr c = GetWindow(pm, 5);
            while (c != IntPtr.Zero) { if (Cls(c) == "WorkerW") return c; c = GetWindow(c, 2); }
            // Klassisches Layout (Windows 10 / älteres Windows 11): Die Icons hängen in einem Top-Level-WorkerW,
            // das nächste WorkerW dahinter ist die Wallpaper-Ebene.
            IntPtr top = IntPtr.Zero;
            while ((top = FindWindowEx(IntPtr.Zero, top, "WorkerW", null)) != IntPtr.Zero)
                if (FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero) {
                    IntPtr ww = FindWindowEx(IntPtr.Zero, top, "WorkerW", null);
                    if (ww != IntPtr.Zero) return ww;
                }
            return pm;
        }
        // Wie FindHost, bittet Progman aber vorher, die WorkerW-Ebene anzulegen.
        public static IntPtr GetWorkerWChild() {
            IntPtr pm = FindWindow("Progman", null); IntPtr r;
            if (pm == IntPtr.Zero) return IntPtr.Zero;
            // SMTO_ABORTIFHUNG (0x2): hängt Explorer, sofort zurückkehren statt den Aufrufer 1 s zu blockieren
            SendMessageTimeout(pm, 0x052C, IntPtr.Zero, IntPtr.Zero, 0x0002, 1000, out r);
            return FindHost();
        }
        public static void MakeChild(IntPtr h) {
            uint st = (uint)GetWindowLong(h, -16); st |= 0x40000000; st &= ~0x80000000u;
            SetWindowLong(h, -16, unchecked((int)st));
        }
    }

    static class Api
    {
        [DllImport("d3d11.dll")] public static extern int D3D11CreateDevice(IntPtr a, int dt, IntPtr sw, uint fl, IntPtr lv, uint nlv, uint sdk, out IntPtr dev, out int ofl, out IntPtr ctx);
        [DllImport("dxgi.dll")] public static extern int CreateDXGIFactory2(uint f, ref Guid riid, out IntPtr factory);
        [DllImport("d2d1.dll")] public static extern int D2D1CreateFactory(int type, ref Guid riid, IntPtr opts, out IntPtr factory);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int  CreateSCForHwnd(IntPtr s, IntPtr dev, IntPtr hwnd, ref SwapChainDesc1 d, IntPtr fs, IntPtr rst, out IntPtr sc);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int  GetBuffer(IntPtr s, uint b, ref Guid riid, out IntPtr o);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int  Present(IntPtr s, uint sync, uint fl);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int  ResizeBuffers(IntPtr s, uint count, uint w, uint h, int format, uint flags);   // IDXGISwapChain 13
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void CtxVoid(IntPtr s);                                                         // ID3D11DeviceContext: ClearState 110 / Flush 111
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int  CreateDxgiSurfaceRT(IntPtr s, IntPtr surf, ref RTProps p, out IntPtr rt);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int  CreateSolidBrush(IntPtr s, ref ColorF c, IntPtr props, out IntPtr brush);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int  CreateBitmap(IntPtr s, SizeU size, IntPtr src, uint pitch, ref BitmapProps p, out IntPtr bmp);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int  CreateGradStops(IntPtr s, IntPtr stops, uint count, int gamma, int extend, out IntPtr coll);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int  CreateRadialBrush(IntPtr s, ref RadialProps gp, IntPtr bp, IntPtr stops, out IntPtr brush);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void BeginDraw(IntPtr s);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int  EndDraw(IntPtr s, IntPtr t1, IntPtr t2);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void Clear(IntPtr s, ref ColorF c);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void FillEllipse(IntPtr s, ref Ellipse e, IntPtr brush);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void FillRect(IntPtr s, ref RectF r, IntPtr brush);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void DrawLine(IntPtr s, D2Point a, D2Point b, IntPtr brush, float w, IntPtr stroke);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void DrawBitmap(IntPtr s, IntPtr bmp, ref RectF dest, float opacity, int interp, IntPtr srcRect);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void SetAA(IntPtr s, int mode);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void SetColor(IntPtr s, ref ColorF c);

        // --- Video: Media Foundation / Media Engine ---
        [DllImport("mfplat.dll")] public static extern int MFStartup(uint version, uint flags);
        [DllImport("mfplat.dll")] public static extern int MFCreateDXGIDeviceManager(out uint resetToken, out IntPtr manager);
        [DllImport("mfplat.dll")] public static extern int MFCreateAttributes(out IntPtr attrs, uint initialSize);
        [DllImport("ole32.dll")]  public static extern int CoInitializeEx(IntPtr reserved, uint coinit);
        [DllImport("ole32.dll")]  public static extern int CoCreateInstance(ref Guid clsid, IntPtr outer, uint ctx, ref Guid iid, out IntPtr ppv);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int  CreateTexture2D(IntPtr s, ref Tex2DDesc d, IntPtr init, out IntPtr tex);   // ID3D11Device 5
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void CopyResource(IntPtr s, IntPtr dst, IntPtr src);                         // ID3D11DeviceContext 47
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int  SetMTProtected(IntPtr s, int on);                                        // ID3D10Multithread 5
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int  ResetDevice(IntPtr s, IntPtr dev, uint token);                           // IMFDXGIDeviceManager 7
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int  AttrSetUINT32(IntPtr s, ref Guid key, uint v);                           // IMFAttributes 21
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int  AttrSetUnknown(IntPtr s, ref Guid key, IntPtr unk);                      // IMFAttributes 27
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int  MECreateInstance(IntPtr s, uint flags, IntPtr attrs, out IntPtr engine);  // IMFMediaEngineClassFactory 3
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int  MESetSource(IntPtr s, [MarshalAs(UnmanagedType.BStr)] string url);      // IMFMediaEngine 6
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int  MESetBool(IntPtr s, int v);                                              // SetAutoPlay 29 / SetLoop 31 / SetMuted 35
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int  MEPlay(IntPtr s);                                                        // IMFMediaEngine 32
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int  MENativeSize(IntPtr s, out uint cx, out uint cy);                        // IMFMediaEngine 40
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int  METransfer(IntPtr s, IntPtr dst, ref NormRect src, ref IRect dstRect, ref MFARGB border); // 43
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int  METick(IntPtr s, out long pts);                                          // IMFMediaEngine 44

        public static IntPtr Vt(IntPtr o, int slot) { return Marshal.ReadIntPtr(Marshal.ReadIntPtr(o), slot * IntPtr.Size); }
        public static T D<T>(IntPtr o, int slot) where T : class { return (T)(object)Marshal.GetDelegateForFunctionPointer(Vt(o, slot), typeof(T)); }
    }

    // Protokoll in CyberWallpaper.log neben der EXE. Ein normaler Start beginnt eine neue Datei;
    // nach einem automatischen Neustart wird angehängt, damit der Grund des Neustarts erhalten bleibt.
    static class Log
    {
        public static bool Keep;
        static bool started;
        public static string FilePath { get { return System.IO.Path.Combine(Application.StartupPath, "CyberWallpaper.log"); } }
        public static void W(string s)
        {
            try
            {
                string p = FilePath;
                string line = DateTime.Now.ToString("HH:mm:ss") + " " + s + Environment.NewLine;
                bool append = started || Keep;
                if (append && !started && System.IO.File.Exists(p) && new System.IO.FileInfo(p).Length > 256 * 1024) append = false;
                if (append) System.IO.File.AppendAllText(p, line); else System.IO.File.WriteAllText(p, line);
                started = true;
            }
            catch { }
        }
    }

    class WForm : Form
    {
        IntPtr device, ctx, swap, d2d, rt, brush, bg, vignetteBrush;
        Api.Present present; Api.BeginDraw begin; Api.EndDraw end; Api.Clear clear;
        Api.FillEllipse fillE; Api.FillRect fillR; Api.DrawLine drawL; Api.DrawBitmap drawB; Api.SetColor setCol;
        RectF bgRect;
        float sunX, sunY;
        int w, h, imgW, imgH;
        readonly Random rnd = new Random();
        readonly List<Drop> drops = new List<Drop>();
        readonly List<Spark> sparks = new List<Spark>();
        readonly List<Ember> embers = new List<Ember>();
        double time; float neonT; float lightningT, nextLightning; float flash;
        // Video
        bool videoMode, haveFrame, vSrcSet; int vFrames, vLogTick, vLogCount;
        bool videoSupported, videoReady, imageEffects; uint vNatW, vNatH;
        string currentMedia = "";
        IntPtr engine, videoTex, backTex, dxgiMgr, mfAttrs, notifyPtr;
        EngineNotify notify; NormRect vSrc;
        Api.CopyResource copyRes; Api.METick meTick; Api.METransfer meTransfer; Api.MENativeSize meSize;
        Api.MESetSource meSetSource; Api.MEPlay mePlay, mePause;
        // Stabilität: echte Frame-Zeit, Watchdog, Fehlererkennung
        readonly Stopwatch clock = Stopwatch.StartNew();
        double lastT; float watchT, watchEvery = 3f; int failStreak, attachFails; bool ready, handleLost;
        public string Fatal;          // != null: Weiterzeichnen ist sinnlos -> Program startet die App neu
        public bool ShuttingDown;     // gewolltes Beenden (kein Fehler)
        Guid IID_Surface = new Guid(0xcafcb56c, 0x6ac3, 0x4889, 0xbf, 0x47, 0x9e, 0x23, 0xbb, 0xd2, 0x60, 0xec);

        public WForm(int ww, int hh) {
            w = ww; h = hh;
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual; Width = w; Height = h;
            SetStyle(ControlStyles.Opaque, true);
        }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        void L(string s) { Log.W(s); }

        // Wird das WorkerW zerstört (Explorer-Neustart/-Absturz), reißt Windows unser Kindfenster mit.
        // Die Swapchain hängt dann an einem toten Fenster -> als fatal melden, statt unsichtbar weiterzulaufen.
        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            if (ready && !ShuttingDown) handleLost = true;
        }

        public bool Init()
        {
            try
            {
                Settings.Load();
                string startMedia = ResolveStartMedia();
                L("Start-Medium: " + System.IO.Path.GetFileName(startMedia));
                int ofl;
                // BGRA_SUPPORT (0x20) für Direct2D, VIDEO_SUPPORT (0x800) für die Media Engine.
                // Immer mit Video-Support, damit später live auf ein Video gewechselt werden kann.
                int hr = Api.D3D11CreateDevice(IntPtr.Zero, 1, IntPtr.Zero, 0x820, IntPtr.Zero, 0, 7, out device, out ofl, out ctx);
                videoSupported = hr == 0;
                if (!videoSupported)
                {
                    L("D3D11 mit Video-Support 0x" + hr.ToString("X8") + " -> ohne (nur Bilder)");
                    hr = Api.D3D11CreateDevice(IntPtr.Zero, 1, IntPtr.Zero, 0x20, IntPtr.Zero, 0, 7, out device, out ofl, out ctx);
                }
                L("D3D11 0x" + hr.ToString("X8") + " videoSupported=" + videoSupported); if (hr != 0) return false;
                Guid iidF2 = new Guid(0x50c83a1c, 0xe072, 0x4c48, 0x87, 0xb0, 0x36, 0x30, 0xfa, 0x36, 0xa6, 0xd0);
                IntPtr factory; hr = Api.CreateDXGIFactory2(0, ref iidF2, out factory); L("Factory 0x" + hr.ToString("X8")); if (hr != 0) return false;
                var d = new SwapChainDesc1 { Width = (uint)w, Height = (uint)h, Format = 87, SampleCount = 1, BufferUsage = 0x20, BufferCount = 2, SwapEffect = 4 };
                hr = Api.D<Api.CreateSCForHwnd>(factory, 15)(factory, device, Handle, ref d, IntPtr.Zero, IntPtr.Zero, out swap); L("Swap 0x" + hr.ToString("X8")); if (hr != 0) return false;
                Guid iidD2D = new Guid(0x06152247, 0x6f50, 0x465a, 0x92, 0x45, 0x11, 0x8b, 0xfd, 0x3b, 0x60, 0x07);
                hr = Api.D2D1CreateFactory(0, ref iidD2D, IntPtr.Zero, out d2d); L("D2D 0x" + hr.ToString("X8")); if (hr != 0) return false;
                present = Api.D<Api.Present>(swap, 8);
                if (!CreateTarget()) return false;

                BuildVignette();
                InitEffects();
                nextLightning = Rndf(Config.LightningEveryMinSec, Config.LightningEveryMaxSec);
                // Beim Start nichts in settings.txt schreiben: Scheitert das gewählte Medium nur vorübergehend
                // (z. B. Grafiktreiber direkt nach einem Neustart noch nicht bereit), bleibt die Auswahl erhalten.
                string err = SwitchTo(startMedia, false);
                if (err != null && !Media.SamePath(startMedia, Media.OriginalBackground))
                {
                    L("Start-Medium fehlgeschlagen (" + err + ") -> Standard-Bild (Auswahl bleibt gespeichert)");
                    err = SwitchTo(Media.OriginalBackground, false);
                }
                L("Setup OK " + w + "x" + h + " | " + StatusText);
                lastT = clock.Elapsed.TotalSeconds;
                ready = true;
                return true;
            }
            catch (Exception ex) { L("EX " + ex); return false; }
        }

        static void Rel(ref IntPtr p) { if (p != IntPtr.Zero) { Marshal.Release(p); p = IntPtr.Zero; } }

        // Direct2D-Zeichenziel auf dem Backbuffer der Swapchain anlegen (beim Start und nach einer Größenänderung)
        bool CreateTarget()
        {
            IntPtr surface;
            int hr = Api.D<Api.GetBuffer>(swap, 9)(swap, 0, ref IID_Surface, out surface); L("Surf 0x" + hr.ToString("X8")); if (hr != 0) return false;
            var pp = new RTProps { Format = 87, Alpha = 1, DpiX = 96, DpiY = 96 };
            hr = Api.D<Api.CreateDxgiSurfaceRT>(d2d, 15)(d2d, surface, ref pp, out rt);
            Marshal.Release(surface);
            L("RT 0x" + hr.ToString("X8")); if (hr != 0) { rt = IntPtr.Zero; return false; }

            Api.D<Api.SetAA>(rt, 32)(rt, 0);
            var white = new ColorF { R = 1, G = 1, B = 1, A = 1 };
            hr = Api.D<Api.CreateSolidBrush>(rt, 8)(rt, ref white, IntPtr.Zero, out brush); L("Brush 0x" + hr.ToString("X8")); if (hr != 0) return false;

            begin   = Api.D<Api.BeginDraw>(rt, 48);
            end     = Api.D<Api.EndDraw>(rt, 49);
            clear   = Api.D<Api.Clear>(rt, 47);
            fillE   = Api.D<Api.FillEllipse>(rt, 21);
            fillR   = Api.D<Api.FillRect>(rt, 17);
            drawL   = Api.D<Api.DrawLine>(rt, 15);
            drawB   = Api.D<Api.DrawBitmap>(rt, 26);
            setCol  = Api.D<Api.SetColor>(brush, 8);
            return true;
        }

        // Zwischentextur in Bildschirmgröße (Ziel für neue Videobilder) + Backbuffer, in den sie jeden Frame kopiert wird
        bool CreateVideoTextures()
        {
            var td = new Tex2DDesc { Width = (uint)w, Height = (uint)h, MipLevels = 1, ArraySize = 1, Format = 87,
                                     SampleCount = 1, SampleQuality = 0, Usage = 0, BindFlags = 0x20 | 0x8 };   // RENDER_TARGET | SHADER_RESOURCE
            int hr = Api.D<Api.CreateTexture2D>(device, 5)(device, ref td, IntPtr.Zero, out videoTex); L("VideoTex 0x" + hr.ToString("X8")); if (hr != 0) return false;
            Guid iidTex2D = new Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c");
            hr = Api.D<Api.GetBuffer>(swap, 9)(swap, 0, ref iidTex2D, out backTex); L("BackTex 0x" + hr.ToString("X8"));
            return hr == 0;
        }

        // Desktop-Größe hat sich geändert (Auflösung, Monitor, Docking, RDP): Swapchain und alles Größenabhängige
        // an Ort und Stelle neu aufbauen. Video läuft weiter, die Oberfläche bleibt offen.
        // false = hat nicht geklappt -> der Aufrufer meldet Fatal und die App startet wie bisher neu.
        bool ResizeTo(int nw, int nh)
        {
            try
            {
                L("Desktop-Größe geändert: " + w + "x" + h + " -> " + nw + "x" + nh);
                // Alles freigeben, was am Backbuffer oder am alten Zeichenziel hängt – sonst lehnt ResizeBuffers ab
                Rel(ref vignetteBrush); Rel(ref bg); Rel(ref brush); Rel(ref rt);
                Rel(ref backTex); Rel(ref videoTex);
                haveFrame = false;
                Api.D<Api.CtxVoid>(ctx, 110)(ctx);   // ClearState
                Api.D<Api.CtxVoid>(ctx, 111)(ctx);   // Flush: verzögerte Freigaben jetzt ausführen

                w = nw; h = nh;
                Native.SetWindowPos(Handle, new IntPtr(1), 0, 0, w, h, 0x0010 | 0x0040);
                int hr = Api.D<Api.ResizeBuffers>(swap, 13)(swap, 0, (uint)w, (uint)h, 0, 0);   // 0 = Anzahl/Format beibehalten
                L("ResizeBuffers 0x" + hr.ToString("X8")); if (hr != 0) return false;

                if (!CreateTarget()) return false;
                BuildVignette();
                if (videoReady)
                {
                    if (!CreateVideoTextures()) return false;
                    vSrcSet = false;   // Zuschnitt für das neue Seitenverhältnis neu berechnen
                }
                if (!videoMode && currentMedia.Length > 0)
                {
                    if (!LoadBackground(currentMedia)) return false;
                    if (imageEffects)
                    {
                        sunX = bgRect.L + Config.SunFx * (bgRect.R - bgRect.L);
                        sunY = bgRect.T + Config.SunFy * (bgRect.B - bgRect.T);
                        BuildSparkles();
                    }
                }
                foreach (var dp in drops) { dp.X = Rndf(0, w); dp.Y = Rndf(0, h); }
                foreach (var em in embers) { em.X = Rndf(0, w); em.Y = Rndf(0, h); }
                lastT = clock.Elapsed.TotalSeconds;   // der Neuaufbau soll nicht als Zeitsprung zählen
                return true;
            }
            catch (Exception ex) { L("Resize EX " + ex); return false; }
        }

        // Video wählen: fester Pfad (VideoFile), sonst das neueste Video im Ordner VideoFolder
        static string FindVideo()
        {
            try
            {
                if (Config.VideoFile.Length > 0)
                {
                    string p = System.IO.Path.Combine(Application.StartupPath, Config.VideoFile);
                    if (System.IO.File.Exists(p)) return p;
                }
                string dir = System.IO.Path.Combine(Application.StartupPath, Config.VideoFolder);
                if (!System.IO.Directory.Exists(dir)) return null;
                string best = null; DateTime bestT = DateTime.MinValue;
                foreach (string f in System.IO.Directory.GetFiles(dir))
                {
                    string ext = System.IO.Path.GetExtension(f).ToLowerInvariant();
                    if (ext != ".mp4" && ext != ".m4v" && ext != ".mov" && ext != ".mkv" && ext != ".webm") continue;
                    DateTime t = System.IO.File.GetLastWriteTime(f);
                    if (best == null || t > bestT) { best = f; bestT = t; }
                }
                return best;
            }
            catch { return null; }
        }

        // Start-Medium: zuletzt gewähltes (settings.txt), sonst neuestes Video, sonst Standard-Bild
        static string ResolveStartMedia()
        {
            if (Settings.Current.Length > 0 && System.IO.File.Exists(Settings.Current)) return Settings.Current;
            string v = Config.UseVideo ? FindVideo() : null;
            return v ?? Media.OriginalBackground;
        }

        // Live auf ein anderes Medium wechseln. Rückgabe: null = ok, sonst Fehlermeldung.
        // remember = false: nur anzeigen, die gespeicherte Auswahl (settings.txt) nicht anfassen.
        public string SwitchTo(string path, bool remember = true)
        {
            try
            {
                if (!System.IO.File.Exists(path)) return "Datei nicht gefunden";
                if (Media.IsVideo(path))
                {
                    if (!videoSupported) return "Video wird auf diesem System nicht unterstützt";
                    if (!videoReady && !InitVideoEngine()) return "Video-Engine konnte nicht gestartet werden (siehe Log)";
                    notify.ErrorHr = 0; notify.ErrorCode = 0;
                    haveFrame = false; vSrcSet = false; vNatW = vNatH = 0; vFrames = 0; vLogTick = 0; vLogCount = 0;
                    int hr = meSetSource(engine, path); L("SetSource 0x" + hr.ToString("X8"));
                    if (hr != 0) return "Video konnte nicht geöffnet werden (0x" + hr.ToString("X8") + ")";
                    hr = mePlay(engine); L("Play 0x" + hr.ToString("X8"));
                    videoMode = true;
                }
                else if (Media.IsImage(path))
                {
                    if (videoReady) mePause(engine);   // Video anhalten, spart Leistung
                    if (!LoadBackground(path)) return "Bild konnte nicht geladen werden";
                    imageEffects = Media.SamePath(path, Media.OriginalBackground);   // Sonne/Lichter passen nur zum Standard-Bild
                    if (imageEffects)
                    {
                        sunX = bgRect.L + Config.SunFx * (bgRect.R - bgRect.L);
                        sunY = bgRect.T + Config.SunFy * (bgRect.B - bgRect.T);
                        BuildSparkles();
                    }
                    else sparks.Clear();
                    videoMode = false;
                }
                else return "Dateityp wird nicht unterstützt";

                currentMedia = path;
                if (remember) { Settings.Current = path; Settings.Save(); }
                L("Gewechselt zu: " + System.IO.Path.GetFileName(path));
                return null;
            }
            catch (Exception ex) { L("SwitchTo EX " + ex); return ex.Message; }
        }

        public string CurrentMedia { get { return currentMedia; } }

        // Kurzbeschreibung für die Oberfläche
        public string StatusText
        {
            get
            {
                if (currentMedia.Length == 0) return "(nichts)";
                string name = System.IO.Path.GetFileName(currentMedia);
                if (videoMode)
                {
                    string s = name + " – Video" + (vNatW > 0 ? " " + vNatW + "×" + vNatH : " (lädt …)");
                    if (notify != null && notify.ErrorHr != 0) s += " – FEHLER beim Abspielen (0x" + notify.ErrorHr.ToString("X8") + ")";
                    return s;
                }
                return name + " – Bild" + (imageEffects ? " mit Effekten" : "");
            }
        }

        // Media Engine einmalig aufsetzen: dekodiert Videos per GPU in eine Textur (stumm, Endlosschleife)
        bool InitVideoEngine()
        {
            try
            {
                int hr;
                // Die Media Engine nutzt das D3D-Gerät aus eigenen Threads -> Multithread-Schutz an
                Guid iidMT = new Guid("9B7E4E00-342C-4106-A19F-4F2704F689F0");   // ID3D10Multithread
                IntPtr mt;
                hr = Marshal.QueryInterface(device, ref iidMT, out mt); L("MT 0x" + hr.ToString("X8"));
                if (hr == 0) { Api.D<Api.SetMTProtected>(mt, 5)(mt, 1); Marshal.Release(mt); }

                hr = Api.MFStartup(0x00020070, 0); L("MFStartup 0x" + hr.ToString("X8")); if (hr != 0) return false;
                uint token;
                hr = Api.MFCreateDXGIDeviceManager(out token, out dxgiMgr); L("DXGIMgr 0x" + hr.ToString("X8")); if (hr != 0) return false;
                hr = Api.D<Api.ResetDevice>(dxgiMgr, 7)(dxgiMgr, device, token); L("ResetDevice 0x" + hr.ToString("X8")); if (hr != 0) return false;

                hr = Api.MFCreateAttributes(out mfAttrs, 3); L("Attrs 0x" + hr.ToString("X8")); if (hr != 0) return false;
                notify = new EngineNotify();
                notifyPtr = Marshal.GetComInterfaceForObject(notify, typeof(IMFMediaEngineNotify));
                Guid kCallback = new Guid("c60381b8-83a4-41f8-a3d0-de05076849a9");   // MF_MEDIA_ENGINE_CALLBACK
                Guid kDxgiMgr  = new Guid("065702da-1094-486d-8617-ee7cc4ee4648");   // MF_MEDIA_ENGINE_DXGI_MANAGER
                Guid kOutFmt   = new Guid("5066893c-8cf9-42bc-8b8a-472212e52726");   // MF_MEDIA_ENGINE_VIDEO_OUTPUT_FORMAT
                var setUnk = Api.D<Api.AttrSetUnknown>(mfAttrs, 27);
                var setU32 = Api.D<Api.AttrSetUINT32>(mfAttrs, 21);
                hr = setUnk(mfAttrs, ref kCallback, notifyPtr); L("Attr Callback 0x" + hr.ToString("X8")); if (hr != 0) return false;
                hr = setUnk(mfAttrs, ref kDxgiMgr, dxgiMgr);     L("Attr DXGI 0x" + hr.ToString("X8"));     if (hr != 0) return false;
                hr = setU32(mfAttrs, ref kOutFmt, 87);           L("Attr Format 0x" + hr.ToString("X8"));   if (hr != 0) return false;   // B8G8R8A8

                Api.CoInitializeEx(IntPtr.Zero, 2);   // STA (bereits aktiv -> harmlos)
                Guid clsidFac = new Guid("B44392DA-499B-446b-A4CB-005FEAD0E6D5");   // CLSID_MFMediaEngineClassFactory
                Guid iidFac   = new Guid("4D645ACE-26AA-4688-9BE1-DF3516990B93");   // IID_IMFMediaEngineClassFactory
                IntPtr fac;
                hr = Api.CoCreateInstance(ref clsidFac, IntPtr.Zero, 1, ref iidFac, out fac); L("Factory 0x" + hr.ToString("X8")); if (hr != 0) return false;
                hr = Api.D<Api.MECreateInstance>(fac, 3)(fac, 0, mfAttrs, out engine); L("Engine 0x" + hr.ToString("X8"));
                Marshal.Release(fac);
                if (hr != 0) return false;

                Api.D<Api.MESetBool>(engine, 35)(engine, 1);   // SetMuted
                Api.D<Api.MESetBool>(engine, 31)(engine, 1);   // SetLoop
                Api.D<Api.MESetBool>(engine, 29)(engine, 1);   // SetAutoPlay

                if (!CreateVideoTextures()) return false;

                copyRes    = Api.D<Api.CopyResource>(ctx, 47);
                meTick     = Api.D<Api.METick>(engine, 44);
                meTransfer = Api.D<Api.METransfer>(engine, 43);
                meSize     = Api.D<Api.MENativeSize>(engine, 40);
                meSetSource = Api.D<Api.MESetSource>(engine, 6);
                mePlay      = Api.D<Api.MEPlay>(engine, 32);
                mePause     = Api.D<Api.MEPlay>(engine, 33);   // Pause hat dieselbe Signatur wie Play
                vSrc = new NormRect { L = 0, T = 0, R = 1, B = 1 };
                videoReady = true;
                return true;
            }
            catch (Exception ex) { L("Video EX " + ex); return false; }
        }

        // "Cover"-Zuschnitt: Video füllt den Bildschirm, überstehender Rand wird abgeschnitten
        void ComputeVideoCrop()
        {
            uint vw, vh;
            if (meSize(engine, out vw, out vh) != 0 || vw == 0 || vh == 0) return;
            float sa = (float)w / h, va = (float)vw / vh;
            if (va > sa) { float f = sa / va; vSrc = new NormRect { L = (1 - f) / 2, T = 0, R = (1 + f) / 2, B = 1 }; }
            else         { float f = va / sa; vSrc = new NormRect { L = 0, T = (1 - f) / 2, R = 1, B = (1 + f) / 2 }; }
            vSrcSet = true; vNatW = vw; vNatH = vh;
            L("Video nativ " + vw + "x" + vh);
        }

        // Bild als Hintergrund laden (ersetzt ein vorher geladenes Bild)
        bool LoadBackground(string path)
        {
            if (!System.IO.File.Exists(path)) { L("Bild fehlt: " + path); return false; }
            using (var img = new Bitmap(path))
            {
                int iw = img.Width, ih = img.Height;
                var r = new Rectangle(0, 0, iw, ih);
                var bd = img.LockBits(r, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
                var size = new SizeU { W = (uint)iw, H = (uint)ih };
                var bp = new BitmapProps { Format = 87, Alpha = 1, DpiX = 96, DpiY = 96 };
                IntPtr newBg;
                int hr = Api.D<Api.CreateBitmap>(rt, 4)(rt, size, bd.Scan0, (uint)bd.Stride, ref bp, out newBg);
                img.UnlockBits(bd);
                L("CreateBitmap 0x" + hr.ToString("X8") + " " + iw + "x" + ih);
                if (hr != 0) return false;
                if (bg != IntPtr.Zero) Marshal.Release(bg);
                bg = newBg; imgW = iw; imgH = ih;
            }
            // Cover-Skalierung
            float scale = Math.Max((float)w / imgW, (float)h / imgH);
            float dw = imgW * scale, dh = imgH * scale;
            float ox = (w - dw) * 0.5f, oy = (h - dh) * 0.5f;
            bgRect = new RectF { L = ox, T = oy, R = ox + dw, B = oy + dh };
            return true;
        }

        void BuildVignette()
        {
            if (!Config.Vignette) return;
            var stops = new GradStop[] {
                new GradStop { Pos = 0.0f, Color = new ColorF { R=0,G=0,B=0,A=0 } },
                new GradStop { Pos = 0.55f, Color = new ColorF { R=0,G=0,B=0,A=0 } },
                new GradStop { Pos = 1.0f, Color = new ColorF { R=0,G=0,B=0,A=Config.VignetteStrength } },
            };
            int stride = Marshal.SizeOf(typeof(GradStop));
            IntPtr sp = Marshal.AllocHGlobal(stride * stops.Length);
            for (int i = 0; i < stops.Length; i++) Marshal.StructureToPtr(stops[i], (IntPtr)(sp.ToInt64() + i * stride), false);
            IntPtr coll;
            int hr = Api.D<Api.CreateGradStops>(rt, 9)(rt, sp, (uint)stops.Length, 0, 0, out coll);
            Marshal.FreeHGlobal(sp);
            if (hr != 0) { L("GradStops 0x" + hr.ToString("X8")); return; }
            var gp = new RadialProps { Center = new D2Point { X = w * 0.5f, Y = h * 0.52f }, Offset = new D2Point { X = 0, Y = 0 }, Rx = w * 0.72f, Ry = h * 0.80f };
            hr = Api.D<Api.CreateRadialBrush>(rt, 11)(rt, ref gp, IntPtr.Zero, coll, out vignetteBrush);
            L("Vignette 0x" + hr.ToString("X8"));
        }

        float Rndf(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }

        void InitEffects()
        {
            if (Config.Rain)
                for (int i = 0; i < Config.RainCount; i++)
                    drops.Add(new Drop { X = Rndf(0, w), Y = Rndf(0, h), Spd = Rndf(Config.RainSpeedMin, Config.RainSpeedMax), Len = Rndf(Config.RainLenMin, Config.RainLenMax), A = Rndf(0.07f, 0.22f) });

            if (Config.Embers)
                for (int i = 0; i < Config.EmberCount; i++)
                    embers.Add(new Ember { X = Rndf(0, w), Y = Rndf(0, h), Vy = Rndf(-24f, -10f), Drift = Rndf(-10f, 10f), R = Rndf(1.2f, 2.6f), A = Rndf(0.2f, 0.6f), C = rnd.NextDouble() < 0.5 ? new float[] { 1f, 0.55f, 0.2f } : new float[] { 1f, 0.3f, 0.7f } });
        }

        // Stadt-/Wolken-Lichter aus den Masken bauen (passen nur zum Standard-Bild background.png)
        void BuildSparkles()
        {
            sparks.Clear();
            if (Config.Sparkles && imgW > 0 && imgH > 0)
            {
                // Gemalte Bereiche: Stadt (city_mask) -> blau, Wolke (cloud_mask) -> pink.
                // Pro Rasterzelle EIN zufällig gewählter Lichtpixel (gleichmäßig, organisch).
                bool[] cityM = LoadMask("city_mask.png", imgW, imgH);
                bool[] cloudM = LoadMask("cloud_mask.png", imgW, imgH);
                float[][] cityPal = new float[][] {
                    new float[]{0.10f,0.75f,1.00f}, new float[]{0.40f,0.88f,1.00f},
                    new float[]{0.62f,0.95f,1.00f}, new float[]{0.00f,1.00f,0.92f} };
                float[][] cloudPal = new float[][] {
                    new float[]{1.00f,0.28f,0.85f}, new float[]{1.00f,0.50f,0.82f},
                    new float[]{0.82f,0.42f,1.00f}, new float[]{1.00f,0.38f,0.70f} };

                var chosen = new Dictionary<long, float[]>();   // {px, py, region(0=Stadt,1=Wolke)}
                var seen = new Dictionary<long, int>();
                int cellPx = Math.Max(4, Config.CityLightCell);
                try
                {
                    string bp = System.IO.Path.Combine(Application.StartupPath, "background.png");
                    if (System.IO.File.Exists(bp))
                        using (var img = new Bitmap(bp))
                        {
                            int iw = img.Width, ih = img.Height;
                            var bd = img.LockBits(new Rectangle(0, 0, iw, ih), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                            int stride = bd.Stride;
                            byte[] rowbuf = new byte[stride];
                            for (int y = 0; y < ih; y++)
                            {
                                Marshal.Copy((IntPtr)(bd.Scan0.ToInt64() + (long)y * stride), rowbuf, 0, stride);
                                int bi = y * iw;
                                for (int x = 0; x < iw; x++)
                                {
                                    int o = x * 4; byte B = rowbuf[o], G = rowbuf[o + 1], R = rowbuf[o + 2];
                                    float lum = 0.299f * R + 0.587f * G + 0.114f * B;
                                    if (lum < Config.CityLightMinLum) continue;
                                    float fy = y / (float)ih;
                                    bool inCity = cityM[bi + x], inCloud = cloudM[bi + x];
                                    int region;
                                    if (inCloud) region = 1;   // Wolke (Vordergrund) hat Vorrang -> IMMER pink
                                    else
                                    {
                                        // echte blaue Stadtlichter im Band zählen auch ohne Maske als Stadt (z. B. rechts)
                                        bool blueByPixel = (B >= R) && lum >= Config.BlueFallbackMinLum
                                                           && fy >= Config.BlueFallbackTop && fy <= Config.BlueFallbackBottom;
                                        if (inCity || blueByPixel) region = 0;   // Stadt -> blau
                                        else continue;                           // außerhalb beider Bereiche -> kein Licht
                                    }
                                    float px = bgRect.L + (x / (float)iw) * (bgRect.R - bgRect.L);
                                    float py = bgRect.T + (y / (float)ih) * (bgRect.B - bgRect.T);
                                    long key = (long)((int)(px / cellPx)) * 100000L + (int)(py / cellPx);
                                    int s; seen.TryGetValue(key, out s); s++; seen[key] = s;
                                    if (s == 1 || rnd.NextDouble() * s < 1.0)
                                        chosen[key] = new float[] { px, py, region };
                                }
                            }
                            img.UnlockBits(bd);
                        }
                }
                catch { }

                foreach (var kv in chosen)
                {
                    var k = kv.Value;
                    float[] pal = (k[2] < 0.5f) ? cityPal[rnd.Next(cityPal.Length)] : cloudPal[rnd.Next(cloudPal.Length)];
                    sparks.Add(new Spark {
                        X = k[0], Y = k[1],
                        Base = Rndf(0.5f, 1f), Phase = Rndf(0, 6.28f), Freq = Rndf(1.2f, 5.5f),
                        R = Rndf(0.8f, 1.7f), C = new float[] { pal[0], pal[1], pal[2] } });
                }
            }

        }

        // Lädt eine Masken-PNG (gleiche Größe wie das Bild) -> bool je Pixel (weiß = im Bereich)
        bool[] LoadMask(string file, int iw, int ih)
        {
            var arr = new bool[iw * ih];
            try
            {
                string p = System.IO.Path.Combine(Application.StartupPath, file);
                if (!System.IO.File.Exists(p)) return arr;
                using (var m = new Bitmap(p))
                {
                    if (m.Width != iw || m.Height != ih) return arr;
                    var bd = m.LockBits(new Rectangle(0, 0, iw, ih), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    int stride = bd.Stride; byte[] row = new byte[stride];
                    for (int y = 0; y < ih; y++)
                    {
                        Marshal.Copy((IntPtr)(bd.Scan0.ToInt64() + (long)y * stride), row, 0, stride);
                        int bi = y * iw;
                        for (int x = 0; x < iw; x++) if (row[x * 4 + 2] > 128) arr[bi + x] = true;
                    }
                    m.UnlockBits(bd);
                }
            }
            catch { }
            return arr;
        }

        void Col(float r, float g, float b, float a) { var c = new ColorF { R = r, G = g, B = b, A = a }; setCol(brush, ref c); }

        // Wieder in die Desktop-Ebene einhängen (WorkerW-Kind von Progman)
        // Rückgabe: true = hängt danach wirklich in dieser Ebene.
        public bool Attach(IntPtr hst)
        {
            if (hst == IntPtr.Zero) return false;   // Explorer läuft gerade nicht
            Native.SetParent(Handle, hst);
            Native.MakeChild(Handle);
            Native.SetWindowPos(Handle, new IntPtr(1), 0, 0, w, h, 0x0010 | 0x0040);
            return Native.GetParent(Handle) == hst;
        }

        public void Frame()
        {
            if (rt == IntPtr.Zero || Fatal != null || ShuttingDown) return;
            if (handleLost || !IsHandleCreated) { Fatal = "Fenster wurde zerstört (Explorer-Neustart?)"; return; }

            // Echte Zeit seit dem letzten Frame: Der Timer tickt ungenau (meist später als 1/Fps),
            // mit festem dt liefen die Animationen sonst zu langsam und ungleichmäßig.
            double now = clock.Elapsed.TotalSeconds;
            float dt = (float)(now - lastT); lastT = now;
            if (dt <= 0f) dt = 1f / Config.Fps; else if (dt > 0.1f) dt = 0.1f;   // nach Hängern nicht springen
            time += dt; neonT += dt;

            // Watchdog: alle ~3 s prüfen, ob wir noch in der richtigen Ebene hängen und die Größe noch stimmt
            watchT += dt;
            if (watchT >= watchEvery)
            {
                watchT = 0f;
                IntPtr want = Native.FindHost();
                if (want != IntPtr.Zero)
                {
                    bool attached = Native.GetParent(Handle) == want;
                    if (!attached)
                    {
                        attached = Attach(want);
                        if (attached) L("Wieder in die Desktop-Ebene eingehängt");
                        else
                        {
                            // Klappt das Einhängen nicht, seltener versuchen (3 s -> 6, 12, 24, 30) und nach
                            // 5 Fehlversuchen neu starten, statt alle 3 s weiter am Explorer zu rütteln.
                            attachFails++;
                            watchEvery = Math.Min(30f, 3f * (1 << Math.Min(attachFails, 4)));
                            L("Einhängen in die Desktop-Ebene fehlgeschlagen (" + attachFails + "x)");
                            if (attachFails >= 5) { Fatal = "Fenster lässt sich nicht mehr in die Desktop-Ebene einhängen"; return; }
                        }
                    }
                    if (attached)
                    {
                        attachFails = 0; watchEvery = 3f;
                        Native.RECT rc;
                        if (Native.GetClientRect(want, out rc) && rc.R > 0 && rc.B > 0 && (rc.R != w || rc.B != h))
                        {
                            int ow = w, oh = h;
                            if (!ResizeTo(rc.R, rc.B))
                            { Fatal = "Desktop-Größe geändert (" + ow + "x" + oh + " -> " + rc.R + "x" + rc.B + "), Anpassen fehlgeschlagen"; return; }
                        }
                    }
                }
            }

            // Video: neues Bild in die Zwischentextur holen, dann jeden Frame in den Backbuffer kopieren
            // (Flip-Swapchain verwirft den Inhalt nach Present, daher auch ohne neues Bild kopieren)
            if (videoMode && videoReady)
            {
                long pts;
                if (meTick(engine, out pts) == 0)   // S_OK = neues Videobild bereit
                {
                    if (!vSrcSet) ComputeVideoCrop();
                    var dst = new IRect { L = 0, T = 0, R = w, B = h };
                    var border = new MFARGB { A = 255 };
                    if (meTransfer(engine, videoTex, ref vSrc, ref dst, ref border) == 0) { haveFrame = true; vFrames++; }
                }
                if (haveFrame) copyRes(ctx, backTex, videoTex);
                if (vLogCount < 10 && ++vLogTick >= Config.Fps * 3)   // die ersten 30 s alle 3 s protokollieren
                {
                    vLogTick = 0; vLogCount++;
                    L("Video: Bilder=" + vFrames + " letztesEvent=" + notify.LastEvent
                      + (notify.ErrorHr != 0 ? " FEHLER code=" + notify.ErrorCode + " hr=0x" + notify.ErrorHr.ToString("X8") : ""));
                }
            }

            begin(rt);
            if (!videoMode || !haveFrame)
            {
                var black = new ColorF { A = 1 };
                clear(rt, ref black);
            }

            // Hintergrundbild (Cover)
            if (!videoMode && bg != IntPtr.Zero) drawB(rt, bg, ref bgRect, 1f, 1, IntPtr.Zero);

            // Neon-Schimmer (atmender Farbstich)
            if (Config.NeonPulse && (!videoMode || Config.VideoNeonPulse))
            {
                float p = (float)(0.5 + 0.5 * Math.Sin(time * 0.6));
                float a = Config.NeonPulseMax * (0.4f + 0.6f * p);
                // zwischen Cyan und Magenta pendeln
                float r = 0.1f + 0.9f * p, g = 0.3f * (1 - p) + 0.6f * p, b = 1f;
                var full = new RectF { L = 0, T = 0, R = w, B = h };
                Col(r, 0.2f + 0.3f * (1 - p), b, a); fillR(rt, ref full, brush);
            }

            // Sonne am Horizont: pulsierender Schein + langsam rotierende Lichtstrahlen
            if (Config.Sun && !videoMode && imageEffects)   // Sonne ist auf das Standard-Bild positioniert
            {
                float pulse = 0.82f + 0.18f * (float)Math.Sin(time * 1.1);
                float[] sc = Config.SunColor;
                float R = Config.SunGlowRadius;
                Col(sc[0], sc[1], sc[2], 0.05f * pulse);
                var h1 = new Ellipse { X = sunX, Y = sunY, Rx = R * 1.8f * pulse, Ry = R * 1.35f * pulse }; fillE(rt, ref h1, brush);
                Col(sc[0], sc[1], sc[2], 0.09f * pulse);
                var h2 = new Ellipse { X = sunX, Y = sunY, Rx = R * pulse, Ry = R * 0.8f * pulse }; fillE(rt, ref h2, brush);
                Col(Math.Min(1f, sc[0] + 0.1f), Math.Min(1f, sc[1] + 0.2f), sc[2] + 0.05f, 0.16f * pulse);
                var h3 = new Ellipse { X = sunX, Y = sunY, Rx = R * 0.5f, Ry = R * 0.42f }; fillE(rt, ref h3, brush);
                double rot = time * 0.05;
                for (int i = 0; i < Config.SunRayCount; i++)
                {
                    double a = rot + i * (Math.PI * 2 / Config.SunRayCount);
                    float len = Config.SunRayLen * (0.55f + 0.45f * (float)Math.Sin(time * 0.6 + i * 1.3));
                    float ra = 0.05f + 0.05f * (float)(0.5 + 0.5 * Math.Sin(time * 1.7 + i));
                    Col(sc[0], sc[1], sc[2], ra);
                    drawL(rt, new D2Point { X = sunX, Y = sunY },
                              new D2Point { X = sunX + (float)Math.Cos(a) * len, Y = sunY + (float)Math.Sin(a) * len * 0.72f },
                              brush, 2.4f, IntPtr.Zero);
                }
            }

            // Stadtlichter als leuchtende Lichtquellen (weicher mehrschichtiger Bloom)
            if (Config.Sparkles && !videoMode && imageEffects)
                foreach (var s in sparks)
                {
                    float wave = 0.5f + 0.5f * (float)Math.Sin(time * s.Freq + s.Phase);
                    float fl = s.Base * (0.08f + 0.92f * wave);   // tiefes Pulsieren: fast aus -> voll hell
                    Col(s.C[0], s.C[1], s.C[2], fl * 0.14f);
                    var g0 = new Ellipse { X = s.X, Y = s.Y, Rx = s.R * 6.2f, Ry = s.R * 6.2f }; fillE(rt, ref g0, brush);
                    Col(s.C[0], s.C[1], s.C[2], fl * 0.38f);
                    var g1 = new Ellipse { X = s.X, Y = s.Y, Rx = s.R * 2.8f, Ry = s.R * 2.8f }; fillE(rt, ref g1, brush);
                    Col(Math.Min(1f, s.C[0] + 0.35f), Math.Min(1f, s.C[1] + 0.35f), Math.Min(1f, s.C[2] + 0.35f), Math.Min(1f, fl * 1.15f));
                    var g2 = new Ellipse { X = s.X, Y = s.Y, Rx = s.R * 0.95f, Ry = s.R * 0.95f }; fillE(rt, ref g2, brush);
                }

            // Funken (aufsteigend, hinter dem Regen)
            if (Config.Embers)
                foreach (var e in embers)
                {
                    e.Y += e.Vy * dt; e.X += (float)Math.Sin(time * 0.5 + e.X) * e.Drift * dt;
                    if (e.Y < -10) { e.Y = h + 10; e.X = Rndf(0, w); }
                    Col(e.C[0], e.C[1], e.C[2], e.A * 0.25f);
                    var q1 = new Ellipse { X = e.X, Y = e.Y, Rx = e.R * 3f, Ry = e.R * 3f }; fillE(rt, ref q1, brush);
                    Col(e.C[0], e.C[1], e.C[2], e.A);
                    var q2 = new Ellipse { X = e.X, Y = e.Y, Rx = e.R, Ry = e.R }; fillE(rt, ref q2, brush);
                }

            // Regen
            if (Config.Rain && (!videoMode || Config.VideoRain))
            {
                float wind = Config.RainWind;
                foreach (var dp in drops)
                {
                    dp.Y += dp.Spd * dt; dp.X += wind * dt;
                    if (dp.Y > h + 30) { dp.Y = -30; dp.X = Rndf(0, w); }
                    else if (dp.X < -30) dp.X = w + 30;
                    float mag = (float)Math.Sqrt(dp.Spd * dp.Spd + wind * wind);
                    float ux = wind / mag, uy = dp.Spd / mag;
                    Col(Config.RainColor[0], Config.RainColor[1], Config.RainColor[2], dp.A);
                    drawL(rt, new D2Point { X = dp.X, Y = dp.Y }, new D2Point { X = dp.X - ux * dp.Len, Y = dp.Y - uy * dp.Len }, brush, 1.1f, IntPtr.Zero);
                }
            }

            // Lichtzucken (Blitz)
            if (Config.Lightning)
            {
                lightningT += dt;
                if (lightningT >= nextLightning) { lightningT = 0; nextLightning = Rndf(Config.LightningEveryMinSec, Config.LightningEveryMaxSec); flash = 1f; }
                if (flash > 0.01f)
                {
                    var full = new RectF { L = 0, T = 0, R = w, B = h };
                    Col(0.8f, 0.85f, 1f, flash * 0.22f); fillR(rt, ref full, brush);
                    flash *= 0.72f;
                }
            }

            // Vignette
            if (Config.Vignette && vignetteBrush != IntPtr.Zero)
            {
                var full = new RectF { L = 0, T = 0, R = w, B = h };
                fillR(rt, ref full, vignetteBrush);
            }

            int hrEnd = end(rt, IntPtr.Zero, IntPtr.Zero);
            int hrPresent = present(swap, 1, 0);
            if (hrEnd < 0 || hrPresent < 0)
            {
                // Grafikgerät verloren (Treiber-Update, GPU-Wechsel ...) -> sofort; andere Fehler erst, wenn sie anhalten
                if (IsDeviceLost(hrEnd) || IsDeviceLost(hrPresent) || ++failStreak >= 60)
                    Fatal = "Zeichnen fehlgeschlagen: EndDraw 0x" + hrEnd.ToString("X8") + " Present 0x" + hrPresent.ToString("X8");
            }
            else failStreak = 0;
        }

        static bool IsDeviceLost(int hr)
        {
            return hr == unchecked((int)0x8899000C)    // D2DERR_RECREATE_TARGET
                || hr == unchecked((int)0x887A0005)    // DXGI_ERROR_DEVICE_REMOVED
                || hr == unchecked((int)0x887A0006)    // DXGI_ERROR_DEVICE_HUNG
                || hr == unchecked((int)0x887A0007)    // DXGI_ERROR_DEVICE_RESET
                || hr == unchecked((int)0x887A0020);   // DXGI_ERROR_DRIVER_INTERNAL_ERROR
        }
    }

    static class Program
    {
        const int MaxRestarts = 5;           // so viele schnelle Neustarts hintereinander, dann aufgeben
        const int MaxRestartsPerHour = 20;   // Obergrenze auch für Fehler, die erst nach längerer Laufzeit wiederkommen
        const string MutexName       = "Local\\CyberWallpaperFX";
        const string ShowGuiEventName = "Local\\CyberWallpaperFX.ZeigeGui";

        static WForm wall;
        static MediaForm gui;
        static System.Threading.Mutex mutex;
        static System.Threading.EventWaitHandle showEv;
        static Stopwatch stable;             // läuft, seit das Wallpaper wirklich zeichnet (ohne Warte- und Startzeit)
        static int restarts;                 // schnelle Neustarts in Folge (aus /restart:n)
        static int hourCount; static long hourStart;   // Neustarts im laufenden Stundenfenster (UTC-Ticks)

        // Auswahl-Oberfläche öffnen (bzw. nach vorne holen)
        static void ShowGui()
        {
            if (wall == null || wall.ShuttingDown) return;
            if (gui == null || gui.IsDisposed) gui = new MediaForm(wall);
            gui.ShowAndActivate();
        }

        [STAThread]
        static void Main(string[] args)
        {
            // /restart:<n>:<NeustartsDieseStunde>:<BeginnDesStundenfenstersUtcTicks>   /gui = Oberfläche wieder öffnen
            bool reopenGui = false;
            foreach (string a in args)
            {
                if (a.StartsWith("/restart:"))
                {
                    string[] p = a.Substring(9).Split(':');
                    int.TryParse(p[0], out restarts);
                    if (p.Length > 1) int.TryParse(p[1], out hourCount);
                    if (p.Length > 2) long.TryParse(p[2], out hourStart);
                }
                else if (a == "/gui") reopenGui = true;
            }
            Log.Keep = restarts > 0;

            // Nur eine Instanz: Ein zweiter Start würde sonst ein zweites Wallpaper darüberlegen.
            // - normaler zweiter Start: öffnet stattdessen die Oberfläche im laufenden Wallpaper
            // - automatischer Neustart (/restart:n): wartet kurz, bis die alte Instanz beendet ist
            bool owned;
            mutex = new System.Threading.Mutex(false, MutexName);
            try { owned = mutex.WaitOne(restarts > 0 ? 10000 : 0); }
            catch (System.Threading.AbandonedMutexException) { owned = true; }
            if (!owned)
            {
                if (restarts == 0)
                {
                    Native.AllowSetForegroundWindow(-1);   // laufender Instanz erlauben, ihr Fenster nach vorne zu holen
                    // Kurz wiederholen: Wechselt die laufende Instanz gerade (Neustart), gibt es das Event einen Moment nicht
                    for (int i = 0; i < 20; i++)
                    {
                        try { using (var ev = System.Threading.EventWaitHandle.OpenExisting(ShowGuiEventName)) ev.Set(); break; }
                        catch { System.Threading.Thread.Sleep(150); }
                    }
                }
                return;
            }
            try
            {
                // Das Event sofort anlegen, nicht erst nach dem Start: Ein zweiter EXE-Start während des Wartens auf
                // den Desktop wird so vorgemerkt und öffnet die Oberfläche, sobald das Wallpaper läuft.
                showEv = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, ShowGuiEventName);
                if (reopenGui) showEv.Set();
                Run();
            }
            finally { try { mutex.ReleaseMutex(); } catch { } }
        }

        static void Run()
        {
            Native.SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (restarts > 0) { Log.W("--- Neustart " + restarts + " ---"); System.Threading.Thread.Sleep(1500); }

            // Auf den Desktop warten (direkt nach der Anmeldung oder einem Explorer-Neustart ist er noch nicht da)
            for (int i = 0; i < 240 && Native.FindWindow("Progman", null) == IntPtr.Zero; i++) System.Threading.Thread.Sleep(250);
            IntPtr host = Native.GetWorkerWChild();
            if (host == IntPtr.Zero) { GiveUp("Der Windows-Desktop (Explorer) wurde nicht gefunden."); return; }
            // Kurz warten, bis die Desktop-Ebene eine Größe hat. Bleibt sie unbekannt, mit 1920x1080 starten –
            // der Watchdog passt die Größe dann an Ort und Stelle an.
            Native.RECT rc; int w = 1920, h = 1080; bool sized = false;
            for (int i = 0; i < 20 && !sized; i++)
            {
                if (Native.GetClientRect(host, out rc) && rc.R > 0 && rc.B > 0) { w = rc.R; h = rc.B; sized = true; }
                else System.Threading.Thread.Sleep(250);
            }
            if (!sized) Log.W("Desktop-Größe noch unbekannt -> starte mit " + w + "x" + h);

            var f = new WForm(w, h);
            wall = f;
            f.Show();
            if (!f.Attach(host)) Log.W("Einhängen in die Desktop-Ebene beim Start fehlgeschlagen (Watchdog versucht es erneut)");

            if (!f.Init())
            {
                // Kein totes Fenster über dem Desktop stehen lassen
                f.ShuttingDown = true; f.Dispose();
                // Auch beim allerersten Start erneut versuchen: Direkt nach der Anmeldung oder während eines
                // Treiber-Updates ist das Grafikgerät oft nur kurz nicht bereit.
                if (restarts < MaxRestarts) System.Threading.Thread.Sleep(2000 * (restarts + 1));
                RestartOrGiveUp(restarts + 1, "Das Wallpaper konnte nicht gestartet werden.", false);
                return;
            }
            stable = Stopwatch.StartNew();

            bool fatalHandled = false;
            var timer = new Timer(); timer.Interval = 1000 / Config.Fps;
            var tray = new NotifyIcon();
            tray.Icon = SystemIcons.Application; tray.Text = "Cyber Wallpaper"; tray.Visible = true;
            var menu = new ContextMenuStrip();
            var miGui = menu.Items.Add("Hintergrund wählen …", null, (s, e) => ShowGui());
            miGui.Font = new Font(miGui.Font, FontStyle.Bold);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Beenden", null, (s, e) => { timer.Stop(); f.ShuttingDown = true; tray.Visible = false; Application.Exit(); });
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += (s, e) => ShowGui();

            // Normaler zweiter Start setzt dieses Event -> Oberfläche im UI-Thread öffnen
            var waiter = new System.Threading.Thread(() =>
            {
                while (true)
                {
                    showEv.WaitOne();
                    try { if (!f.ShuttingDown && f.IsHandleCreated) f.BeginInvoke((MethodInvoker)ShowGui); } catch { }
                }
            });
            waiter.IsBackground = true;
            waiter.Start();

            timer.Tick += (s, e) =>
            {
                f.Frame();
                if (f.Fatal != null && !fatalHandled)
                {
                    fatalHandled = true;
                    timer.Stop();
                    OnFatal(f, tray);
                }
            };
            timer.Start();
            Application.Run();
            tray.Dispose();
        }

        // Weiterzeichnen ist sinnlos -> neu starten. Das passiert direkt hier im Timer-Tick und endet mit
        // Environment.Exit: Ein offener Dialog (z. B. „Hinzufügen …") hat eine eigene Nachrichtenschleife,
        // hinter der Application.Exit hängen bliebe – das Wallpaper wäre so lange tot.
        static void OnFatal(WForm f, NotifyIcon tray)
        {
            string reason = f.Fatal;
            f.ShuttingDown = true;
            bool guiOpen = false;
            try { guiOpen = gui != null && !gui.IsDisposed && gui.Visible; } catch { }
            try { tray.Visible = false; tray.Dispose(); } catch { }
            try { f.Hide(); } catch { }
            Log.W("Neustart nötig: " + reason);
            // Lief die App schon länger stabil, zählt das als neuer Vorfall; sonst hochzählen (Schutz vor Endlosschleife)
            int n = stable != null && stable.Elapsed.TotalSeconds > 60 ? 1 : restarts + 1;
            RestartOrGiveUp(n, "Das Wallpaper musste zu oft neu starten.\nLetzter Grund: " + reason, guiOpen);
            try { mutex.ReleaseMutex(); } catch { }
            Environment.Exit(0);
        }

        // Startet die App neu – außer es waren zu viele Neustarts in Folge (n) oder innerhalb einer Stunde.
        static void RestartOrGiveUp(int n, string giveUpMsg, bool reopenGui)
        {
            long now = DateTime.UtcNow.Ticks;
            if (hourStart <= 0 || now < hourStart || now - hourStart > TimeSpan.TicksPerHour) { hourStart = now; hourCount = 0; }
            hourCount++;
            if (n > MaxRestarts || hourCount > MaxRestartsPerHour) { GiveUp(giveUpMsg); return; }
            try { Process.Start(Application.ExecutablePath, "/restart:" + n + ":" + hourCount + ":" + hourStart + (reopenGui ? " /gui" : "")); }
            catch (Exception ex) { Log.W("Neustart fehlgeschlagen: " + ex.Message); }
        }

        static void GiveUp(string msg)
        {
            Log.W("Abbruch: " + msg);
            MessageBox.Show(msg + "\n\nDetails stehen in:\n" + Log.FilePath, "Cyber Wallpaper", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
