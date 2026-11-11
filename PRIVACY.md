# Datenschutz

Die KI-Zentrale arbeitet **vollständig lokal**.

- **Keine Telemetrie, keine Analyse, kein Konto.** Es werden keine Daten an den Entwickler oder Dritte gesendet.
- **Kein offener Netzwerk-Port.** Die Oberfläche läuft in WebView2 unter einer internen Adresse (`https://kiz.app/`), deren Anfragen die App selbst beantwortet – es gibt keinen Server, der von außen erreichbar ist.
- **Netzwerkzugriffe** erfolgen nur auf `127.0.0.1` (localhost), um den Status deiner lokalen Tools abzufragen (z. B. Modellserver, Hermes-API, Open WebUI).
- **Gespeichert** wird nur in `%LOCALAPPDATA%\KI-Zentrale\`:
  - `config.json` – erkannte Tools mit Pfaden und Ports
  - `layout.json` – Anordnung der Kacheln
  - `monitor.json` – Position und Pin des Systemmonitors
  - `WebView2\` – Arbeitsdaten der Oberfläche
- **Gelesen** werden zur Erkennung: Startmenü-Einträge, laufende Prozesse, Programmliste in der Registry, Konfigurationsdateien der unterstützten Tools (z. B. Port-Angaben). Geheimnisse wie API-Schlüssel oder Bot-Token werden nur auf Vorhandensein geprüft, nie gespeichert oder angezeigt.
- Die Deinstallation entfernt auf Wunsch auch alle gespeicherten Einstellungen.

Externe Links (z. B. zum WebView2-Download im Setup) öffnen sich nur, wenn du sie anklickst.
