# Mitmachen

Danke, dass du die KI-Zentrale verbessern willst! Beiträge jeder Größe sind willkommen – von Tippfehlern bis zu neuen Tool-Kacheln.

## Fehler melden und Ideen einbringen

- **Fehler:** [Fehlerbericht anlegen](https://github.com/romkakojo/ki-zentrale/issues/new?template=bug_report.yml). Hilfreich sind Windows-Version, Anzeige-Skalierung, KI-Zentrale-Version (Tray → „Erkannte Tools anzeigen“) und – falls relevant – die Datei `%LOCALAPPDATA%\KI-Zentrale\config.json` (vorher persönliche Pfade unkenntlich machen).
- **Ideen / neue Tools:** [Wunsch eintragen](https://github.com/romkakojo/ki-zentrale/issues/new?template=feature_request.yml).
- Sicherheitslücken bitte **nicht** öffentlich melden, siehe [SECURITY.md](SECURITY.md).

## Code beitragen

1. Repository forken und einen Zweig anlegen: `git checkout -b feature/mein-thema`
2. Bauen und testen: `./build.ps1` (siehe [COMPILING.md](COMPILING.md)), die App aus `artifacts\app\KI-Zentrale.exe` starten.
3. Commit-Nachrichten kurz und aussagekräftig, gern im Stil `Hinzugefügt: …`, `Behoben: …`.
4. Eintrag unter `[Unveröffentlicht]` in [CHANGELOG.md](CHANGELOG.md).
5. Pull Request gegen `main` öffnen und die Vorlage ausfüllen.

### Richtlinien

- **Alles lokal:** keine Telemetrie, keine Cloud-Dienste, kein offener Netzwerk-Port. Die Oberfläche spricht ausschließlich über WebView2-Abfangen mit der App.
- **Keine festen Pfade oder Ports:** Neue Tools werden in `src/KIZentrale/Detector.cs` erkannt und in `config.json` gespeichert; `Backend.cs` liest nur von dort.
- **Grafikspeicher schonen:** WebView2 läuft mit `--disable-gpu`; bitte keine GPU-lastigen Effekte.
- **Stil:** Anthrazit/Carbon, eine Akzentfarbe (Signal-Orange `#ff7a1a`), 3D-Tasten mit Drück-Animation. Siehe `docs/screenshots/design.png`.
- Code-Stil laut [.editorconfig](.editorconfig); Kommentare und Oberflächentexte auf Deutsch.

### Neues Tool hinzufügen (Kurzfassung)

1. `Detector.cs`: Erkennung ergänzen (Startmenü-App, Prozess, Datei, Port) → Eintrag in `config.json`.
2. `Backend.cs`: Status in `Status()` und Aktionen in `Action()`.
3. `widget.html`: Kachel mit `data-tile="…"` anlegen, in `DEFAULT_LAYOUT` und `TileIds` (Backend) eintragen.
4. README-Tabelle „Unterstützte Tools“ ergänzen.

## Verhaltenskodex

Sei freundlich und respektvoll. Wir folgen dem [Contributor Covenant](https://www.contributor-covenant.org/de/version/2/1/code_of_conduct/).
