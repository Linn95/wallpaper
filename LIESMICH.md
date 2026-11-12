# Cyber Wallpaper (DirectX, Bild + Effekte)

Animiertes Cyberpunk-Live-Wallpaper **hinter den Desktop-Icons**: ein Hintergrundbild
mit animierten Effekten darüber (Regen, flackernde Stadtlichter, Neon-Schimmer,
Lichtzucken, Vignette, Funken). Rendert per Direct3D 11 + Direct2D auf eine
DXGI-Swapchain im WorkerW-Kindfenster. Keine Installation nötig.

## Starten
Doppelklick auf `CyberWallpaper.exe`.

## Beenden
Rechtsklick auf das Tray-Icon (unten rechts bei der Uhr) -> **Beenden**.

## Hintergrund wählen (Oberfläche, Branch `GUI`)
Öffnen auf einem dieser Wege:
- Rechtsklick aufs Tray-Icon -> **Hintergrund wählen …**
- Doppelklick aufs Tray-Icon
- `CyberWallpaper.exe` einfach nochmal starten: Es startet kein zweites Wallpaper,
  sondern öffnet die Oberfläche im laufenden.

In der Liste stehen das Standard-Bild (`background.png`, mit Sonne und Stadtlichtern),
alles aus dem `videos`-Ordner und selbst hinzugefügte Dateien. Der aktuelle Hintergrund
ist mit ▶ markiert.
- **Anwenden** (oder Doppelklick / Enter): wechselt sofort, ohne Neustart.
- **Hinzufügen …**: Videos/Bilder von irgendwo auswählen (werden nur verlinkt, nicht kopiert).
- **Entfernen** (oder Entf): nimmt selbst hinzugefügte Einträge aus der Liste, die Datei bleibt.
- **videos-Ordner öffnen**: dort abgelegte Videos erscheinen automatisch in der Liste.

Unterstützt: Videos `.mp4/.m4v/.mov/.mkv/.webm`, Bilder `.png/.jpg/.jpeg/.bmp`.
Die Auswahl wird in `settings.txt` gespeichert und beim nächsten Start wieder geladen.
Fremde Bilder laufen ohne Sonne/Stadtlichter (die passen nur zum Standard-Bild).
Schließen des Fensters versteckt es nur, das Wallpaper läuft weiter.

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
- `CyberWallpaperFX.cs`  – Quellcode (Rendering, Video, Wallpaper-Einbindung)
- `MediaForm.cs`         – Quellcode der Oberfläche (Auswahl, Einstellungen)
- `settings.txt`         – gespeicherte Auswahl (lokal, nicht im Repo)
- `background.png`       – dein Hintergrundbild
- `build.ps1`            – Build-Skript (nutzt den eingebauten C#-Compiler)

## Grenzen
- Primär auf den Hauptmonitor ausgelegt.
- Nach einem Explorer-Neustart das Wallpaper neu starten.
