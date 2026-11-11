# Änderungen

Alle nennenswerten Änderungen an diesem Projekt. Format nach [Keep a Changelog](https://keepachangelog.com/de/1.1.0/), Versionen nach [Semantic Versioning](https://semver.org/lang/de/).

## [Unveröffentlicht]

## [1.1.0] – 2026-11-11

### Neu
- **Selbsterkennung** beim ersten Start: Tools, Pfade, Starter-Skripte, Ports und Logs werden automatisch gefunden (`config.json`); nicht gefundene Tools werden ausgeblendet.
- Tray-Menü: „Tools neu erkennen“ und „Erkannte Tools anzeigen“; Knopf „Tools neu erkennen“ im Bearbeiten-Modus.
- **Installationsprogramm** (`KI-Zentrale-Setup.exe`): pro Benutzer, ohne Admin-Rechte, Verknüpfungen, Autostart, Eintrag unter „Installierte Apps“, stiller Modus `/S`.
- Deinstallation über `KI-Zentrale.exe --uninstall`.
- App als eigenständige Einzeldatei inklusive .NET-Laufzeit.

### Geändert
- Oberfläche ohne feste Hardware- und Modellangaben; Modellname und Ports kommen aus der Erkennung.
- Abhängigkeiten aktualisiert: WebView2 1.0.4314.35, actions/upload-artifact 7, softprops/action-gh-release 3.

## 1.0.0 – 2026-11-10 (interne Version, nicht veröffentlicht)

### Neu
- Desktop-Widget im Anthrazit-/Carbon-Design mit Kacheln für NInfer, Hermes Agent, Open WebUI, OpenCode, Claude, ComfyUI, Pinokio, Luminar AI und llama.cpp.
- 3D-Tasten mit Drück-Animation und Lichtwelle.
- ✎ Bearbeiten-Modus: Kacheln im Raster verschieben, entfernen, hinzufügen.
- Systemmonitor (CPU, RAM, GPU über NVML) mit Pin „immer im Vordergrund“.
- Nur im Infobereich, randlose Fenster, DPI-sichere Platzierung mit Wächter.
- Startparameter `--monitor`.

[Unveröffentlicht]: https://github.com/romkakojo/ki-zentrale/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/romkakojo/ki-zentrale/releases/tag/v1.1.0
