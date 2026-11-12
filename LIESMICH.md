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

## Video statt Bild (Branch `feature/video`)
Leg ein Video in den Ordner `videos\` und starte das Programm neu: Es nimmt automatisch
das **neueste** Video dort (.mp4/.m4v/.mov/.mkv/.webm). Einen festen Pfad kannst du über
`VideoFile` im `Config`-Block setzen (leer = automatisch). Das Video läuft stumm, in
Endlosschleife, hardware-dekodiert über Windows Media Foundation (`IMFMediaEngine`).
Ist kein Video da oder schlägt die Wiedergabe fehl, läuft automatisch wieder das Bild-Wallpaper.

- Format: **MP4 mit H.264** (läuft auf jedem Windows ohne Zusatz-Codecs). H.265/HEVC
  und WebM brauchen eigene Erweiterungen aus dem Microsoft Store.
- Ideal: nahtlos loopender Clip in Bildschirmauflösung, 24–30 fps, ohne Tonspur.
- Das Video wird auf den Bildschirm „gecovert" (füllt alles, Ränder werden abgeschnitten).
- Im Videomodus sind die bild-spezifischen Effekte (Sonne, Stadt-/Wolkenlichter) aus.
  Regen und Neon-Schimmer lassen sich mit `VideoRain` / `VideoNeonPulse` zuschalten.
- `UseVideo = false` erzwingt den Bildmodus.
- Videos sind per `.gitignore` vom Repo ausgeschlossen (zu groß) und liegen nur lokal.

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
- Entwickelt auf Windows 11 (aktueller Build). Für Windows 10 / ältere Windows-11-Builds gibt es eine
  Suche nach der klassischen WorkerW-Ebene, die aber ungetestet ist.

## Stabilität
- **Selbstheilung:** Nach einem Explorer-Neustart, einem Grafiktreiber-Update oder einer geänderten
  Bildschirmauflösung startet sich das Wallpaper automatisch neu (Grund steht in `CyberWallpaper.log`).
  Nach 5 erfolglosen Neustarts in Folge gibt es mit einer Meldung auf.
- **Nur eine Instanz:** Ein zweiter Start tut nichts, solange das Wallpaper läuft. Zum Neustarten
  (z. B. nach einem neuen Video) erst über das Tray-Icon beenden.
- Schlägt der Start fehl, erscheint eine Meldung mit dem Pfad zur Log-Datei; es bleibt kein
  leeres Fenster über dem Desktop stehen.
