using MyJellyfinClient.Services.Api;

namespace MyJellyfinClient.Services;

public class AutoDownloadManager
{
    private readonly IJellyfinApiService _apiService;
    private readonly IDownloadManager _downloadManager;
    private readonly ISettingsService _settings;
    private readonly LocalDatabaseService _dbService;

    public AutoDownloadManager(IJellyfinApiService apiService, IDownloadManager downloadManager, LocalDatabaseService dbService, ISettingsService settings)
    {
        _apiService = apiService;
        _downloadManager = downloadManager;
        _dbService = dbService;
        _settings = settings;
    }

    /// <summary>
    /// Prüft, ob es eine Folge-Episode gibt, und startet den Download im Hintergrund.
    /// </summary>
    public async Task TriggerNextEpisodeDownloadAsync(string seriesId, string currentUserId, string authToken)
    {
        if (string.IsNullOrEmpty(seriesId)) return;

        try
        {
            // 1. Jellyfin fragen, was die nächste Episode für diese Serie ist
            var nextUpResponse = await _apiService.GetNextUpAsync(authToken, currentUserId, seriesId);
            var nextEpisode = nextUpResponse?.Items?.FirstOrDefault();

            if (nextEpisode == null) return; // Es gibt keine nächste Folge (Staffel/Serie zu Ende)

            // 2. Prüfen, ob wir diese Folge lokal schon haben
            var localItem = await _dbService.GetMediaItemAsync(nextEpisode.Id);
            if (localItem != null && localItem.IsDownloaded)
            {
                return; // Ist bereits heruntergeladen
            }

            // 3. Download-URL generieren (Direct Stream Link)
            // Hinweis: Für eine dynamische Qualitätsauswahl müsste man hier noch den Parameter &VideoBitrate=X anfügen
            string serverBaseUrl = _settings.ServerUrl;
            string token = await _settings.GetAuthTokenAsync();
            string downloadUrl = $"{serverBaseUrl}/Items/{nextEpisode.Id}/Download?api_key={token}";

            // 4. In der lokalen DB vermerken, dass der Download startet
            if (localItem == null)
            {
                localItem = new Models.LocalMediaItem
                {
                    JellyfinItemId = nextEpisode.Id,
                    ProfileId = currentUserId,
                    Title = nextEpisode.Name,
                    SeriesName = nextEpisode.SeriesName,
                    IsSeries = true
                };
                await _dbService.SaveOrUpdateMediaItemAsync(localItem);
            }

            // 5. Nativen Android Background Service starten
            _downloadManager.StartDownload(nextEpisode.Id, downloadUrl, $"{nextEpisode.SeriesName} - {nextEpisode.Name}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AutoDownload] Fehler beim Ermitteln der nächsten Folge: {ex.Message}");
        }
    }
}