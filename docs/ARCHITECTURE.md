# Aufbau

```
ki-zentrale/
├─ src/KIZentrale/          App (C#, .NET 10, WinForms + WebView2)
│  ├─ Program.cs            Start, Einzelinstanz, --monitor / --uninstall
│  ├─ MainForm.cs           Widget-Fenster, Tray, Platzierung, Wächter, Erst-Start
│  ├─ MonitorForm.cs        Systemmonitor-Fenster, Pin (TopMost), Position merken
│  ├─ Detector.cs           Selbsterkennung der Tools → config.json
│  ├─ Backend.cs            Status und Aktionen der Kacheln (liest config.json)
│  ├─ SysInfo.cs            CPU/RAM (Win32), GPU (NVML aus dem NVIDIA-Treiber)
│  ├─ WebHost.cs            gemeinsame WebView2-Umgebung (ohne GPU)
│  ├─ Uninstaller.cs        Deinstallation
│  ├─ widget.html           Oberfläche Widget (eingebettet)
│  └─ monitor.html          Oberfläche Systemmonitor (eingebettet)
├─ src/Setup/               Installationsprogramm (.NET Framework 4.8, enthält die App)
├─ docs/                    Dokumentation und Screenshots
├─ build.ps1                baut App und Setup nach artifacts/
└─ .github/workflows/       CI (Build) und Release
```

## Wie Oberfläche und App zusammenspielen

```
widget.html ──fetch('/api/…')──► WebView2 ──WebResourceRequested──► Backend.Handle()
     ▲                                                                   │
     └────────────────────── JSON-Antwort ◄──────────────────────────────┘
```

- Die Seiten werden unter `https://kiz.app/` geladen. Die App fängt alle Anfragen an diese Adresse ab und beantwortet sie selbst – es gibt **keinen** HTTP-Server und keinen offenen Port.
- Fensteraktionen (Ziehen, Ausblenden, Pin, Monitor) laufen über `window.chrome.webview.postMessage`.
- Endpunkte: `/api/status`, `/api/config`, `/api/layout` (GET/POST), `/api/action`, `/api/window`, `/api/redetect`, `/api/sys` (Monitor).

## Selbsterkennung (`Detector.cs`)

| Quelle | Wofür |
|---|---|
| `shell:AppsFolder` (Startmenü) | Claude, ComfyUI, Pinokio, Luminar AI, Open WebUI (App-IDs) |
| laufende Prozesse + Befehlszeile | NInfer/llama.cpp: Pfad, Port, Modell |
| Laufwerkssuche (Tiefe 2–3, ohne Systemordner) | `ninfer-serve.exe`, `llama-server.exe`, `*.ninfer` |
| Registry „Installierte Apps“ | Open WebUI |
| Konfigurationsdateien | Hermes `.env` (API-Port, Telegram), Open WebUI `config.json` (Port) |
| `PATH` | OpenCode, Windows Terminal |

Ergebnis: `%LOCALAPPDATA%\KI-Zentrale\config.json`. Das Backend liest nur von dort – keine festen Pfade im Code.

## Fenster

- `WS_EX_TOOLWINDOW` → kein Taskleisten-/Alt-Tab-Eintrag; DWM für runde Ecken und dunkle Ränder.
- Platzierung in physischen Pixeln mit `GetDpiForMonitor`; ein Timer prüft alle 4 s, ob ≥ 60 % des Fensters sichtbar sind.
