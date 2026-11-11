# Sicherheit

## Unterstützte Versionen

Sicherheitskorrekturen gibt es für die jeweils neueste veröffentlichte Version.

## Sicherheitslücke melden

Bitte **keine öffentlichen Issues** für Sicherheitslücken. Nutze stattdessen die private Meldefunktion von GitHub: **Security → Report a vulnerability** in diesem Repository. Beschreibe, wie sich das Problem nachvollziehen lässt und welche Auswirkungen es hat. Du bekommst so schnell wie möglich eine Rückmeldung.

## Hinweise zum Design

- Die App lauscht auf keinem Netzwerk-Port; die Oberfläche kommuniziert nur prozessintern über WebView2.
- Installation pro Benutzer ohne Admin-Rechte; Schreibzugriffe nur in `%LOCALAPPDATA%` und `HKCU`.
- Veröffentlichte Setups werden reproduzierbar von GitHub Actions gebaut; jedes Release enthält eine SHA-256-Prüfsumme.
