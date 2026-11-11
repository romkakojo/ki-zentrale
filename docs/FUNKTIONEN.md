# Funktionen der KI-Zentrale

Vollständige Beschreibung aller Funktionen (Version 1.1.0).

**Inhalt:** [Überblick](#überblick) · [Erster Start & Selbsterkennung](#erster-start--selbsterkennung) · [Das Widget](#das-widget) · [Modellserver-Karte](#modellserver-karte) · [Tool-Kacheln](#tool-kacheln) · [Fußleiste](#fußleiste) · [Bearbeiten-Modus](#bearbeiten-modus) · [Systemmonitor](#systemmonitor) · [Infobereich (Tray)](#infobereich-tray) · [Fenster & Anzeige](#fenster--anzeige) · [Installation & Deinstallation](#installation--deinstallation) · [Startparameter](#startparameter) · [Dateien](#dateien) · [Grenzen](#grenzen)

---

## Überblick

Die KI-Zentrale ist eine Windows-App für Menschen, die KI lokal betreiben. Sie besteht aus zwei kleinen, randlosen Fenstern im Anthrazit-/Carbon-Design und einem Symbol im Infobereich:

| Teil | Zweck |
|---|---|
| **Widget** | Status und Steuerung aller erkannten KI-Tools |
| **Systemmonitor** | CPU, RAM und GPU in Echtzeit |
| **Tray-Symbol** | Ein-/Ausblenden, Menü, Status des Modellservers |

Alles läuft in **einem** Prozess (~100 MB RAM), ohne Grafikbeschleunigung und ohne Netzwerk-Port.

---

## Erster Start & Selbsterkennung

Beim ersten Start (wenn noch keine `config.json` existiert) durchsucht die KI-Zentrale den PC – typischerweise in wenigen Sekunden – und zeigt anschließend eine Übersicht wie:

```
✔  NInfer (Modellserver)  –  Port 8080, Starter gefunden
✔  Hermes Agent  –  API Port 8642, Telegram
✔  Open WebUI  –  Port 8090
✔  OpenCode
✖  Luminar AI  –  nicht gefunden
…
Projektordner: E:\Projekte
```

### Was erkannt wird

| Tool | Erkannt über | Gespeichert |
|---|---|---|
| **NInfer** | laufender Prozess `ninfer-serve.exe` oder Suche auf allen Festplatten (bis Tiefe 3) | Programmpfad, **Starter-Skript** (`start-ninfer.ps1` bzw. ein `.ps1`/`.bat`, das `ninfer-serve` aufruft), **Port** (aus Befehlszeile oder Starter, sonst 8080), **Modelldatei** (`*.ninfer`), **Log** (`server.log.err`), Datei mit aktuellem Kontext |
| **llama.cpp** | Prozess `llama-server.exe` oder Laufwerkssuche | Pfad; Markierung „ausgemustert“, wenn NInfer vorhanden ist |
| **Hermes Agent** | Ordner `%LOCALAPPDATA%\hermes` (bzw. `HERMES_HOME`) | Programmpfad, **API-Server** an/aus, Port und Modellname aus `.env`, ob **Telegram** eingerichtet ist, Gateway-Log |
| **Open WebUI** (Desktop-App) | Windows-Programmliste | Programmpfad, **eigener Port** aus der App-Konfiguration, Startmenü-ID |
| **OpenCode** | Suchpfad (`opencode.cmd`/`.ps1`/`.exe`, auch npm-Ordner) | Pfad, Konfigurationsdatei |
| **Claude Desktop, ComfyUI** (bevorzugt „Comfy Desktop“), **Pinokio, Luminar AI** | Startmenü (`shell:AppsFolder`) | App-ID zum Starten, Prozessnamen für den Status |
| Sonstiges | Laufwerke, Suchpfad | **Projektordner** (`X:\Projekte` auf einem beliebigen Laufwerk, sonst „Dokumente“), **Terminal** (Windows Terminal, sonst `cmd`) |

- Die Suche überspringt Systemordner (Windows, ProgramData, Papierkorb, Spielebibliotheken, `node_modules` …).
- Geheimnisse (API-Schlüssel, Bot-Token) werden **nur auf Vorhandensein geprüft**, nie gespeichert.
- **Nicht gefundene Tools werden ausgeblendet**; im Bearbeiten-Modus stehen sie unter „Nicht installiert“.
- **Erneut erkennen:** Tray → „Tools neu erkennen“ oder Knopf im Bearbeiten-Modus (z. B. nach Installation eines neuen Tools oder Port-Änderung).
- **Ansehen:** Tray → „Erkannte Tools anzeigen“.

---

## Das Widget

Ein schmales Fenster (460 × bis zu 1000 Pixel bei 100 %), standardmäßig am rechten Rand des Hauptbildschirms.

### Kopfzeile

| Element | Funktion |
|---|---|
| Logo + „KI-Zentrale“ | Uhrzeit der letzten Aktualisierung; **an der Kopfzeile ziehen = Fenster verschieben** |
| Pulskurve | Systemmonitor ein-/ausblenden |
| ✎ | Bearbeiten-Modus |
| ⟳ | sofort aktualisieren (sonst automatisch alle 3 Sekunden) |
| — | in den Infobereich ausblenden |

### Tasten

Alle Tasten sind 3D-Tasten: beim Überfahren heben sie sich leicht, beim Klick sinken sie 4 Pixel ein, mit Lichtwelle an der Klickstelle; die Kachel gibt mit nach. Farben:

- **Orange** – Hauptaktion (Starten, Öffnen, Chat)
- **Graphit** – Nebenaktion
- **Stahl** – Terminal
- **Dunkelrot** – stoppende Aktionen; **fragen nach** („Sicher? Nochmal“ – zweiter Klick innerhalb von 3 Sekunden bestätigt)

Rückmeldungen erscheinen als kurze Einblendung unten im Fenster.

---

## Modellserver-Karte

Die große Karte oben zeigt den lokalen Modellserver (NInfer).

| Anzeige | Bedeutung |
|---|---|
| Pulsierender Punkt | **grün** = bereit, **gelb** = lädt Modell, **grau** = aus |
| Titel | „NInfer · *Modell-ID*“ (vom Server gemeldet) |
| Untertitel | „läuft seit HH:MM · Port …“ bzw. „lädt Modell …“ / „gestoppt · Grafikspeicher frei“ |
| Chip | bereit / startet / aus / fehlt |
| **Kontext** | aktuelle Kontextgröße (z. B. „128k“) – aus der API oder der Kontext-Datei des Starters |
| **Tempo** | Token/s der letzten Antwort (aus dem Server-Log) |
| **Aktiv** | „rechnet“ oder „frei“ |
| **Grafikspeicher** | belegt / gesamt mit Verlaufsbalken (Graphit → Orange) |

**Aktionen (läuft):** ⟳ Neustart · 📈 Monitor (Web-Monitor des Servers) · ■ Stoppen (mit Rückfrage)
**Aktionen (aus):** ▶ Starten (über das erkannte Starter-Skript; ohne Skript direkt mit Modelldatei und Port) · 📜 Log
**Nicht gefunden:** 🔍 Tools neu erkennen

---

## Tool-Kacheln

Jede Kachel zeigt Symbol, Name, Kurzbeschreibung und einen **Statuspunkt** (grün = läuft). Aktive Tools bekommen einen orangen Rand am Symbol. Gruppiert in **„Agenten & Oberflächen“** (mit Zähler „x von y aktiv“) und **„Kreativ & Werkzeuge“**.

| Kachel | Status-Chips | Aktionen |
|---|---|---|
| **Hermes** | Telegram (verbunden laut Log), API-Port, Dashboard | **Chat** – öffnet Open WebUI mit dem Modell „Hermes“ (startet Open WebUI, falls nötig) · **Dashboard** – öffnet das Hermes-Web-Dashboard (startet es, falls nötig) |
| **Open WebUI** | Port bzw. „aus“ | **Öffnen** – startet die Desktop-App |
| **OpenCode** | Hintergrunddienst + Port | **Terminal** – OpenCode im Projektordner (Windows Terminal) · **Ordner…** – Projektordner wählen, dann OpenCode darin starten |
| **Claude** | „nicht lokal“ | **Öffnen** |
| **ComfyUI** | – | **Starten** (Hinweis: braucht viel Grafikspeicher) |
| **Pinokio** | – | **Öffnen** |
| **Luminar AI** | – | **Öffnen** |
| **llama.cpp** | gedimmt, wenn ausgemustert | **Info** |

---

## Fußleiste

| Taste | Funktion |
|---|---|
| 🧹 **VRAM frei** | stoppt den Modellserver (und llama.cpp) – gibt den Grafikspeicher frei (mit Rückfrage) |
| 📜 **Logs** | öffnet das Modellserver-Log im Editor |
| ⏻ **Alles stoppen** | stoppt Modellserver und ComfyUI (mit Rückfrage) |

---

## Bearbeiten-Modus

Über **✎** (wird orange, Kacheln wackeln leicht):

- **Verschieben:** Kachel greifen und ins Raster ziehen; eine orange gestrichelte Lücke zeigt die Zielposition. Auch **zwischen den Bereichen** möglich. Leere Bereiche zeigen „hierher ziehen“.
- **Entfernen:** rotes **×** an der Kachelecke.
- **Wieder hinzufügen:** Leiste oben listet entfernte Kacheln als **+ Name**.
- **Standard-Anordnung wiederherstellen.**
- **🔍 Tools neu erkennen.**
- **✓ Fertig** beendet den Modus.

Jede Änderung wird sofort gespeichert (`layout.json`) und bleibt über Neustarts erhalten. Nicht installierte Tools behalten ihren Platz und erscheinen wieder, sobald sie erkannt werden.

---

## Systemmonitor

Kleines Zusatzfenster (300 × 340 Pixel bei 100 %), standardmäßig links neben dem Widget. Aktualisierung jede Sekunde, Verlaufskurven über 60 Sekunden.

| Karte | Inhalt |
|---|---|
| **CPU** | Auslastung in %, Prozessorname, Anzahl Threads, Verlauf |
| **RAM** | belegt / gesamt in GB, Prozent, **zugesagter Speicher** (RAM + Auslagerungsdatei) – frühe Warnung bei Speichermangel, Balken und Verlauf |
| **GPU** | Auslastung in % mit Verlauf, **VRAM** belegt/gesamt mit Balken, **Temperatur**, **Leistung** (aktuell/Limit in W), **Takt** (MHz), **Lüfter** (%) |

- **Farben:** Kurven und Balken ab 70 % orange, ab 90 % rot; Temperatur ab 70 °C orange, ab 80 °C rot.
- **📌 Pin:** hält den Monitor **dauerhaft im Vordergrund** über allen Fenstern (Symbol orange); erneuter Klick löst ihn.
- **Verschieben** an der Kopfzeile; **—** blendet aus.
- Position, Pin und Sichtbarkeit werden gespeichert (`monitor.json`) und beim nächsten Start wiederhergestellt.
- **Ein-/ausblenden:** Pulskurven-Symbol im Widget, Tray-Menü „Systemmonitor“, Desktop-Verknüpfung „Systemmonitor“.
- GPU-Werte kommen direkt aus dem NVIDIA-Treiber (NVML) – ohne externe Programme. Ohne NVIDIA-Karte zeigt die GPU-Karte „NVML nicht verfügbar“.

---

## Infobereich (Tray)

- **Symbol:** Graphit-Plakette; Punkt **orange** = Modellserver bereit, **grau** = nicht bereit. Tooltip zeigt den Status.
- **Linksklick:** Widget ein-/ausblenden.
- **Rechtsklick-Menü:**
  - Widget anzeigen / Widget ausblenden
  - Systemmonitor (CPU/RAM/GPU) – mit Häkchen, wenn sichtbar
  - NInfer-Status (z. B. „bereit (128k Kontext)“)
  - Tools neu erkennen · Erkannte Tools anzeigen
  - **Mit Windows starten** (an/aus)
  - KI-Zentrale beenden

Schließen oder Minimieren der Fenster blendet sie in den Infobereich aus – beendet wird nur über das Menü.

---

## Fenster & Anzeige

- **Randlos** mit abgerundeten Ecken (Windows 11), Schatten und dunklem Rahmen.
- **Kein Taskleisten- und kein Alt-Tab-Eintrag.**
- **DPI-sicher:** Größe und Position werden in echten Pixeln mit der Skalierung des jeweiligen Monitors berechnet – funktioniert bei 100–300 %, jeder Auflösung und mit mehreren Monitoren.
- **Wächter:** Alle 4 Sekunden wird geprüft, ob mindestens 60 % jedes Fensters auf einem Bildschirm liegen; sonst wird es an den Standardplatz zurückgeholt (z. B. nach Abstecken eines Monitors oder Änderung der Skalierung). Selbst verschobene Fenster bleiben, wo sie sind.
- **Einzelinstanz:** Ein zweiter Start holt nur das vorhandene Fenster nach vorn.
- **Ohne GPU-Beschleunigung** (WebView2 mit `--disable-gpu`), damit der Grafikspeicher dem Modell bleibt.
- Wenig Platz? Inhalte scrollen statt sich zu überlappen.

---

## Installation & Deinstallation

### Setup (`KI-Zentrale-Setup.exe`)

- Assistent im gleichen Design; läuft ohne Vorinstallation (nutzt das in Windows enthaltene .NET Framework 4.8).
- **Pro Benutzer, ohne Admin-Rechte**, Standardordner `%LOCALAPPDATA%\Programs\KI-Zentrale` (wählbar).
- Optionen: Desktop-Verknüpfungen (KI-Zentrale, Systemmonitor) · Startmenü-Einträge · Mit Windows starten · Nach Installation starten.
- Prüft die **WebView2-Laufzeit** und verlinkt den Download, falls sie fehlt.
- Beendet eine laufende KI-Zentrale vor dem Aktualisieren; **Update = einfach neues Setup ausführen** (Einstellungen bleiben).
- Trägt die App unter **„Installierte Apps“** ein (Version, Größe, Symbol).
- **Still:** `KI-Zentrale-Setup.exe /S` (Desktop + Startmenü, Autostart wie vorher).

### Deinstallation

Über „Installierte Apps“, Startmenü → „KI-Zentrale deinstallieren“ oder `KI-Zentrale.exe --uninstall`:

- entfernt App, Verknüpfungen, Autostart und Registrierung,
- fragt, ob auch die Einstellungen gelöscht werden sollen,
- **lässt alle KI-Tools unverändert**.

---

## Startparameter

| Aufruf | Wirkung |
|---|---|
| `KI-Zentrale.exe` | Widget anzeigen (bzw. vorhandenes nach vorn holen) |
| `KI-Zentrale.exe --monitor` | nur den Systemmonitor anzeigen |
| `KI-Zentrale.exe --uninstall` | Deinstallation mit Rückfragen |
| `KI-Zentrale.exe --uninstall --quiet` | Deinstallation ohne Rückfragen (Einstellungen bleiben) |
| `KI-Zentrale-Setup.exe /S` | stille Installation |

---

## Dateien

| Ort | Inhalt |
|---|---|
| `%LOCALAPPDATA%\Programs\KI-Zentrale\` | `KI-Zentrale.exe`, `monitor.ico` |
| `%LOCALAPPDATA%\KI-Zentrale\config.json` | erkannte Tools, Pfade, Ports |
| `%LOCALAPPDATA%\KI-Zentrale\layout.json` | Anordnung der Kacheln |
| `%LOCALAPPDATA%\KI-Zentrale\monitor.json` | Position, Pin, Sichtbarkeit des Monitors |
| `%LOCALAPPDATA%\KI-Zentrale\WebView2\` | Arbeitsdaten der Oberfläche |
| `HKCU\…\Run\KI-Zentrale` | Autostart (optional) |
| `HKCU\…\Uninstall\KI-Zentrale` | Eintrag in „Installierte Apps“ |

---

## Grenzen

- Nur **Windows 10/11 x64**; die Oberfläche ist auf Deutsch.
- GPU-Werte nur für **NVIDIA** (NVML); die **CPU-Temperatur** wird nicht angezeigt (benötigt Admin-Rechte bzw. Zusatztreiber).
- Die Kacheln decken die oben genannten Tools ab; weitere lassen sich ergänzen ([CONTRIBUTING.md](../CONTRIBUTING.md)).
- Der Hermes-Chat setzt Open WebUI mit eingerichteter Hermes-Verbindung voraus.
- Das Setup ist nicht code-signiert (SmartScreen-Hinweis beim ersten Start).
