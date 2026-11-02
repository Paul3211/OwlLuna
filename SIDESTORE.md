# Eigene OwlLuna-App für das iPad

Dieser Fork basiert auf owaisazmal/OwlLuna, Stand `816d430c5f52a85a4115daa25e4a45d938fcb738` (1.0, Build 5).
Die MIT-Lizenz und die mitgelieferten Schriftlizenzen bleiben enthalten.

## Bauen und herunterladen

Unter **Actions → Build OwlLuna IPA (unsigned) → Run workflow** den Branch `main` oder `sidestore` auswählen.
Der Workflow verwendet den Standard-Runner `xcode-27` und läuft nur in einem öffentlichen Repository.
Keine Zertifikate, Passwörter, Secrets, Apple-Mitgliedschaft oder kostenpflichtigen Dienste sind nötig.
Der Runner ist derzeit eine öffentliche Vorschau; seine Xcode-/SDK-Version wird bei jedem Build protokolliert.
Das Projekt braucht Xcode 27 / Swift 6, die App mindestens iPadOS 18.
Es gibt keine Swift-Package-, CocoaPods- oder Carthage-Abhängigkeiten für die native App.

Nach erfolgreichem Abschluss das Artefakt **OwlLuna-unsigned-N** herunterladen und das äußere ZIP entpacken.
Die darin enthaltene **OwlLuna-unsigned.ipa** ist für SideStore gedacht. Die IPA selbst nicht entpacken.
Daneben stehen eine SHA-256-Prüfsumme und ein maschinenlesbarer Prüfbericht.
Artefakte werden sieben Tage aufbewahrt; die heruntergeladene IPA lokal sichern oder später neu bauen.
Ein grüner Build belegt die Kompilierung und Paketprüfung, noch keinen Test auf deinem realen iPad.

## Kostenlose Installation mit SideStore

Die aktuelle offizielle Anleitung verwenden:
https://docs.sidestore.io/docs/installation/prerequisites
https://docs.sidestore.io/docs/installation/install

SideStore einmalig mit dem Windows-PC einrichten. Anmeldedaten ausschließlich selbst bei Apple/SideStore eingeben.
Danach die IPA auf das iPad übertragen und in SideStore unter **My Apps → +** auswählen.
Die von SideStore benötigte lokale VPN-Verbindung beim Installieren und Erneuern aktivieren.
Bei kostenlosen Apple-Konten gelten Apples Laufzeit- und App-Limits; rechtzeitig erneuern.
Ein kostenloser Account hebt die regelmäßige Erneuerung nicht auf.
Die Installation und das Erneuern auf der jeweils neuesten iPadOS-Version müssen am Gerät geprüft werden.
Vor Updates die Notizbibliothek über OwlLunas Backup-Funktion exportieren.
Für Updates denselben Apple-Account und dieselbe von SideStore verwendete App-ID beibehalten.

## Was geändert wurde

- Eigene Bundle-ID `io.github.paul3211.OwlLuna`.
- Nur für diesen Build: Widget-Abhängigkeit und Widget-Einbettung aus dem App-Target lösen. Der Quellcode bleibt erhalten.
- Keine App-Group-, iCloud- oder Entwickler-Entitlements; keine fremde Team-ID.
- `OWLLUNA_LOCAL_ONLY` verhindert den Zugriff auf iCloud und den Widget-Container.
- Die bestehende Oberfläche, PencilKit, PDFs, Hefte, Ordner, Speichern, Export und Backups bleiben erhalten.
- Keine zusätzlichen KI-Funktionen oder externen Dienste. Bereits im Original vorhandene Funktionen bleiben Originalcode.
- Fremde CI-, Release- und Website-Workflows wurden aus dem Fork entfernt.

Die Bibliothek liegt weiterhin im privaten App-Speicher. Das Entfernen eines Widgets entfernt keine Hefte.
Die IPA-Prüfung kontrolliert ZIP-Integrität, Bundle-ID, iPad-Unterstützung, Geräteplattform, ARM64 und entfernte Signaturen.
Die separaten Tests unter **Test SideStore writing and storage** prüfen Schreiben, Rückgängig/Wiederholen,
Autosave, beschädigte Daten und Backup-Wiederherstellung im iPad-Simulator.

## Abnahme auf dem iPad

Ein Heft anlegen; mit Pencil schreiben; Radierer, Lasso, Rückgängig und Wiederholen ausprobieren.
Ein mehrseitiges PDF importieren und beschriften. App beenden und neu öffnen; alle Inhalte müssen wieder erscheinen.
PDF exportieren, Bibliotheksbackup erstellen und eine Kopie wiederherstellen.
Flugmodus, Hoch-/Querformat und ein Update derselben App testen. Erst dann wichtige Schulhefte ausschließlich darin führen.

## Windows später

Die vorhandenen `.owlluna`-Pakete enthalten `manifest.json`, Assets und pro Seite `.pkdrawing`-Daten.
Der Manifest-Codec bewahrt unbekannte Felder; SwiftData ist nur ein wiederaufbaubarer Index.
PencilKit-Handschrift ist kein gemeinsam bearbeitbares Windows-Format.
Für Windows ist später ein versioniertes Strichformat mit Punkten, Druck, Neigung, Zeit, Werkzeug, Farbe und Seitentransformationen nötig.
Die iPad-App muss Striche verlustarm in beide Richtungen übertragen können; Originaldaten bleiben erhalten.
PDF ist bis dahin ein Austauschformat zum Lesen/Annotieren, kein verlustfreier Handschrift-Roundtrip.
Dell Active Pen erfordert Windows-Pen-Eingaben samt Druck und Handballenerkennung sowie Tests am tatsächlichen Dell-Modell.
Windows-App und gemeinsame Formatkonvertierung sind noch nicht implementiert; zuerst wird die iPad-Version abgenommen.
