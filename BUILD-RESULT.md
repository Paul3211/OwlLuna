# Build-Ergebnis vom 2. November 2026

**Eine echte unsignierte iPad-IPA wurde gebaut und heruntergeladen.**

- Repository: https://github.com/Paul3211/OwlLuna
- Upstream: https://github.com/owaisazmal/OwlLuna
- Upstream-Commit: `816d430c5f52a85a4115daa25e4a45d938fcb738`, MIT-Lizenz unverändert erhalten.
- Gebauter Commit: `9e40630845dc0344eb2b57d641b00d352ed65885`.
- Build: https://github.com/Paul3211/OwlLuna/actions/runs/41437087858 — erfolgreich, 4 Minuten 41 Sekunden.
- IPA-Artefakt: https://github.com/Paul3211/OwlLuna/actions/runs/41437087858/artifacts/13136957264
- Datei: `OwlLuna-unsigned.ipa`, 13.103.069 Bytes, Version 1.0 (Build 5).
- SHA-256: `e4872ee725056ec7298278361350e5fea9354736a92eef078a0b7808a7eaa67f`.
- Xcode 27.1 (27A9275), iOS SDK 27.1, Swift 6, Mindestversion iPadOS 18.0.
- Bundle-ID: `io.github.paul3211.OwlLuna`.

## Verifiziert

Auf macOS: ZIP-Integrität, richtige App und Bundle-ID, iPad-Unterstützung, ARM64-Gerätecode,
iOS-Plattform im Mach-O-Binary, keine Codesignatur, kein Provisioning-Profil und kein Widget.
Nach dem Download unabhängig unter Windows: SHA-256 stimmt überein, ZIP-CRC gültig,
ARM64-Mach-O mit iOS-Geräteplattform, unverschlüsselt und ohne LC_CODE_SIGNATURE.
Im Release-Archivprotokoll wurden keine Compilerwarnungen oder Compilerfehler gefunden.
Der erste Build war erfolgreich; Compilerreparaturen waren nicht erforderlich.

Die ursprüngliche Oberfläche und die Schreibfunktionen wurden nicht umgebaut.
Nur der SideStore-Build deaktiviert Widget-Einbettung, App Groups und iCloud.
Keine zusätzlichen KI-Funktionen oder kostenpflichtigen Dienste wurden eingebaut.
Das frühere Starter-ZIP war in diesem Chat/Arbeitsbereich nicht verfügbar; die Umsetzung basiert auf dem echten Repository.

## Tests

https://github.com/Paul3211/OwlLuna/actions/runs/41437405705 — erfolgreich.
Getesteter Commit: `efe34e329cff375a41be1433b32b65605d39160a`.
Er unterscheidet sich vom IPA-Commit nur durch Anleitung und Testworkflow, nicht durch App-Code.

51 ausgewählte Tests bestanden, 0 Fehlschläge:

- BackupTests: 20
- DocumentTests: 11
- DrawingInputTests: 6
- ManifestCodecTests: 6
- NotebookPackageTests: 8

Damit sind unter anderem Stifteingabelogik, Rückgängig/Wiederholen, Autosave,
Crash-Wiederherstellung, Schutz beschädigter Daten und Backup-Rückspielung geprüft.
Das ist kein vollständiger UI- oder Hardwaretest.
Die Tests liefen auf dem iPad-Simulator; die Pencil-Hardware und SideStore wurden nicht am realen Gerät getestet.
Der Xcode-Vorschau-Runner gab Laufzeitdiagnosen zur Simulator-Buildnummer aus; der Testprozess endete trotzdem erfolgreich.

## Noch offen

SideStore-Signierung, Installation, Erneuerung und Schreibgefühl müssen auf deinem aktuellen iPad geprüft werden.
Die Anleitung und eine kurze Geräte-Abnahme stehen in [SIDESTORE.md](SIDESTORE.md).
Windows-App, Dell Active Pen und gemeinsames bearbeitbares Strichformat sind noch nicht implementiert.
Die technische Ausgangslage dafür ist in der Anleitung dokumentiert.
Es wurden keine kostenpflichtigen Runner oder Dienste verwendet und keine Zahlungs-/Budgeteinstellungen geändert.

GitHub-Artefakte dieses Builds laufen am 9. November 2026 ab; lokale Kopien bleiben verfügbar.

