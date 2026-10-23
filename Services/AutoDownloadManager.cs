using MyJellyfinClient.Models;
using MyJellyfinClient.Services.Api;

namespace MyJellyfinClient.Services;

/// <summary>
/// Laedt bei Serien automatisch die naechste Folge, sobald eine heruntergeladene Folge
/// gesehen wurde und dieser Status erfolgreich an Jellyfin gemeldet ist.
/// </summary>
public class AutoDownloadManager
{
    private readonly IJellyfinApiService _apiService;
    private readonly DownloadStarter _downloadStarter;
    private readonly LocalDatabaseService _dbService;
    private readonly ISettingsService _settings;

    public AutoDownloadManager(IJellyfinApiService apiService, DownloadStarter downloadStarter, LocalDatabaseService dbService, ISettingsService settings)
    {
        _apiService = apiService;
        _downloadStarter = downloadStarter;
        _dbService = dbService;
        _settings = settings;
    }

    /// <summary>
    /// watched = die soeben gesehene (und synchronisierte) Folge. Geladen wird die erste Folge nach der
    /// zuletzt vorhandenen Folge der Serie - ueber Staffelgrenzen hinweg - die noch nicht geladen/gesehen ist.
    /// count = Anzahl der gesehenen Folgen; pro gesehener Folge wird eine weitere geladen.
    /// </summary>
    public async Task TriggerNextEpisodeDownloadAsync(LocalMediaItem watched, int count = 1)
    {
        if (!_settings.AutoDownloadNextEpisode) return; // in den Einstellungen abgeschaltet
        if (watched == null || !watched.IsSeries) return;

        try
        {
            string userId = _settings.UserId;
            string auth = $"MediaBrowser Token=\"{await _settings.GetAuthTokenAsync()}\"";

            // Aeltere Downloads kennen die Serien-ID noch nicht: beim Server nachfragen
            string seriesId = watched.SeriesId;
            if (string.IsNullOrEmpty(seriesId))
            {
                var details = await _apiService.GetItemDetailsAsync(auth, userId, watched.JellyfinItemId);
                seriesId = details?.SeriesId;
                if (string.IsNullOrEmpty(seriesId)) return;
            }

            // Alle Folgen in Abspielreihenfolge (ohne Specials/Staffel 0)
            var response = await _apiService.GetEpisodesAsync(auth, seriesId, userId);
            var episodes = response?.Items?.Where(e => e.ParentIndexNumber != 0).ToList();
            if (episodes == null || episodes.Count == 0) return;

            // Was liegt lokal bereits vor oder laedt gerade?
            var rows = await _dbService.GetItemsBySeriesAsync(seriesId, userId);
            var have = rows.Where(r => r.IsDownloaded).Select(r => r.JellyfinItemId).ToHashSet();
            foreach (var active in DownloadTracker.Items) have.Add(active.ItemId);
            have.Add(watched.JellyfinItemId);

            // Ausgangspunkt: die am weitesten hinten liegende vorhandene bzw. gesehene Folge
            int anchor = episodes.FindLastIndex(e => have.Contains(e.Id));
            if (anchor < 0) return;

            // Die naechsten Folgen (je gesehener Folge eine), die noch nicht geladen und nicht gesehen sind.
            // Ende der Staffel => weiter mit der naechsten Staffel.
            var nextEpisodes = episodes.Skip(anchor + 1)
                .Where(e => !have.Contains(e.Id) && e.UserData?.Played != true)
                .Take(Math.Max(1, count))
                .ToList();
            if (nextEpisodes.Count == 0) return; // Serie zu Ende

            var requests = new List<DownloadRequest>();
            foreach (var next in nextEpisodes)
            {
                // Vorgaben aus den Einstellungen anwenden (soweit verfuegbar)
                var options = new DownloadOptions();
                try
                {
                    var json = await _apiService.GetPlaybackInfoAsync(auth, next.Id, userId);
                    var info = MediaStreamParser.Parse(json);
                    options = MediaStreamParser.Resolve(info, _settings.DefaultQuality, _settings.DefaultAudioLanguage, _settings.DefaultSubtitleLanguage, out _);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AutoDownload] PlaybackInfo fehlgeschlagen: {ex.Message}");
                }

                requests.Add(new DownloadRequest
                {
                    ItemId = next.Id,
                    Title = next.DisplayTitle,
                    SeriesName = next.SeriesName ?? watched.SeriesName,
                    SeasonName = next.SeasonName,
                    SeriesId = seriesId,
                    IsSeries = true,
                    RunTimeTicks = next.RunTimeTicks ?? 0,
                    Options = options
                });
            }

            int started = await _downloadStarter.StartAsync(requests);
            System.Diagnostics.Debug.WriteLine($"[AutoDownload] {requests.Count} Folge(n) angefragt, {started} gestartet");
        }        catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AutoDownload] PlaybackInfo fehlgeschlagen: {ex.Message}");
            }

            var request = new DownloadRequest
            {
                ItemId = next.Id,
                Title = next.DisplayTitle,
                SeriesName = next.SeriesName ?? watched.SeriesName,
                SeasonName = next.SeasonName,
                SeriesId = seriesId,
                IsSeries = true,
                RunTimeTicks = next.RunTimeTicks ?? 0,
                Options = options
            };

            int started = await _downloadStarter.StartAsync(new[] { request });
            System.Diagnostics.Debug.WriteLine($"[AutoDownload] {next.SeriesName} - {next.DisplayTitle}: Ergebnis {started}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AutoDownload] Fehler beim Ermitteln der naechsten Folge: {ex.Message}");
        }
    }
}

