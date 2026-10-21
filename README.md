# MyJellyfinClient 🎬

Ein maßgeschneiderter, Offline-First Jellyfin Client, entwickelt mit .NET MAUI (Fokus auf Android).
Diese Applikation ist darauf ausgelegt, Medien nahtlos offline verfügbar zu machen, Wiedergabefortschritte intelligent zu synchronisieren und maximalen Komfort im Player zu bieten.

## 🌟 Kernfeatures

1. **Offline-First & Background Downloads**
   - Zuverlässige Hintergrund-Downloads über native Android `ForegroundService` API.
   - Keine speziellen Storage-Berechtigungen nötig (Nutzung von `GetExternalFilesDir`).
   - Auswahl der Videoqualität & Transcoding-Integration vor dem Download.

2. **Smart Connection Manager**
   - Intelligenter Fallback: Sucht primär den LAN-Server (z. B. lokales WLAN), bei Timeout automatisch den VPN-Server, ansonsten Fallback in den Offline-Modus.

3. **Custom Video Player (LibVLCSharp)**
   - Hardwarebeschleunigte VLC-Engine für maximale Formatunterstützung.
   - Gestensteuerung: Linke Bildschirmhälfte für Helligkeit, rechte Hälfte für Lautstärke, horizontal für Spulen.
   - Tiefgreifendes Android Brightness-Override (überschreibt die Systemhelligkeit nur für die Dauer der Wiedergabe).
   - Automatisches Offline-Tracking des Fortschritts in 5-Sekunden-Intervallen.

4. **Smart Automations**
   - **Auto-Cleanup**: Episoden, die zu >= 90% gesehen wurden, werden automatisch vom Gerätespeicher gelöscht, um Platz freizugeben.
   - **Auto-Download**: Erkennt am Ende einer Episode automatisch die nächste Folge über die Jellyfin `NextUp`-API und startet den Download (WLAN-kompatibel).

5. **Bi-Direktionale Sync Engine**
   - Speichert Wiedergabezeiten (Ticks) in einer lokalen SQLite-Datenbank.
   - Sobald wieder Internet/WLAN verfügbar ist, gleicht der `SyncEngine`-Dienst die Zeiten und "Gesehen"-Markierungen automatisch mit dem Jellyfin-Server ab.

## 📦 Benötigte NuGet-Pakete
Stelle sicher, dass folgende Pakete im Projekt installiert sind:
- `sqlite-net-pcl` & `sqlite-net-bcl`
- `CommunityToolkit.Mvvm`
- `Refit` & `Refit.HttpClientFactory`
- `Refit.Reflection` (Wichtig für das verlässliche MAUI Code-Generation-Fallback)
- `LibVLCSharp.MAUI`
- `VideoLAN.LibVLC.Android` (Für das Android Build Target)
- `VideoLAN.LibVLC.Windows` (Falls auf Windows getestet wird)

## ⚙️ Architektur & Struktur
- **Models**: SQLite Entitäten (`LocalMediaItem`) & API DTOs.
- **Services**: `SyncEngine`, `StorageManager`, `AutoDownloadManager`, `ServerConnectionService`, `LocalDatabaseService`.
- **Api**: `IJellyfinApiService` (Refit) & `DynamicBaseUrlHandler`.
- **ViewModels**: MVVM Pattern zur sauberen Trennung von UI und Logik.
- **Platforms/Android**: Plattformspezifische Implementierungen (Foreground Download Service, Brightness Override).

## 🚀 Erste Schritte
1. Projekt kompilieren und starten (auf Emulator oder physischem Android Gerät).
2. Im Tab **Settings** folgende Daten eintragen:
   - **Jellyfin Server URL** (z.B. http://192.168.178.10:8096)
   - **Backup Server URL** (z.B. https://jellyfin.dein-vpn.de)
   - **User ID** (Aus Jellyfin kopieren)
   - **Auth Token** (In Jellyfin einen API-Schlüssel für diesen Client generieren)
3. Speichern klicken. Die Bibliothek lädt nun automatisch unter Berücksichtigung des Connection Managers.

***

## Entwickler-Dokumentation: MyJellyfinClient 🛠️

Diese Dokumentation fasst alle architektonischen Entscheidungen, technischen Hintergründe und Best Practices der App-Entwicklung zusammen, aktualisiert auf den finalen Projektstand.

### Datenbank & Modelle (Offline-Cache)
* **Lazy Initialization:** Die SQLite-Datenbank (`InitAsync()`) wird erst initialisiert, wenn der erste Query abgesetzt wird. Das hält den App-Start schnell und blockiert nicht den UI-Thread.
* **Multi-User & Storage Integration:** Das `LocalMediaItem` besitzt Properties wie `ProfileId` (für Profil-Switching) und `FileSizeBytes`. Letzteres ermöglicht den "Storage Usage Analyzer" komplett ohne teure I/O-Leseoperationen im Dateisystem.
* **Sync-Flagging:** Bei der Offline-Wiedergabe wird `NeedsSyncToServer = true` gesetzt. Der `SyncEngine`-Hintergrunddienst fragt später nur `GetItemsPendingSyncAsync()` ab und pusht diese per REST-API.

### Networking & Sync-Engine
* **Refit API-Client:** Anstatt `HttpClient` händisch zu bauen, generiert Refit die Implementierung zur Laufzeit anhand von Interfaces. Das spart Boilerplate-Code und macht die API wartbar.
* **Event-Driven Sync (`ConnectivityChanged`):** Wir nutzen das native `Connectivity`-API. Sobald das Gerät WLAN/Mobilfunk hat, feuert das Event. Es ist kein Polling (Timer) nötig, was den Akku schont.
* **Fehlertoleranz (Semaphore):** Die Queue fängt Netzwerkfehler pro Item einzeln in einer `try-catch`-Schleife ab. Das `NeedsSyncToServer`-Flag bleibt bei Fehlschlägen erhalten und der Sync der restlichen Items läuft weiter.

### Nativer Android Download Service
* **Foreground Service (`TypeDataSync`):** Android beendet Hintergrund-Aktivitäten gnadenlos (Doze Mode). Ein Foreground Service verhindert dies, erfordert aber eine sichtbare Notification und ab Android 14 die genaue Typ-Deklaration.
* **Scoped Storage:** Durch `GetExternalFilesDir()` speichern wir im geschützten App-Verzeichnis. Vorteile: Keine System-Speicherberechtigungen nötig und Auto-Löschung bei App-Deinstallation.
* **Streaming HTTP Client:** Durch `HttpCompletionOption.ResponseHeadersRead` lesen wir große Videodateien in 8-Kilobyte-Häppchen und verhindern eine `OutOfMemoryException`.
* **Notification Throttling:** UI-Updates der Benachrichtigung passieren nur bei jedem vollen Prozentpunkt, um ein Einfrieren des Android-Systems zu verhindern.

### Custom Video Player & Gesten (LibVLCSharp)
* **Jellyfin Ticks vs. Millisekunden:** Jellyfin nutzt .NET Ticks (1 Tick = 100 Nanosekunden), VLC arbeitet mit Millisekunden. Die Umrechnung (`Ticks = Milliseconds * 10000`) ist für den sauberen Server-Sync unerlässlich.
* **Android Brightness Override:** Wir modifizieren `Window.Attributes.ScreenBrightness` der aktuellen Activity. Dadurch verändern wir die Helligkeit *nur für unsere App*, ohne globale Systemberechtigungen zu fordern.
* **Gesture Splits:** Da MAUI Pan-Gesten nur das Delta liefern, ist das Overlay in zwei `ContentViews` geteilt (Links = Helligkeit, Rechts = Lautstärke), um die Wisch-Achsen eindeutig zuzuordnen.
* **Lifecycle (`Dispose`):** Die Freigabe des Players in `OnDisappearing` ist zwingend erforderlich, da VLC native C++-Ressourcen nutzt (verhindert Memory Leaks und weiterlaufenden Ton).

### Automatisierung (Auto-Cleanup & Downloads)
* **Threshold für "Gesehen":** Ein Video gilt bei >= 90% als gesehen, um die Automatisierung vor dem Abspann zu triggern.
* **Verhinderung von Datei-Sperren (`FileInUse`):** VLC hält einen Lock auf die `.mp4` Datei. Ein `Task.Delay(5000)` als Fire-and-Forget-Task beim Cleanup verschafft dem Player Pufferzeit, bevor `File.Delete()` gerufen wird, um eine `IOException` zu vermeiden.
* **NextUp API-Logik:** Die `/Shows/NextUp`-API berechnet serverseitig die exakte nächste Episode für den spezifischen Benutzer. Ein lokaler Check (`localItem.IsDownloaded`) verhindert unnötigen Traffic.

### UI, UX & MVVM-Architektur
* **Globale Styles (`App.xaml`):** Farben (`#10141D`, `#AA5CC3`), Buttons und Pages sind zentral deklariert. Das reduziert XAML-Code und ermöglicht ein konsistentes Jellyfin-Design (Material Theme).
* **AppShell Routing:** Die `AppShell.xaml` stellt die Bottom-Navigation-Bar bereit. Unterseiten wie `PlayerPage` werden unsichtbar im Code-Behind als Routen registriert und können Parameter per `[QueryProperty]` empfangen.
* **Transcoding bei Downloads:** Über Parameter wie `&VideoBitrate=` in der Download-URL zwingen wir den Jellyfin-Server zum Transcodieren (z. B. auf 720p/4Mbps), was Speicherplatz auf dem Smartphone spart.

### Security & Smart Connection Manager
* **Storage-Sicherheit:** Unkritische Daten (URL, UserID) liegen im schnellen `Preferences`. Das kritische `AuthToken` liegt im hardwareverschlüsselten `SecureStorage` (Android Keystore).
* **Smart Ping-Fallback:** Der `ServerConnectionService` pingt bei Bedarf mit 2 Sekunden Timeout den LAN-Server. Antwortet dieser nicht, wird nahtlos auf die Backup-URL (VPN) gewechselt.
* **DelegatingHandler Middleware:** Der `DynamicBaseUrlHandler` fängt jeden Refit-API-Request kurz vor dem Absenden ab, injiziert dynamisch die erreichbare Server-URL oder wirft eine explizite Exception (`OfflineMode`), wenn kein Server antwortet. Die UI fängt dies gracefully ab.
