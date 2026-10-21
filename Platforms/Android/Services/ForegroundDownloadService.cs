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
    private const int NotificationId = 1001;
    private NotificationManager _notificationManager;

    public override IBinder OnBind(Intent intent) => null; // Wird nicht benötigt

    public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
    {
        string itemId = intent.GetStringExtra("itemId");
        string url = intent.GetStringExtra("url");
        string title = intent.GetStringExtra("title");

        CreateNotificationChannel();
        StartForeground(NotificationId, CreateNotification(title, 0));

        // Download im Hintergrund-Thread starten
        Task.Run(() => DownloadFileAsync(itemId, url, title, startId));

        return StartCommandResult.Sticky;
    }

    private async Task DownloadFileAsync(string itemId, string url, string title, int startId)
    {
        try
        {
            using var httpClient = new HttpClient();
            using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;

            // Datei im App-spezifischen Verzeichnis speichern (keine Storage-Permissions nötig!)
            var externalPath = GetExternalFilesDir(Environment.DirectoryMovies)?.AbsolutePath;
            var fileName = $"{itemId}.mp4"; // Oder mkv, je nach Manifest
            var filePath = Path.Combine(externalPath, fileName);

            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
            using var contentStream = await response.Content.ReadAsStreamAsync();

            var buffer = new byte[8192];
            long totalRead = 0;
            int read;
            int lastPercentage = 0;

            while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, read);
                totalRead += read;

                if (totalBytes != -1)
                {
                    var percentage = (int)((totalRead * 100) / totalBytes);
                    // Update der Notification nur jeden vollen Prozentpunkt, um UI-Spam zu vermeiden
                    if (percentage > lastPercentage)
                    {
                        lastPercentage = percentage;
                        UpdateNotification(title, percentage);
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

            // Benachrichtigung auf "Fertig" setzen
            _notificationManager.Notify(NotificationId, new NotificationCompat.Builder(this, ChannelId)
                .SetContentTitle($"{title} heruntergeladen")
                .SetSmallIcon(Resource.Drawable.navigation_empty_icon) // TODO: Eigenes Jellyfin-Icon einfügen
                .Build());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Download Fehler: {ex.Message}");
            // Optional: Fehler-Notification anzeigen
        }
        finally
        {
            // Service beenden, wenn Download fertig
            StopForeground(StopForegroundFlags.Remove);
            StopSelfResult(startId);
        }
    }

    private Notification CreateNotification(string title, int progress)
    {
        return new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle($"Lade herunter: {title}")
            .SetContentText($"{progress}%")
            .SetSmallIcon(Resource.Drawable.navigation_empty_icon) // TODO: Eigenes Jellyfin-Icon einfügen
            .SetProgress(100, progress, false)
            .SetOngoing(true)
            .Build();
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