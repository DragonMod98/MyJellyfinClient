using MyJellyfinClient.Models;

namespace MyJellyfinClient.Services;

/// <summary>
/// Beschreibt ein Element, das heruntergeladen werden soll.
/// </summary>
public class DownloadRequest
{
    public string ItemId { get; set; }
    public string Title { get; set; }
    public string SeriesName { get; set; }
    public string SeasonName { get; set; }
    public bool IsSeries { get; set; }
    public long RunTimeTicks { get; set; }
    public DownloadOptions Options { get; set; } = new();
}

/// <summary>
/// Gemeinsame Logik zum Starten von Downloads (Einzelfilm, Folge oder ganze Staffel).
/// </summary>
public class DownloadStarter
{
    private readonly ISettingsService _settings;
    private readonly IDownloadManager _downloadManager;
    private readonly LocalDatabaseService _dbService;
    private readonly ServerConnectionService _connectionService;

    public DownloadStarter(
        ISettingsService settings,
        IDownloadManager downloadManager,
        LocalDatabaseService dbService,
        ServerConnectionService connectionService)
    {
        _settings = settings;
        _downloadManager = downloadManager;
        _dbService = dbService;
        _connectionService = connectionService;
    }

    /// <summary>
    /// Fordert (ab Android 13) die Benachrichtigungs-Berechtigung an.
    /// </summary>
    public static async Task EnsureNotificationPermissionAsync()
    {
#if ANDROID
        if (await Permissions.CheckStatusAsync<Permissions.PostNotifications>() != PermissionStatus.Granted)
        {
            await Permissions.RequestAsync<Permissions.PostNotifications>();
        }
#else
        await Task.CompletedTask;
#endif
    }

    /// <summary>
    /// Startet die Downloads. Gibt die Anzahl der tatsaechlich gestarteten Downloads zurueck
    /// oder -1, wenn der Server nicht erreichbar ist.
    /// </summary>
    public async Task<int> StartAsync(IEnumerable<DownloadRequest> requests)
    {
        string token = await _settings.GetAuthTokenAsync();
        string userId = _settings.UserId;

        // Gleiche (aktive) Server-URL wie die Bibliothek verwenden (lokal/extern)
        string serverUrl = await _connectionService.GetActiveServerUrlAsync();
        if (string.IsNullOrEmpty(serverUrl))
            return -1;

        await EnsureNotificationPermissionAsync();

        int started = 0;
        foreach (var request in requests)
        {
            var existing = await _dbService.GetMediaItemAsync(request.ItemId);

            // Bereits heruntergeladene Elemente ueberspringen
            if (existing is { IsDownloaded: true } && !string.IsNullOrEmpty(existing.LocalFilePath) && File.Exists(existing.LocalFilePath))
                continue;

            // Vorhandenen Fortschritt behalten, Metadaten aktualisieren
            var item = existing ?? new LocalMediaItem { JellyfinItemId = request.ItemId };
            item.ProfileId = userId;
            item.Title = request.Title;
            item.SeriesName = request.SeriesName;
            item.SeasonName = request.SeasonName;
            item.IsSeries = request.IsSeries;
            if (request.RunTimeTicks > 0)
                item.TotalTicks = request.RunTimeTicks;
            item.IsDownloaded = false; // Wird vom Background-Service auf true gesetzt, wenn fertig
            await _dbService.SaveOrUpdateMediaItemAsync(item);

            string url = BuildUrl(serverUrl, token, request);
            _downloadManager.StartDownload(request.ItemId, url, request.Title);
            started++;
        }

        return started;
    }

    private static string BuildUrl(string serverUrl, string token, DownloadRequest request)
    {
        var o = request.Options ?? new DownloadOptions();
        string baseUrl = serverUrl.TrimEnd('/');

        // Original mit allen Spuren: Datei direkt laden
        if (!o.NeedsServerProcessing)
            return $"{baseUrl}/Items/{request.ItemId}/Download?api_key={token}";

        // Sonst vom Server als MKV mit der gewaehlten Tonspur/Untertitel (und Qualitaet) erzeugen lassen
        var sb = new System.Text.StringBuilder($"{baseUrl}/Videos/{request.ItemId}/stream.mkv?api_key={token}&Static=false&SubtitleMethod=Embed");
        if (!string.IsNullOrEmpty(o.MediaSourceId)) sb.Append($"&MediaSourceId={o.MediaSourceId}");
        if (o.AudioStreamIndex is int a) sb.Append($"&AudioStreamIndex={a}");
        if (o.SubtitleStreamIndex is int s) sb.Append($"&SubtitleStreamIndex={s}");

        switch (o.Quality)
        {
            case "1080p":
                sb.Append("&VideoCodec=h264&AudioCodec=aac&VideoBitRate=10000000&MaxWidth=1920&AllowVideoStreamCopy=false");
                break;
            case "720p":
                sb.Append("&VideoCodec=h264&AudioCodec=aac&VideoBitRate=4000000&MaxWidth=1280&AllowVideoStreamCopy=false");
                break;
            default: // Original-Qualitaet: Video/Ton nach Moeglichkeit unveraendert uebernehmen
                sb.Append("&VideoCodec=h264,hevc&AudioCodec=aac,ac3,eac3,mp3,opus&AllowVideoStreamCopy=true&AllowAudioStreamCopy=true");
                break;
        }

        return sb.ToString();
    }
}
