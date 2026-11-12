---
name: cyberwallpaper-project
description: Selbstgebautes DirectX-Live-Wallpaper-Projekt des Nutzers (hinter Desktop-Icons) und geplante Erweiterungen
metadata:
  node_type: memory
  type: project
  originSessionId: 1f7d6afd-7e7e-4bc3-b5e7-0c3964ac0ea9
  modified: 2026-11-12T09:24:40.175Z
---

Nutzer hat mit Claude ein eigenständiges Windows-Live-Wallpaper gebaut: `C:\Users\micha\Documents\claude\CyberWallpaper\` (Quelle `CyberWallpaperFX.cs`, kompiliert mit dem eingebauten .NET-Framework-csc via `build.ps1`, kein SDK/NuGet). Es zeigt ein Hintergrundbild (`background.png`, Cyberpunk/Synthwave-Stadt) mit animierten Effekten (Regen, Sonne mit pulsierendem Schein + rotierenden Strahlen, Vignette, dezenter Neon-Schimmer). Effekte oben im `Config`-Block regelbar. `Fps`, Lichter-Dichte, Schwellen usw. dort.

**Stadt-/Wolken-Lichter per Masken:** Nutzer markiert in `background - Kopie.png` Bereiche (blaue Linie = Stadt, pinke Linie = Wolke). Ein PowerShell-Schritt bildet die **Differenz** zu `background.png` (= die gemalten Linien), füllt pro Spalte zwischen min/max-y und speichert `city_mask.png` + `cloud_mask.png`. Das Programm lädt diese Masken (gleiche Bildgröße) und platziert pro Raster-Zelle genau ein pulsierendes Licht (Reservoir-Sampling, gleichmäßig, keine Hot-Spots): **Wolke hat Vorrang → pink**; sonst Stadt/echte blaue Pixel im Band → **blau**; außerhalb kein Licht. Fester Farb-Palette je Bereich. Beim Ändern der Markierung: Masken neu extrahieren, App neu starten.

**Watchdog:** Das Fenster prüft alle ~3 s, ob es noch im WorkerW hängt, und klinkt sich sonst automatisch wieder ein (z. B. nach „Desktop anzeigen"/Explorer-Neustart).

**GitHub:** privates Repo `https://github.com/Linn95/wallpaper` (Branch `main`). gh-CLI (`C:\Program Files\GitHub CLI\gh.exe`) installiert, angemeldet als GitHub-Account **Linn95** (git-config-User ist aber `reishin56-dot`). `.gitignore` schließt `*.exe` und `*.log` aus. Weiterarbeiten: `git add -A && git commit -m … && git push`.

Entscheidende Technik: Auf diesem Windows-11-Build (26200) funktioniert der klassische GDI-WorkerW-Trick NICHT (DWM komponiert injizierte GDI-Fenster nicht). Lösung: eine DXGI-Flip-Swapchain + Direct2D auf einem in das WorkerW-Kindfenster von Progman eingehängten Fenster — per Hand-COM-Interop (vtable-Aufrufe). Das wird von DWM hinter den Icons komponiert.

Geplant für später (Nutzer-Wunsch, 2026-11): Unterstützung für **GIFs** (einfach, über WIC-Frame-Dekodierung — nächster logischer Schritt), Bilder-Slideshow, evtl. Videos/MP4 (Media Foundation, aufwendiger). Siehe [[cyberwallpaper-capture-tip]] für die Screenshot-Eigenheit beim Testen.
