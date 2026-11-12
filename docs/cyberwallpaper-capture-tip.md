---
name: cyberwallpaper-capture-tip
description: "Screenshot-/Test-Fallstricke beim CyberWallpaper (DPI, show-desktop, Masken-Karte)"
metadata:
  node_type: memory
  type: reference
  originSessionId: 1f7d6afd-7e7e-4bc3-b5e7-0c3964ac0ea9
  modified: 2026-11-12T09:42:14.947Z
---

Beim Testen des [[cyberwallpaper-project]]:

**NICHT `Shell.MinimizeAll()` / "Desktop anzeigen" für Screenshots benutzen.** Dieser Zustand umgeht die DXGI-Swapchain und zeigt die DWM-Basisebene (altes Windows-Hintergrundbild), nicht das Live-Wallpaper — der Mitschnitt ist dann falsch. Schlimmer: wiederholte Minimier-Zyklen können die Desktop-Komposition verklemmen, sodass das Wallpaper auch live verschwindet; Reparatur = App beenden, `explorer.exe` neu starten, App neu starten.

**Verlässlich verifizieren** stattdessen über eine **Farb-Karte**, die direkt aus den Masken (`city_mask.png`/`cloud_mask.png`) + der Platzierungs-Logik + `background.png` berechnet und auf ein abgedunkeltes Bild gezeichnet wird (cyan=Stadt/blau, magenta=Wolke/pink). Den **Live-Zustand** den Nutzer am echten Bildschirm prüfen lassen.

**Videomodus prüfen:** `CyberWallpaper.log` zeigt nach dem Start jeden Einrichtungsschritt mit HRESULT und die ersten 30 s alle 3 s `Video: Bilder=N letztesEvent=E` (steigendes N = Bilder kommen an; Event 18 = Zeitaktualisierung/läuft; bei Fehler `FEHLER code=… hr=…`). Wächst N über die Cliplänge hinaus weiter, funktioniert die Schleife.

Ein **DPI-aware** `CopyFromScreen` zeigt ebenfalls nur die Basisebene. Bild und Effekte liegen in derselben Koordinaten-Ebene (bgRect), daher ist die Ausrichtung per Konstruktion korrekt, unabhängig vom Capture.
