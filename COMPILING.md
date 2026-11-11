# KI-Zentrale bauen

## Voraussetzungen

- Windows 10 (1809+) oder Windows 11, x64
- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- Windows PowerShell 5.1 (in Windows enthalten)
- Internetzugang beim ersten Build (NuGet-Pakete: WebView2, .NET-Laufzeit für die Einzeldatei, Referenz-Assemblies für .NET Framework 4.8)

Visual Studio ist nicht nötig; die Lösung `KI-Zentrale.sln` lässt sich aber mit Visual Studio 2022+ oder JetBrains Rider öffnen.

## Alles bauen

```powershell
./build.ps1
```

Ergebnis in `artifacts\`:

| Datei | Inhalt |
|---|---|
| `artifacts\app\KI-Zentrale.exe` | die App als Einzeldatei inkl. .NET-Laufzeit (~50 MB) |
| `artifacts\KI-Zentrale-Setup.exe` | das Installationsprogramm (enthält die App) |

Optionen: `./build.ps1 -SkipSetup` (nur App), `./build.ps1 -Version 1.2.0` (Version für diesen Build überschreiben).

## Einzeln bauen

```powershell
# App
dotnet publish src/KIZentrale/KIZentrale.csproj -c Release -o artifacts/app
# Setup (braucht artifacts/app/KI-Zentrale.exe)
dotnet build src/Setup/Setup.csproj -c Release -o artifacts/setup
```

## Entwickeln und testen

- App direkt starten: `artifacts\app\KI-Zentrale.exe`. Läuft schon eine installierte Version, vorher im Tray → „KI-Zentrale beenden“.
- Erkennung erneut auslösen: Tray → „Tools neu erkennen“ oder `%LOCALAPPDATA%\KI-Zentrale\config.json` löschen.
- Nur den Systemmonitor: `KI-Zentrale.exe --monitor`
- Deinstallation testen: `KI-Zentrale.exe --uninstall` (`--quiet` ohne Rückfragen)
- Die HTML-Oberflächen (`widget.html`, `monitor.html`) sind eingebettete Ressourcen – nach Änderungen neu bauen.

## Symbole neu erzeugen

```powershell
powershell -File src/KIZentrale/tools/make-icon.ps1          # kiz.ico
powershell -File src/KIZentrale/tools/make-monitor-icon.ps1  # monitor.ico
```

## Release veröffentlichen

1. Version in `Directory.Build.props` erhöhen, `CHANGELOG.md` ergänzen.
2. Commit, dann Tag setzen und pushen: `git tag v1.2.0 && git push origin v1.2.0`
3. Der Workflow [release.yml](.github/workflows/release.yml) baut alles und legt ein GitHub-Release mit `KI-Zentrale-Setup.exe` und SHA-256-Prüfsumme an.
