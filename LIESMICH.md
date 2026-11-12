# Cyber Wallpaper (DirectX, Bild + Effekte)

Animiertes Cyberpunk-Live-Wallpaper **hinter den Desktop-Icons**: ein Hintergrundbild
mit animierten Effekten darüber (Regen, flackernde Stadtlichter, Neon-Schimmer,
Lichtzucken, Vignette, Funken). Rendert per Direct3D 11 + Direct2D auf eine
DXGI-Swapchain im WorkerW-Kindfenster. Keine Installation nötig.

## Starten
Doppelklick auf `CyberWallpaper.exe`.

## Beenden
Rechtsklick auf das Tray-Icon (unten rechts bei der Uhr) -> **Beenden**.

## Eigenes Bild verwenden
Lege dein Wunschbild als `background.png` in diesen Ordner (überschreibt das aktuelle)
und starte das Programm neu. Am besten in Bildschirmauflösung oder größer.
(WebP wird nicht direkt geladen -> vorher in PNG umwandeln.)

## Effekte / Aussehen anpassen
Oben in `CyberWallpaperFX.cs` im Block `Config` ändern, dann neu bauen. Wichtige Werte:

- Regen:   `Rain`, `RainCount`, `RainSpeedMin/Max`, `RainWind`, `RainLenMin/Max`, `RainColor`
- Lichter: `Sparkles`, `SparkleCount`
- Funken:  `Embers`, `EmberCount`
- Schimmer:`NeonPulse`, `NeonPulseMax`   (Farbstich; kleiner = dezenter)
- Blitz:   `Lightning`, `LightningEveryMinSec/MaxSec`
- Vignette:`Vignette`, `VignetteStrength`
- `Fps`

Jeder `bool` schaltet einen Effekt ganz ab/an.

Neu bauen (PowerShell im Projektordner):
    .\build.ps1

## Automatisch mit Windows starten
1. Win + R, `shell:startup`, Enter.
2. Verknüpfung zu `CyberWallpaper.exe` in den Autostart-Ordner legen.

## Dateien
- `CyberWallpaper.exe`   – das Programm
- `CyberWallpaperFX.cs`  – Quellcode
- `background.png`       – dein Hintergrundbild
- `build.ps1`            – Build-Skript (nutzt den eingebauten C#-Compiler)

## Grenzen
- Primär auf den Hauptmonitor ausgelegt.
- Nach einem Explorer-Neustart das Wallpaper neu starten.
