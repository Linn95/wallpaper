# Erzeugt icon.ico (Synthwave-Sonne über Skyline) in allen üblichen Größen.
# Jede Größe wird direkt gezeichnet (nicht herunterskaliert); kleine Größen sind vereinfacht.
#   .\tools\make-icon.ps1                       -> icon.ico im Projektordner
#   .\tools\make-icon.ps1 -Preview vorschau.png -> zusätzlich eine Vorschau auf dunklem/hellem Grund
param([string]$Preview = "")
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

$code = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class IconMaker
{
    static GraphicsPath Round(RectangleF r, float rad)
    {
        var p = new GraphicsPath(); float d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure(); return p;
    }

    public static Bitmap Draw(int s)
    {
        var bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);
            float k = s / 256f;
            bool small = s < 48;
            float m = small ? 0.5f : 8 * k;
            var outer = new RectangleF(m, m, s - 2 * m, s - 2 * m);

            using (GraphicsPath bgPath = Round(outer, (small ? 0.20f : 0.22f) * s))
            {
                // Hintergrund: dunkles Violett
                using (var bg = new LinearGradientBrush(new PointF(0, outer.Top), new PointF(0, outer.Bottom),
                                                        Color.FromArgb(40, 24, 72), Color.FromArgb(12, 9, 24)))
                    g.FillPath(bg, bgPath);
                g.SetClip(bgPath);

                float cx = s * 0.5f;
                float hz = s * (small ? 0.78f : 0.72f);            // Horizont
                float r  = s * (small ? 0.31f : 0.27f);            // Sonnenradius
                float cy = hz - r * (small ? 0.92f : 0.80f);       // Sonne sitzt auf dem Horizont

                // Schein hinter der Sonne
                if (!small)
                    using (var glow = new GraphicsPath())
                    {
                        glow.AddEllipse(cx - r * 1.45f, cy - r * 1.45f, r * 2.9f, r * 2.9f);
                        using (var pg = new PathGradientBrush(glow))
                        {
                            pg.CenterColor = Color.FromArgb(110, 255, 60, 200);
                            pg.SurroundColors = new[] { Color.FromArgb(0, 255, 60, 200) };
                            g.FillPath(pg, glow);
                        }
                    }

                // Sonne mit Streifen in der unteren Hälfte
                using (var sun = new GraphicsPath())
                {
                    sun.AddEllipse(cx - r, cy - r, 2 * r, 2 * r);
                    using (var reg = new Region(sun))
                    {
                        float[] ys = small ? new[] { 0.22f, 0.58f } : new[] { 0.10f, 0.32f, 0.52f, 0.70f };
                        float[] hs = small ? new[] { 0.13f, 0.17f } : new[] { 0.05f, 0.08f, 0.10f, 0.13f };
                        for (int i = 0; i < ys.Length; i++)
                            reg.Exclude(new RectangleF(cx - r - 1, cy + ys[i] * r, 2 * r + 2, Math.Max(small ? 1.2f : 1f, hs[i] * r)));
                        using (var sb = new LinearGradientBrush(new PointF(0, cy - r), new PointF(0, cy + r), Color.Black, Color.Black))
                        {
                            var blend = new ColorBlend();
                            blend.Colors = new[] { Color.FromArgb(255, 232, 100), Color.FromArgb(255, 128, 56), Color.FromArgb(255, 48, 176) };
                            blend.Positions = new[] { 0f, 0.5f, 1f };
                            sb.InterpolationColors = blend;
                            g.FillRegion(sb, reg);
                        }
                    }
                }

                // Boden unterhalb des Horizonts
                using (var ground = new LinearGradientBrush(new PointF(0, hz), new PointF(0, s), Color.FromArgb(22, 12, 40), Color.FromArgb(8, 6, 16)))
                    g.FillRectangle(ground, 0, hz, s, s - hz);

                if (!small)
                {
                    // Neon-Gitter in Perspektive
                    using (var gp = new Pen(Color.FromArgb(170, 255, 60, 200), Math.Max(1f, 1.8f * k)))
                    {
                        for (int i = -5; i <= 5; i++) g.DrawLine(gp, cx + i * 0.05f * s, hz, cx + i * 0.32f * s, s);
                        float y = hz, step = 0.028f * s;
                        for (int i = 0; i < 5; i++) { y += step; step *= 1.55f; g.DrawLine(gp, 0, y, s, y); }
                    }
                    // Skyline-Silhouette vor der Sonne (x, Oberkante, Breite als Anteil)
                    float[][] b = {
                        new[] { 0.08f, 0.56f, 0.09f }, new[] { 0.16f, 0.47f, 0.07f }, new[] { 0.225f, 0.60f, 0.08f },
                        new[] { 0.30f, 0.52f, 0.06f }, new[] { 0.36f, 0.62f, 0.07f },
                        new[] { 0.58f, 0.60f, 0.07f }, new[] { 0.645f, 0.50f, 0.07f }, new[] { 0.71f, 0.42f, 0.08f },
                        new[] { 0.79f, 0.57f, 0.07f }, new[] { 0.86f, 0.50f, 0.07f } };
                    using (var city = new SolidBrush(Color.FromArgb(14, 9, 26)))
                        foreach (var t in b) g.FillRectangle(city, t[0] * s, t[1] * s, t[2] * s, hz - t[1] * s + 1);
                    // ein paar leuchtende Fenster
                    using (var win = new SolidBrush(Color.FromArgb(220, 0, 214, 255)))
                    {
                        float w = Math.Max(1.5f, 3.2f * k);
                        float[][] wn = { new[] { 0.185f, 0.52f }, new[] { 0.185f, 0.58f }, new[] { 0.325f, 0.57f },
                                         new[] { 0.67f, 0.55f }, new[] { 0.74f, 0.47f }, new[] { 0.74f, 0.54f }, new[] { 0.885f, 0.56f } };
                        foreach (var t in wn) g.FillRectangle(win, t[0] * s, t[1] * s, w, w * 1.3f);
                    }
                }

                // Horizontlinie in Cyan
                using (var hp = new Pen(Color.FromArgb(0, 214, 255), small ? Math.Max(1f, s / 16f) : 3.5f * k))
                    g.DrawLine(hp, 0, hz, s, hz);

                g.ResetClip();
                // Rahmen: Verlauf Cyan -> Magenta
                float bw = small ? 1f : 5f * k;
                var inner = new RectangleF(outer.X + bw / 2, outer.Y + bw / 2, outer.Width - bw, outer.Height - bw);
                using (GraphicsPath borderPath = Round(inner, (small ? 0.20f : 0.22f) * s - bw / 2))
                using (var bb = new LinearGradientBrush(new PointF(outer.Left, outer.Top), new PointF(outer.Right, outer.Bottom),
                                                        Color.FromArgb(0, 214, 255), Color.FromArgb(255, 60, 200)))
                using (var bp = new Pen(bb, bw))
                    g.DrawPath(bp, borderPath);
            }
        }
        return bmp;
    }

    // ICO schreiben: 256 px als PNG, kleinere Größen als 32-Bit-Bitmap (maximal kompatibel, z. B. fürs Tray-Icon)
    public static void WriteIco(string path, int[] sizes)
    {
        var images = new List<byte[]>();
        foreach (int s in sizes)
            using (Bitmap b = Draw(s)) images.Add(s >= 256 ? Png(b) : Dib(b));
        using (var fs = File.Create(path))
        using (var w = new BinaryWriter(fs))
        {
            w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                int s = sizes[i];
                w.Write((byte)(s >= 256 ? 0 : s)); w.Write((byte)(s >= 256 ? 0 : s));
                w.Write((byte)0); w.Write((byte)0); w.Write((short)1); w.Write((short)32);
                w.Write(images[i].Length); w.Write(offset);
                offset += images[i].Length;
            }
            foreach (byte[] img in images) w.Write(img);
        }
    }

    static byte[] Png(Bitmap b) { using (var ms = new MemoryStream()) { b.Save(ms, ImageFormat.Png); return ms.ToArray(); } }

    static byte[] Dib(Bitmap b)
    {
        int s = b.Width, maskRow = ((s + 31) / 32) * 4;
        using (var ms = new MemoryStream())
        using (var w = new BinaryWriter(ms))
        {
            w.Write(40); w.Write(s); w.Write(s * 2); w.Write((short)1); w.Write((short)32);
            w.Write(0); w.Write(s * s * 4 + maskRow * s); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
            for (int y = s - 1; y >= 0; y--)                                  // Zeilen von unten nach oben, BGRA
                for (int x = 0; x < s; x++) { Color c = b.GetPixel(x, y); w.Write(c.B); w.Write(c.G); w.Write(c.R); w.Write(c.A); }
            w.Write(new byte[maskRow * s]);                                   // AND-Maske leer (Alpha wird genutzt)
            return ms.ToArray();
        }
    }

    // Vorschau: alle Größen auf dunklem (Taskleiste) und hellem Grund
    public static void Preview(string path, int[] sizes)
    {
        int W = 1000, H = 400;
        using (var bmp = new Bitmap(W, H))
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillRectangle(new SolidBrush(Color.FromArgb(32, 32, 32)), 0, 0, W, H / 2 + 60);
            g.FillRectangle(new SolidBrush(Color.FromArgb(243, 243, 243)), 0, H / 2 + 60, W, H - (H / 2 + 60));
            using (Bitmap big = Draw(256)) g.DrawImage(big, 20, 12, 256, 256);
            float x = 300;
            foreach (int s in sizes)
            {
                if (s >= 256) continue;
                using (Bitmap b = Draw(s))
                {
                    g.DrawImage(b, x, 140 - s / 2, s, s);
                    g.DrawImage(b, x, 330 - s / 2, s, s);
                }
                g.DrawString(s + " px", new Font("Segoe UI", 8f), Brushes.Gray, x, 210);
                x += Math.Max(s, 30) + 22;
            }
            bmp.Save(path, ImageFormat.Png);
        }
    }
}
'@
Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing -Language CSharp

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$ico = Join-Path $root "icon.ico"
[IconMaker]::WriteIco($ico, $sizes)
Write-Host "OK -> $ico"
if ($Preview) { [IconMaker]::Preview($Preview, $sizes); Write-Host "Vorschau -> $Preview" }
