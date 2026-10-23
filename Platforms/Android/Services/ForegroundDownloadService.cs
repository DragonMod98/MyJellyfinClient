using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using MyJellyfinClient.Services; // Für LocalDatabaseService
using Environment = Android.OS.Environment;

namespace MyJellyfinClient.Platforms.Android.Services;

[Service(ForegroundServiceType = ForegroundService.TypeDataSync)]
public class ForegroundDownloadService : Service
{
    private const string ChannelId = "JellyfinDownloadChannel";
    private const string ActionCancelAll = "com.companyname.myjellyfinclient.CANCEL_ALL";
    private const int NotificationId = 1001;       // Laufende (Foreground-)Benachrichtigung
    private const int ResultNotificationBase = 2000; // Fertig/Fehler-Meldungen (+ startId)
    private NotificationManager _notificationManager;

    // Downloads (z.B. eine ganze Staffel) werden nacheinander abgearbeitet
    private static readonly SemaphoreSlim DownloadLock = new(1, 1);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, CancellationTokenSource> Running = new();
    private static int _pending;
    private static int _batchCompleted;
    private static int _latestStartId;

    public static void Cancel(string itemId)
    {
        if (Running.TryGetValue(itemId, out var cts))
            cts.Cancel();
    }

    public static void CancelAll()
    {
        foreach (var cts in Running.Values)
            cts.Cancel();
    }

    public override IBinder OnBind(Intent intent) => null; // Wird nicht benötigt

    public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
    {
        // Immer die neueste startId merken, sonst kann StopSelfResult den Service nicht beenden
        _latestStartId = startId;

        // "Abbrechen" aus der Benachrichtigung
        if (intent?.Action == ActionCancelAll)
        {
            CancelAll();
            return StartCommandResult.NotSticky;
        }

        string itemId = intent.GetStringExtra("itemId");
        string url = intent.GetStringExtra("url");
        string title = intent.GetStringExtra("title");

        CreateNotificationChannel();
        StartForeground(NotificationId, CreateNotification(title, 0));

        Interlocked.Increment(ref _pending);

        // Eigenes Abbruch-Token pro Download (auch fuer noch wartende Downloads)
        var cts = new CancellationTokenSource();
        Running[itemId] = cts;

        // Download im Hintergrund-Thread starten (wartet, bis vorherige Downloads fertig sind)
        long estimatedBytes = intent.GetLongExtra("estimatedBytes", 0);
        Task.Run(() => DownloadFileAsync(itemId, url, title, startId, estimatedBytes, cts));

        return StartCommandResult.Sticky;
    }

    private async Task DownloadFileAsync(string itemId, string url, string title, int startId, long estimatedBytes, CancellationTokenSource cts)
    {
        var token = cts.Token;
        bool acquired = false;
        bool success = false;
        bool cancelled = false;
        string error = null;
        string filePath = null;

        try
        {
            await DownloadLock.WaitAsync(token);
            acquired = true;

            UpdateNotification(title, 0);
            DownloadTracker.Report(itemId, 0);

            using var httpClient = new HttpClient();
            using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            // Direkt-Downloads liefern die Laenge, Server-Streams (Ton/Untertitel/Transcode) nicht:
            // dann die vorab geschaetzte Groesse nutzen, sonst unbestimmten Fortschritt zeigen.
            var totalBytes = response.Content.Headers.ContentLength ?? (estimatedBytes > 0 ? estimatedBytes : -1L);
            bool isEstimate = response.Content.Headers.ContentLength == null;
            if (totalBytes == -1)
            {
                UpdateNotification(title, -1);
                DownloadTracker.Report(itemId, -1);
            }

            // Datei im App-spezifischen Verzeichnis speichern (keine Storage-Permissions nötig!)
            var externalPath = GetExternalFilesDir(Environment.DirectoryMovies)?.AbsolutePath;
            var fileName = $"{itemId}.mp4"; // Oder mkv, je nach Manifest
            filePath = Path.Combine(externalPath, fileName);

            long totalRead = 0;
            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
            using (var contentStream = await response.Content.ReadAsStreamAsync(token))
            {
                var buffer = new byte[8192];
                int read;
                int lastPercentage = 0;

                while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, read, token);
                    totalRead += read;

                    if (totalBytes != -1)
                    {
                        var percentage = (int)((totalRead * 100) / totalBytes);
                        if (isEstimate) percentage = Math.Min(percentage, 99); // Schaetzung kann ueberschritten werden
                        // Update nur jeden vollen Prozentpunkt, um UI-Spam zu vermeiden
                        if (percentage > lastPercentage)
                        {
                            lastPercentage = percentage;
                            if (percentage % 10 == 0)
                                System.Diagnostics.Debug.WriteLine($"Download {title}: {percentage}%");
                            UpdateNotification(title, percentage);
                            DownloadTracker.Report(itemId, percentage / 100.0);
                        }
                    }
                }
            }

            // Download beendet -> Datenbank aktualisieren
            var dbService = IPlatformApplication.Current.Services.GetService<LocalDatabaseService>();
            if (dbService != null)
            {
                var item = await dbService.GetMediaItemAsync(itemId);
                if (item != null)
                {
                    item.IsDownloaded = true;
                    item.LocalFilePath = filePath;
                    item.FileSizeBytes = totalRead;
                    await dbService.SaveOrUpdateMediaItemAsync(item);
                }
            }

            // Benachrichtigung "Fertig" (eigene ID, damit sie nach dem Beenden des Service bleibt)
            _notificationManager.Notify(ResultNotificationBase + startId, new NotificationCompat.Builder(this, ChannelId)
                .SetContentTitle($"{title} heruntergeladen")
                .SetSmallIcon(Resource.Drawable.navigation_empty_icon) // TODO: Eigenes Jellyfin-Icon einfügen
                .SetAutoCancel(true)
                .Build());

            success = true;
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Download Fehler: {ex.Message}");
            error = ex.Message;
            _notificationManager?.Notify(ResultNotificationBase + startId, new NotificationCompat.Builder(this, ChannelId)
                .SetContentTitle($"Download fehlgeschlagen: {title}")
                .SetContentText(ex.Message)
                .SetSmallIcon(Resource.Drawable.navigation_empty_icon)
                .SetAutoCancel(true)
                .Build());
        }
        finally
        {
            if (acquired) DownloadLock.Release();
            Running.TryRemove(itemId, out _);

            // Abgebrochen oder fehlgeschlagen: unvollstaendige Datei und Platzhalter-Eintrag entfernen
            if (cancelled || error != null)
            {
                try
                {
                    if (filePath != null && File.Exists(filePath)) File.Delete(filePath);
                    var db = IPlatformApplication.Current?.Services.GetService<LocalDatabaseService>();
                    if (db != null) await db.DeleteMediaItemAsync(itemId);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Aufraeumen nach Abbruch fehlgeschlagen: {ex.Message}");
                }
            }

            DownloadTracker.Remove(itemId);

            if (success) Interlocked.Increment(ref _batchCompleted);
            int left = Interlocked.Decrement(ref _pending);

            if (error != null)
            {
                ShowMessage($"Download von \"{title}\" fehlgeschlagen: {error}");
            }

            if (left <= 0)
            {
                // Alle Downloads fertig -> Zusammenfassung zeigen und Service beenden
                int completed = Interlocked.Exchange(ref _batchCompleted, 0);
                if (completed == 1 && success)
                    ShowMessage($"\"{title}\" wurde heruntergeladen.");
                else if (completed > 1)
                    ShowMessage($"{completed} Downloads abgeschlossen.");
                else if (cancelled)
                    ShowMessage("Download abgebrochen.");

                StopForeground(StopForegroundFlags.Remove);
                StopSelfResult(_latestStartId);
            }
        }
    }
    private static void ShowMessage(string message)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (Shell.Current != null)
                await Shell.Current.DisplayAlert("Download", message, "OK");
        });
    }

    private Notification CreateNotification(string title, int progress)
    {
        return new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle($"Lade herunter: {title}")
            .SetContentText(progress < 0 ? "Wird vom Server vorbereitet..." : $"{progress}%")
            .SetSmallIcon(Resource.Drawable.navigation_empty_icon) // TODO: Eigenes Jellyfin-Icon einfügen
            .SetProgress(100, Math.Max(progress, 0), progress < 0)
            .SetOngoing(true)
            .AddAction(0, "Alle abbrechen", CancelPendingIntent())
            .Build();
    }

    private PendingIntent CancelPendingIntent()
    {
        var cancelIntent = new Intent(this, typeof(ForegroundDownloadService)).SetAction(ActionCancelAll);
        return PendingIntent.GetService(this, 0, cancelIntent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
    }

    private void UpdateNotification(string title, int progress)
    {
        _notificationManager.Notify(NotificationId, CreateNotification(title, progress));
    }

    private void CreateNotificationChannel()
    {
        _notificationManager = (NotificationManager)GetSystemService(NotificationService);
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(ChannelId, "Downloads", NotificationImportance.Low)
            {
                Description = "Zeigt den Fortschritt von Offline-Downloads"
            };
            _notificationManager.CreateNotificationChannel(channel);
        }
    }
}




