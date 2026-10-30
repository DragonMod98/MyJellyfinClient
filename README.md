# MyJellyfinClient

Ein maßgeschneiderter, Offline-First Jellyfin Client, entwickelt mit .NET MAUI (Fokus auf Android).
Medien werden offline verfügbar gemacht, der Wiedergabefortschritt wird mit Jellyfin synchronisiert, und der Player bietet Gesten und Bild-im-Bild.

## Kernfeatures

### Bibliothek
- Kachelansicht mit Ordnernavigation (Bibliothek, Serie, Staffel) und Zurück-Button bzw. Zurück-Geste.
- **Gesehen-Status aus Jellyfin:** grüner Haken (gesehen), lila Zähler (ungesehene Folgen bei Serie/Staffel), oranger Balken (angefangen).
- **Alle herunterladen:** lädt alle Folgen einer Staffel nacheinander.
- Beim Start ohne Serververbindung wechselt die App automatisch zu den Downloads.

### Downloads
- Zuverlässige Hintergrund-Downloads über einen nativen Android `ForegroundService`, ohne Speicherberechtigung (`GetExternalFilesDir`).
- **Wählbar pro Download:** Qualität (Original, 1080p, 720p), Tonspur und Untertitel.
- **Download-Vorgaben** in den Einstellungen: Passen sie zum Medium, wird direkt so geladen, sonst fragt die App nach.
- Mehrere Downloads laufen nacheinander. Aktive Downloads erscheinen mit Fortschritt auf der Downloads-Seite und lassen sich einzeln oder alle abbrechen (auch über die Benachrichtigung).
- Downloads-Seite: nach Serie gruppiert (einklappbar), Fortschrittsbalken pro Eintrag, Löschen per Wischen (Folge, Staffel, Serie).

### Detailseite
Kompakte Info-Chips: Laufzeit, Qualität des Originals (Auflösung, Codec, HDR), Dateigröße, Container, Tonspuren, Untertitel, Jahr, Bewertung, FSK. Dazu Genres und Beschreibung.

### Player (LibVLCSharp)
- Vollbild ohne Statusleiste, Fortsetzen an der gespeicherten Position.
- **Gesten:** links vertikal wischen = Helligkeit, rechts vertikal wischen = Systemlautstärke. Doppeltippen rechts = 10 s vor, links = 5 s zurück. Ein Tipp blendet die Controls ein.
- **Bild-im-Bild:** Beim Verlassen der App läuft das Video in einem kleinen Fenster weiter (Android 8+).
- Fortschritt wird alle 5 Sekunden lokal gespeichert.
- **Ton und Untertitel:** Ein Tipp auf den Bildschirm blendet oben rechts „Ton & Untertitel“ ein. Die Auswahl läuft über eigene Dialoge, weil die eingebaute Spurliste des VLC-Controls im Dunkeldesign unleserlich war.
- **Player-Auswahl** (Einstellungen, Block „Wiedergabe“): integrierter VLC-Player, Standard-App des Geräts oder eine bestimmte installierte Player-App. Die Wahl wird gespeichert und nicht bei jeder Wiedergabe abgefragt. Bei externen Apps werden Fortschritt und Gesehen-Status nicht erfasst.
- Die Wisch-Gesten für Helligkeit und Lautstärke haben links und rechts einen Randabstand, damit die Android-Zurück-Geste nicht gestört wird.
- Bleibt das Bild schwarz (Ton läuft, aber keine Videoausgabe), startet der Player die Wiedergabe automatisch neu (bis zu dreimal).

### Automatisierungen
- **Auto-Cleanup:** Titel ab 90 % gesehen werden vom Gerät gelöscht.
- **Auto-Download der nächsten Folge** (in den Einstellungen abschaltbar): Nach dem Schauen und erfolgreichem Sync wird pro gesehener Folge eine weitere geladen, auch über Staffelgrenzen hinweg. Es werden Vorgaben für Qualität, Ton und Untertitel angewendet.

### Sync mit Jellyfin
Gesehen-Status und Position werden an Jellyfin gemeldet, auch für offline geschaute oder bereits gelöschte Titel. Der Sync läuft beim App-Start, beim Schließen des Players, beim Laden der Bibliothek und sobald wieder eine Verbindung besteht. Die Richtung ist Gerät zu Server.

### Verbindung
Der `ServerConnectionService` testet erst die primäre URL (LAN), dann die Backup-URL (z. B. VPN), sonst gilt die App als offline.

## Benötigte NuGet-Pakete
- `sqlite-net-pcl`
- `CommunityToolkit.Mvvm`
- `Refit`, `Refit.HttpClientFactory`, `Refit.Reflection`
- `LibVLCSharp.MAUI`
- `VideoLAN.LibVLC.Android` (Android-Target), `VideoLAN.LibVLC.Windows` (nur falls auf Windows getestet wird)

## Projektstruktur
| Ordner | Inhalt |
|---|---|
| `Models` | SQLite-Entität `LocalMediaItem`, `DownloadGroup` für die gruppierte Download-Liste |
| `Services` | `SyncEngine`, `AutoDownloadManager`, `DownloadStarter`, `DownloadTracker`, `DownloadOptions` (Stream-Parser und Vorgaben), `StorageManager`, `LocalDatabaseService`, `ServerConnectionService`, `SettingsService`, `PipState`, `ExitConfirmation` |
| `Services/Api` | `IJellyfinApiService` (Refit), API-Modelle, `DynamicBaseUrlHandler` |
| `ViewModels` | MVVM mit CommunityToolkit (`Library`, `MediaDetail`, `Downloads`, `Settings`) |
| `Views` | XAML-Seiten und Code-Behind |
| `Platforms/Android` | `ForegroundDownloadService`, `AndroidDownloadManager`, `AndroidBrightnessService` (Helligkeit, Lautstärke, Vollbild), `MainActivity` (Bild-im-Bild) |

## Erste Schritte
1. Projekt starten (Emulator oder Android-Gerät).
2. Im Tab **Settings** eintragen: Server-URL, optional Backup-URL, User-ID und Auth-Token (API-Schlüssel aus Jellyfin).
3. Optional die Download-Vorgaben und den Auto-Download-Schalter setzen, dann speichern. Die Bibliothek lädt danach automatisch.

---

# Entwickler-Dokumentation

## Datenbank & Modelle
- **Lazy Initialization:** `LocalDatabaseService.InitAsync()` läuft erst beim ersten Zugriff, das hält den Start schnell.
- **`LocalMediaItem`:** enthält `ProfileId` (Multi-User), Serien-Metadaten (`SeriesName`, `SeasonName`, `SeriesId`), `FileSizeBytes`, `ResumePositionTicks`, `TotalTicks` und das Sync-Flag `NeedsSyncToServer`. Der Fortschritt (`Progress`) wird berechnet und nicht gespeichert.
- **Löschen und Sync:** Ist der Gesehen-Status noch nicht gemeldet, bleibt der Eintrag ohne Datei in der DB. Der Sync meldet ihn und löscht ihn danach endgültig.

## Networking & Sync
- **Refit** generiert den API-Client aus `IJellyfinApiService`. Der `DynamicBaseUrlHandler` setzt pro Request die erreichbare Server-URL oder wirft `OfflineMode`.
- **Wiedergabe-Fortschritt:** Teilweise Gesehenes wird über `/Sessions/Playing/Stopped` gemeldet (funktioniert ohne laufende Session), Vollständiges über `PlayedItems`.
- **Fehlertoleranz:** Fehler werden pro Item abgefangen. Das Sync-Flag bleibt dann gesetzt, der nächste Durchlauf versucht es erneut.
- **Auto-Download:** Erst nach bestätigtem Sync. `GetEpisodesAsync` liefert alle Folgen in Abspielreihenfolge, daraus werden pro gesehener Folge die nächsten nicht geladenen und nicht gesehenen Folgen gewählt.

## Download-Service
- **Foreground Service (`TypeDataSync`)** mit Benachrichtigung. Ab Android 13 ist die Laufzeit-Berechtigung `POST_NOTIFICATIONS` nötig.
- **Warteschlange:** Ein statisches `SemaphoreSlim` serialisiert die Downloads. Jeder hat ein eigenes `CancellationTokenSource`, auch wartende.
- **Streaming:** `HttpCompletionOption.ResponseHeadersRead` liest in 8-KB-Blöcken und vermeidet `OutOfMemoryException`.
- **Original-Download:** `/Items/{id}/Download`. Der Endpunkt ignoriert Transcoding-Parameter.
- **Eingeschränkte Spuren oder Qualität:** `/Videos/{id}/stream.mkv` mit `AudioStreamIndex`, `SubtitleStreamIndex` und Bitrate. Der Server liefert dann keine Dateigröße, die Anzeige nutzt eine Schätzung (Laufzeit mal Bitrate bzw. Originalgröße) und bleibt unter 99 %.
- **Abbruch und Fehler:** Teildatei und vorläufiger DB-Eintrag werden entfernt.
- **`DownloadTracker`:** gemeinsamer Zustand (UI-Thread) für die Anzeige aktiver Downloads.

## Player
- **Ticks:** Jellyfin nutzt 10.000.000 Ticks pro Sekunde, VLC Millisekunden (`Ticks = ms * 10000`).
- **Fortsetzen:** Die Startposition wird als Option `:start-time` an das Medium gehängt, weil VLC `Time` vor dem Abspielen ignoriert.
- **Aufräumen (`OnDisappearing`):** Reihenfolge ist wichtig. Zuerst Timer stoppen und Fortschritt speichern, dann `VideoPlayer.MediaPlayer = null`, dann `Stop` und `Dispose` auf einem Hintergrund-Task. Sonst droht ein nativer VLC-Absturz.
- **Gesten:** Werte werden immer als Startwert plus Wischweg berechnet, nicht aufaddiert. `VerticalRange` in `PlayerPage.xaml.cs` steuert die Empfindlichkeit.
- **Lautstärke und Helligkeit:** `AndroidBrightnessService` setzt die Medien-Systemlautstärke und die Fensterhelligkeit der Activity. Letztere wird beim Verlassen zurückgesetzt.
- **Bild-im-Bild:** `MainActivity` startet PiP in `OnUserLeaveHint`. Über `PipState` blendet die Seite im Fenster Gesten und Controls aus. Wird das Fenster geschlossen, beendet sie die Wiedergabe.

## UI & Navigation
- **AppShell:** Tabs Bibliothek, Downloads, Settings. `MediaDetailPage` und `PlayerPage` sind Routen und erhalten Parameter per `[QueryProperty]` bzw. `IQueryAttributable`.
- **Zurück-Geste:** `OnBackButtonPressed` navigiert zurück. Auf den Hauptseiten fragt `ExitConfirmation` vor dem Beenden nach.
- **DI:** Seiten werden vom Container erzeugt. Navigationsparameter dürfen daher nicht im Konstruktor stehen.

## Sicherheit
- URLs und Download-Vorgaben liegen in `Preferences`, das Auth-Token im `SecureStorage` (Android Keystore).
- Der Download nutzt das Token als `api_key` in der URL. Sie ist nicht für Logs oder Weitergabe gedacht.

## Bekannte Einschränkungen
- Der Sync geht nur vom Gerät zum Server, es gibt keinen Konfliktabgleich bei mehreren Geräten.
- Mit eingeschränkter Ton- oder Untertitelspur ist genau eine Spur je Art möglich.
- „Alle herunterladen“ wirkt nur in Ordnern mit Videos (Staffel), nicht über mehrere Staffeln.
- Bild-im-Bild erfordert Android 8 oder neuer und kann herstellerseitig deaktiviert sein.
