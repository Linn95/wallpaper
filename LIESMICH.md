# Cyber Wallpaper (DirectX, Bild + Effekte)

Animiertes Cyberpunk-Live-Wallpaper **hinter den Desktop-Icons**: ein Hintergrundbild
mit animierten Effekten darüber (Regen, flackernde Stadtlichter, Neon-Schimmer,
Lichtzucken, Vignette, Funken). Rendert per Direct3D 11 + Direct2D auf eine
DXGI-Swapchain im WorkerW-Kindfenster.

## Installieren
Am einfachsten über den Installer **`CyberWallpaper-Setup.exe`** (hängt an jedem GitHub-Release):
- Installiert nur für dich nach `%LOCALAPPDATA%\Programs\CyberWallpaper`, **keine Administratorrechte** nötig.
- Optionen: **Mit Windows starten**, Verknüpfung auf dem Desktop, nach der Installation starten.
- Legt einen Startmenü-Eintrag „Cyber Wallpaper" an und trägt sich in **Apps & Features** ein.
- Nochmal ausführen = **Aktualisieren**; Videos und Einstellungen bleiben erhalten.
- **Deinstallieren** über Apps & Features (oder `Deinstallieren.exe` im Programmordner). Du wirst gefragt,
  ob deine Videos und Einstellungen mit gelöscht werden sollen.
- Für Profis: `CyberWallpaper-Setup.exe /silent [/dir:Pfad] [/noautostart] [/desktop] [/nostart]`,
  `Deinstallieren.exe /uninstall /silent [/purge]`.

Ohne Installer geht es auch: das ZIP aus dem Release entpacken und `CyberWallpaper.exe` starten.

## Starten
Startmenü → „Cyber Wallpaper" (bzw. Doppelklick auf `CyberWallpaper.exe`).

## Beenden
Rechtsklick auf das Tray-Icon (unten rechts bei der Uhr) -> **Beenden**.

## Hintergrund wählen (Oberfläche)
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

## Video statt Bild
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
In der Oberfläche den Button **Mit Windows starten: An/Aus** klicken (oder im Installer die Option
setzen). Das trägt das Wallpaper in den Windows-Autostart ein (zu sehen im Task-Manager unter
„Autostart-Apps"). Eine alte Verknüpfung im Autostart-Ordner (`shell:startup`) wird dabei automatisch
entfernt, damit nichts doppelt startet.

## Dateien
- `CyberWallpaper.exe`   – das Programm
- `CyberWallpaperFX.cs`  – Quellcode (Rendering, Video, Wallpaper-Einbindung)
- `MediaForm.cs`         – Quellcode der Oberfläche (Auswahl, Einstellungen)
- `Shared.cs`            – gemeinsame Helfer (Autostart, Verknüpfungen, Titelleiste)
- `Setup.cs`             – Quellcode des Installers/Deinstallers
- `settings.txt`         – gespeicherte Auswahl (lokal, nicht im Repo)
- `background.png`       – dein Hintergrundbild
- `build.ps1`            – baut `CyberWallpaper.exe` (nutzt den eingebauten C#-Compiler)
- `build-setup.ps1`      – baut danach den Installer `CyberWallpaper-Setup.exe`

## Grenzen
- Primär auf den Hauptmonitor ausgelegt.
- Entwickelt auf Windows 11 (aktueller Build). Für Windows 10 / ältere Windows-11-Builds gibt es eine
  Suche nach der klassischen WorkerW-Ebene, die aber ungetestet ist.

## Pause bei Vollbild (z. B. Spiele)
Läuft eine Anwendung im Vollbild im Vordergrund (Spiel, Video im Vollbild, Präsentation), ist das
Wallpaper ohnehin verdeckt: Es **pausiert** dann automatisch – das Video wird angehalten und es wird
nichts mehr gezeichnet, damit Grafikkarte und CPU frei für das Spiel sind. Sobald die Anwendung nicht
mehr im Vordergrund ist, läuft es sofort weiter (die Statuszeile der Oberfläche zeigt den Zustand).
- Erkannt werden randloses Vollbild auf dem Hauptbildschirm und exklusives Direct3D-Vollbild.
- Vollbild auf einem zweiten Monitor pausiert nicht (das Wallpaper ist dann noch sichtbar).
- Pausiert wird erst nach ~1 s, damit kurzes Alt+Tab nicht flackert.
- Einstellungen im `Config`-Block: `PauseOnFullscreen` (an/aus), `PauseOnMaximized` (auch bei
  maximierten Fenstern pausieren, Standard: aus).

## Stabilität
- **Geänderte Bildschirmauflösung** (auch Docking, Monitorwechsel, Remote-Desktop): Das Wallpaper passt
  sich an Ort und Stelle an. Das Video läuft weiter, die Oberfläche bleibt offen.
- **Selbstheilung:** Nach einem Explorer-Neustart oder einem Grafiktreiber-Update startet sich das
  Wallpaper automatisch neu (Grund steht in `CyberWallpaper.log`). War die Oberfläche offen, öffnet sie
  sich danach wieder. Nach 5 erfolglosen Neustarts in Folge oder mehr als 20 Neustarts in einer Stunde
  gibt es mit einer Meldung auf.
- **Nur eine Instanz:** Ein zweiter Start legt kein zweites Wallpaper darüber, sondern öffnet die
  Oberfläche im laufenden – auch wenn das Wallpaper gerade erst startet. Neue Videos lassen sich dort
  ohne Neustart auswählen.
- Nach einem automatischen Neustart läuft wieder der zuletzt gewählte Hintergrund (`settings.txt`).
  Lässt er sich beim Start einmal nicht laden, zeigt das Wallpaper das Standard-Bild, die Auswahl
  bleibt aber gespeichert.
- Schlägt der Start fehl, versucht es das Wallpaper noch einige Male mit wachsender Pause. Erst danach
  erscheint eine Meldung mit dem Pfad zur Log-Datei; es bleibt kein leeres Fenster über dem Desktop stehen.
