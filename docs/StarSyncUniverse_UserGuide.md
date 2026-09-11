# StarSyncUniverse — Benutzerhandbuch und Projektstand

## Zweck
StarSyncUniverse ist die interaktive Karten-, Navigations- und Location-Anwendung für die aktuell geladenen Star-Citizen-Systeme. Die Anwendung kombiniert LIVE-Placement-Daten mit einer paketierten Community-Baseline und optionalen Enrichment-Quellen. Geometrie, Koordinaten und Routenautorität bleiben von reinen Präsentations- und Enrichment-Daten getrennt.

## Bedienung der Karte
- Mausrad: zum Cursor zoomen.
- Linke Maustaste ziehen: Karte verschieben.
- Mittlere Maustaste ziehen: Orbit/Tilt um den aktiven Fokus.
- Doppelklick auf ein Objekt: Objekt fokussieren.
- Rechtsklick: Navigations- und Bookmark-Menü öffnen.
- SYS / GLX: mit einem Klick zwischen Systemansicht und Galaxy-Topologie wechseln.

## Suche und Systemwahl
Die globale Suche in der oberen Leiste durchsucht die geladenen Galaxy-Objekte. Der Systemselektor rechts oben schaltet zwischen verfügbaren Systemen wie Stanton, Pyro und Nyx um.

## Navigation und Routen
Über das Rechtsklick-Menü können Start und Ziel gesetzt werden. Direct Line misst kompatible Punkte innerhalb eines Systems. Route nutzt die bekannten traversierbaren Systemverbindungen und Jump Points. Interstellare Übergänge werden als eigene Route-Legs behandelt und nicht als frei erfundene metrische Distanz interpretiert.

## Bookmarks
Bookmarks können auf Objekten, Surface Targets oder freien Cursorpositionen erstellt werden. Der Bookmark Manager unterstützt Gruppen, Kategorien, Sichtbarkeit, Mehrfachauswahl und SyncHost-ready Sharing mit stabilen IDs und Creator-Identität.

## Display und Body Targets
Die linke Display-Sektion steuert Sterne, Planeten, Stationen, Jump Points, Orbits, Asteroidenfelder, Labels und Regionsgrenzen. Für Planeten und Monde können body-lokale Ziele und Surface Locations angezeigt werden. Präsentationstexturen verändern weder kanonische Koordinaten noch Geometrie.

Direkt unter dem Zoom-Regler stehen zwei Beleuchtungsmodi zur Verfügung:

- `STELLAR LIGHTING`: gerichtete Tag-/Nachtbeleuchtung aus Richtung des Systemsterns.
- `SURVEY LIGHTING`: vollständige Oberflächenlesbarkeit für die Karteninspektion. Unter `Settings > Camera` kann zusätzlich der Survey-Nachtseitenfilter aktiviert werden. Er färbt die sternabgewandte Seite cyan/türkis ein und reduziert ihre Helligkeit progressiv, ohne die darunterliegende Oberflächenstruktur in Schwarz verschwinden zu lassen.

Der Maßstab und die Mess-/Routenanzeige verwenden Meter als interne Basis und zeigen geeignete metrische Einheiten zusammen mit Astronomischen Einheiten (AU) an. Die untere Map Legend ist eine reine transparente Overlay-Legende; Maßstab und Messbeschriftungen werden so positioniert, dass sie diese Bedienhinweise nicht überdecken.

# Externe Datenquellen

## StarBreaker + CURRENT LIVE Data.p4k
StarBreaker ist der lokale Adapter, mit dem StarSyncUniverse direkt auf die installierte Star-Citizen-`Data.p4k` zugreifen kann. StarBreaker wird nicht automatisch aus dem Internet geladen.

### Empfohlene Installation
1. Eine kompatible `starbreaker.exe` beschaffen.
2. `starbreaker.exe` direkt in das StarSyncUniverse-Programmverzeichnis oder in einen Unterordner des Programmverzeichnisses kopieren.
3. StarSyncUniverse starten und unter `Settings > Optional data-source adapters > Local Data.p4k / StarBreaker` prüfen, ob StarBreaker erkannt wurde.
4. Bei erfolgreicher Erkennung zeigt StarSyncUniverse `FOUND` und die aus der EXE gelesene Produkt-/Dateiversion an.
5. Wird keine `starbreaker.exe` gefunden, muss im Feld `StarBreaker path` der vollständige Pfad zur EXE angegeben werden.
6. Zusätzlich muss `Data.p4k path` auf die LIVE-Installation von Star Citizen zeigen, z. B. `...\StarCitizen\LIVE\Data.p4k`.

### Automatische Erkennung
Beim Start prüft StarSyncUniverse zuerst einen bereits konfigurierten gültigen Pfad. Ist dieser leer oder ungültig, wird im Programmverzeichnis nach `starbreaker.exe` gesucht. Geprüft werden der Programmordner selbst, `StarBreaker\starbreaker.exe` und anschließend weitere Unterordner des Programmverzeichnisses.

### Was passiert bei aktivem StarBreaker?
Ist `Enable local Data.p4k update adapter` aktiv, verwendet StarSyncUniverse StarBreaker, um die konfigurierte LIVE-`Data.p4k` auszulesen. Daraus werden die aktuellen Universe-Datasets, Placements, Objekte und weitere authoritative Strukturdaten aufgebaut. Präsentationstexturen und lokale Caches werden dabei aktualisiert, ohne die Trennung zwischen Geometrie, kanonischer Identität und reiner Präsentation aufzuheben.

### Verhalten nach einem Star-Citizen-Update
Nach einem Star-Citizen-Update bleibt die konfigurierte `Data.p4k` derselbe logische Datenursprung. Beim nächsten Reload wird die aktualisierte Datei erneut gelesen. Falls CIG das Datenformat ändert und die vorhandene StarBreaker-Version damit nicht mehr kompatibel ist, muss `starbreaker.exe` durch eine neuere kompatible Version ersetzt werden. Danach genügt ein erneuter Start bzw. `Save & reload data sources`.

## SCUnpacked-Daten
SCUnpacked dient in StarSyncUniverse als lokale semantische Enrichment-Quelle. Es ergänzt lesbare Namen, Beschreibungen, Factions, Services, Trade-/Location-Metadaten und verwandte semantische Informationen. Es ersetzt nicht die LIVE-Geometrie aus `Data.p4k` und ersetzt auch nicht die paketierten Body-Texturen.

### Empfohlene Installation
1. Einen aktuellen SCUnpacked-Datensatz beschaffen und entpacken.
2. Den Datensatz unterhalb des StarSyncUniverse-Programmverzeichnisses in den Ordner `database` kopieren.
3. Beispiel: `StarSyncUniverse\database\scunpacked-data-master\...`
4. Entscheidend ist, dass sich im verwendeten Datensatz `starmap_positions.json` befindet.
5. StarSyncUniverse sucht unter `StarSyncUniverse\database` rekursiv nach `starmap_positions.json` und verwendet automatisch dessen Ordner als SCUnpacked-Root.
6. Bei erfolgreicher Erkennung zeigt die Settings-Seite `FOUND` und eine Versions-/Snapshot-Angabe an. Wenn eine `version.txt`, `VERSION` oder `build.txt` vorhanden ist, wird deren Inhalt verwendet; andernfalls werden Ordnername und Snapshot-Datum von `starmap_positions.json` angezeigt.
7. Wird nichts gefunden, muss im Feld `SCUnpacked root` der vollständige Root-Pfad des Datensatzes angegeben werden.

### Was passiert bei aktivem SCUnpacked?
Ist `Enable local SCUnpacked enrichment` aktiv, wird die paketierte Community-Baseline mit dem angegebenen SCUnpacked-Datensatz aktualisiert bzw. erweitert. StarSyncUniverse korreliert die LIVE-/Snapshot-Objekte mit semantischen Location-Daten. Dadurch können z. B. lesbare Ortsnamen, Beschreibungen, Jurisdiction/Faction-Bezüge, Services, Trade-Profile und weitere Metadaten ergänzt werden.

### Verhalten nach einem Update
Nach einem neuen Star-Citizen-Build bzw. einem neuen SCUnpacked-Snapshot wird der alte SCUnpacked-Datensatz im `database`-Ordner ersetzt oder ein neuer Root-Pfad eingetragen. Danach `Save & reload data sources` ausführen. Die semantische Datenbasis wird aus dem neuen Snapshot neu aufgebaut. LIVE-Placement und Geometrie bleiben weiterhin von `Data.p4k` bzw. dem gewählten Universe-Snapshot autoritativ.

## Online Enrichment
Online Enrichment ist getrennt von StarBreaker und SCUnpacked. Es wird nur nach explizitem Consent aktiviert. Ohne Consent werden keine externen HTTP-Enrichment-Abfragen gesendet. Online-Daten dürfen lokale Beschreibungen/Medien ergänzen, nicht aber kanonische Geometrie oder Placement-Autorität ersetzen.

# SyncHost-Anbindung

## Zweck
Die SyncHost-Verbindung ist für synchronisierbare, benutzerbezogene StarSyncUniverse-Daten vorgesehen, insbesondere Bookmark-/Gruppen-Sharing und darauf aufbauende gemeinsame Daten. Die Verbindung nutzt dieselbe persistente Client-Identität und denselben signierten Request-Ansatz wie der StarSync-WPF-Client.

## Benötigte Informationen
Unter `Settings > StarSync Sync Host` werden folgende Angaben benötigt:

- `Enable SyncHost connection`: aktiviert die Verbindung.
- `SyncHost base URL`: vollständige Basis-URL des SyncHosts inklusive Schema, Host, Port und API-Basispfad, z. B. `https://host.example:18443/starsync-api/`.
- `Transport`: normalerweise `Auto`; alternativ explizit `REST / AS2-Lite`.
- `Player / handle`: Star-Citizen-Handle des Benutzers. Ist kein Handle gesetzt, wird technisch der Windows-Benutzername als Header-Fallback verwendet; für eine saubere Zuordnung sollte daher das echte Handle eingetragen werden.
- `Organization`: optionale Organisation/Corporation-Zuordnung.
- `Client ID`: wird lokal automatisch erzeugt und bleibt persistent.
- `Fingerprint`: SHA-256-Fingerprint des lokalen Client-Schlüssels.
- `Signature`: aktuell `ECDSA-P256-SHA256`.

## Lokale Client-Identität
Beim ersten Bedarf erzeugt StarSyncUniverse eine persistente Client-Identität. Der private Schlüssel wird lokal im Windows-Benutzerkontext DPAPI-geschützt gespeichert. Der private Schlüssel wird nicht an den SyncHost übertragen. Für Requests werden Client-ID, Fingerprint, Timestamp, Nonce, Modul, Operation und Request-Ziel in einen definierten Signing-String aufgenommen und mit ECDSA-P256/SHA-256 signiert.

## Verbindungsprozess
1. SyncHost in den Settings aktivieren.
2. Base URL eintragen.
3. Player/Handle und optional Organization eintragen.
4. `Save SyncHost settings` speichert die Konfiguration lokal.
5. `Probe connection` führt einen signierten Verbindungstest aus.
6. Zuerst wird `<base-url>/sync/sync-host-manifest.json` abgefragt.
7. Wenn das Manifest erreichbar ist, folgt ein signierter Request auf `<base-url>/api/v1/clients/me?clientId=<Client-ID>`.
8. Der SyncHost prüft dabei die übermittelten StarSync-Client-Header und die Request-Signatur.

## Ergebnis des Probe-Tests
Mögliche Zustände:

- `VALID/KNOWN`: Manifest erreichbar und Client im Registry-Endpunkt bekannt/autorisierbar.
- `NOT_AUTHORIZED`: Registry antwortet mit HTTP 401/403.
- `NOT_REGISTERED`: Registry antwortet mit HTTP 404.
- `UNAVAILABLE`: Registry ist erreichbar, antwortet aber mit einem anderen Fehlerstatus.
- `ERROR`: Transport-, TLS-, URL- oder sonstiger Verbindungsfehler.
- `OFF`: SyncHost deaktiviert oder keine Base URL konfiguriert.

## Was auf dem SyncHost vorbereitet sein muss
Der SyncHost muss über die konfigurierte Base URL erreichbar sein und mindestens das Manifest sowie den Client-Registry-Endpunkt bereitstellen. Der verwendete Client muss serverseitig bekannt bzw. gemäß der dortigen Client-/Handle-/Berechtigungslogik zugelassen werden. Bei Reverse Proxy oder TLS muss die konfigurierte öffentliche URL genau auf den tatsächlich erreichbaren SyncHost-Pfad zeigen.

## Typische Fehlerbilder
- Manifest nicht erreichbar: Base URL, Port, Reverse Proxy oder API-Pfad prüfen.
- 401/403: Client ist vorhanden, aber nicht autorisiert bzw. Signatur/Identity wird serverseitig nicht akzeptiert.
- 404 am Client-Registry-Endpunkt: Client-ID ist noch nicht registriert.
- TLS-/Zertifikatsfehler: Zertifikatskette, Hostname und Reverse-Proxy-Konfiguration prüfen.
- Falscher Handle/Organization-Wert: lokale Settings korrigieren und erneut speichern/proben.

## Sicherheit
Jeder Probe-Request erhält einen neuen UTC-Timestamp und eine neue Nonce. StarSyncUniverse sendet die Client-ID, den öffentlichen Fingerprint, Signatur-Metadaten, Handle/Organization und die Request-Signatur. Der private Schlüssel bleibt lokal geschützt.

# Integrity Check und Support Package
Unter `Settings > Integrity & Support` kann ein read-only Gesamttest gestartet werden. Der Test prüft die StarSyncUniverse-Kerndateien und die paketierte JSON-Baseline auf Vorhandensein, Lesbarkeit und strukturelle Gültigkeit. Für Binärdateien werden Versionsinformationen bzw. Assembly-Metadaten und SHA-256-Prüfsummen erfasst.

Für StarBreaker werden Installation und Version sowie die für StarSyncUniverse erforderlichen CLI-Fähigkeiten geprüft: `--help`, `p4k extract --help` und `dcb query --help`. Zusätzlich wird kontrolliert, ob die konfigurierte LIVE-`Data.p4k` vorhanden und lesbar ist. Die sehr große Data.p4k wird absichtlich nicht vollständig gehasht, damit der Supportcheck schnell und ohne unnötige I/O-Last bleibt.

Für SCUnpacked werden `starmap.json`, `starmap_positions.json`, `trade_locations.json`, `items.json`, `resources/commodities.json`, `resources/commodity_trade_locations.json` und der `factions`-Ordner geprüft. JSON-Dateien werden geparst; anschließend wird der komplette semantische SCUnpacked-Loader ausgeführt und die resultierenden Location-, Faction-, Item- und Trade-Zähler validiert.

Ist SyncHost konfiguriert, führt derselbe Check den signierten Manifest-/Registry-Probe aus. Das Ergebnis wird direkt in Settings nach Bereichen aufgeschlüsselt und zusätzlich als `.log` und `.zip` unter `%LOCALAPPDATA%\StarSyncUniverse\Support` gespeichert. Das Supportpaket enthält den Diagnosebericht, eine nicht-geheime Konfigurationsübersicht und vorhandene Renderer-Statusdateien. Private SyncHost-Schlüssel, Bookmark-Inhalte sowie Rohkopien von Data.p4k oder SCUnpacked werden ausdrücklich nicht aufgenommen.

# UI-Hinweise
Der SYS/GLX-Schalter ist zustandsabhängig: grün im SYS-Modus, blau im GLX-Modus. Das Rechtsklick-Menü verwendet eine kompakte holografische Darstellung mit Uhrzeit und Systemkoordinaten in der Fußzeile sowie einer kurzen Flicker-Out-Animation beim Schließen.

## About-Ansicht
Die About-Ansicht enthält die wichtigsten Bedienungs- und Konfigurationshinweise direkt in der Anwendung. Der Hintergrund ist schwarz, damit das rahmenlose StarSyncUniverse-Logo unten rechts unverfälscht dargestellt wird. Das Logo bleibt proportional und ist auf maximal 400 px Breite begrenzt.

## Disclaimer
Unofficial community software. Not affiliated with or endorsed by Cloud Imperium Games or Roberts Space Industries.
