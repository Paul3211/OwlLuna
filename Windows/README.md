# OwlLuna für Windows

Native Windows-App, .NET 10 / WPF, für Windows 10 ab Version 2004 und Windows 11, x64.
Die veröffentlichte Standalone-Version bringt die .NET-Laufzeit mit. ZIP vollständig entpacken und `OwlLuna.Windows.exe` starten.
Die App ist nicht mit einem kostenpflichtigen Herausgeberzertifikat signiert.

## Vorhandene Funktionen

- Hefte mit Titel, Umschlagfarbe, Ordnerpfad, Tags, Favoriten, Suche und Papierkorb/Wiederherstellung.
- Stifte mit Druck, Marker, Radierer, Lasso zum Verschieben und Skalieren, Auswahl über Seiten kopieren/einfügen.
- Rückgängig/Wiederholen, automatische lokale Speicherung, letzte gültige Sicherung und Schutz beschädigter Dateien.
- Mehrere Seiten, Duplizieren, Reihenfolge ändern, Seitennavigator, Lesezeichen, A4/A5/Letter und große Zeichenfläche.
- Kariertes, liniertes, punktiertes, leeres, Cornell- und Notenpapier; Papierfarbe.
- PDF importieren, beschriften und mit Original-PDF-Hintergrund wieder exportieren. PNG- und Markdown-Export, Drucken.
- Textfelder, Bilder, Drehung, Zuschnitt, Schriftgröße, fett/kursiv; Linien, Pfeile, Rechtecke, Dreiecke, Kreise.
- Lernband, Karteikarten mit Wiederholungsintervallen, Tageshefte und Präsentation der aktuellen Seite.
- WAV-Audio aufnehmen und abspielen. Aufnahme erst durch die entsprechende Menüaktion starten.
- Lokale Windows-Handschrifterkennung, PDF-/Bild-OCR und Offline-Audiotranskription, wenn die passenden Windows-Sprachpakete installiert sind.
- Zoom, Handwerkzeug, linker Seitenleistenmodus; dunkler Arbeitsbereich.
- Direkte Google-OAuth-Anmeldung aus der App; geprüfte Bibliotheksbackups hochladen und als Kopien zurückholen.

`Nur Stift` sperrt Fingerzeichnen. Das Handwerkzeug dient zum Verschieben der Ansicht.
Dell Active Pen verwendet die von Windows bereitgestellten Stiftdaten. Die tatsächliche Druckkurve und Handballenerkennung müssen am konkreten Dell/Stift geprüft werden.
Die Werkzeugdarstellung stammt aus der Windows-Ink-Engine; sie ist nicht pixelgleich mit Apples PencilKit.

## Google Drive

Die Anmeldung öffnet Googles offizielle Seite im Standardbrowser. Zugangsdaten nur dort eingeben.
OwlLuna fordert `drive.file` an: Zugriff auf Dateien, die diese App erstellt bzw. für die sie freigegeben wurde.
Backups landen im Ordner `OwlLuna Backups`; bestehende Dateien werden nicht automatisch gelöscht.
Die App speichert die Google-Tokens mit Windows DPAPI für den aktuellen Windows-Benutzer.
Ohne Anmeldung arbeitet die App lokal. Ein fehlgeschlagenes Cloud-Backup entfernt keine lokalen Hefte.
Automatische Cloud-Backups werden nach Änderungen frühestens alle 15 Minuten angestoßen, solange die App geöffnet und angemeldet ist.

Die persönliche Auslieferung kann eine an diesen Windows-Benutzer gebundene, verschlüsselte Client-Konfiguration enthalten.
Die generische öffentliche Quellcodeversion enthält keine Google-Clientwerte. Für einen eigenen Build ist eine eigene OAuth-Desktop-Registrierung erforderlich.
Ein Google-Projekt im Testmodus kann erneute Anmeldung nach sieben Tagen verlangen; die App stellt fehlgeschlagene Erneuerung als erneuten Anmeldebedarf dar.
Es werden keine kostenpflichtigen Cloud-Dienste oder größeren GitHub-Runner benötigt. Der verfügbare Speicher richtet sich nach deinem Google-Konto.

## Daten und Austausch

Standarddaten: `%LOCALAPPDATA%\OwlLunaWindows`. Hefte: `Library\<id>.owlbook`.
`.owlbook` ist ein versioniertes JSON-Format mit Seitengrößen in PDF-Punkten (72 je Zoll), Druckwerten und eingebetteten Medien.
Ein `.owlbackup` ist ein ZIP aus diesen Heften. Wiederherstellung legt neue Kopien an und überschreibt keine vorhandenen Hefte.
Vor größeren Imports oder Updates ein geprüftes Backup speichern.

Die bisherige iPad-IPA verwendet `.owlluna`-Pakete und PencilKit-Dateien. Sie liest `.owlbook` noch nicht.
Ein verlustfreier bidirektionaler iPad-Austausch ist daher noch nicht fertig. PDF bleibt vorerst das gemeinsame Lese-/Beschriftungsformat.
iCloud wurde auf ausdrücklichen Wunsch vorerst aus der Windows-Oberfläche weggelassen.

## Grenzen gegenüber OwlLuna auf dem iPad

Diese Windows-Umsetzung ist noch keine vollständige Funktionsparität. Noch offen sind insbesondere der native iPad-Formatadapter,
die automatische Formkorrektur und Pencil-Gesten, automatische OCR-Indexierung aller Inhalte, Live-Audio/Ink-Zeitleiste,
die vollständige Vorlagen-/Stickerbibliothek, echte Zwei-Heft-Ansicht und geräteübergreifende Synchronisierung.
Kamera-Scannen, direkter Systemkalender und biometrisch gesperrte Hefte sind noch nicht portiert. Heutige Termine lassen sich aus einer Kalenderdatei (.ics) übernehmen.
Keine generativen KI-Funktionen wurden ergänzt.

## Entwickeln und prüfen

```powershell
dotnet build Windows/OwlLuna.Windows.csproj -c Release
Windows/bin/Release/net10.0-windows10.0.19041.0/OwlLuna.Windows.exe --self-test C:\Temp\OwlLunaTests
dotnet publish Windows/OwlLuna.Windows.csproj -c Release -r win-x64 --self-contained true -o dist/OwlLuna-Windows
```

Der GitHub-Workflow `Build Windows desktop app` baut die Standalone-App auf einem kostenlosen öffentlichen Standard-Runner.
Die automatisierten Prüfungen laufen mit getrennten Testdaten; sie ersetzen keinen Hardwaretest mit dem Dell-Stift.
