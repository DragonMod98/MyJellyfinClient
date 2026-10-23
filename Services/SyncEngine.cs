using Microsoft.Maui.Networking;
using MyJellyfinClient.Services.Api;

namespace MyJellyfinClient.Services;

public class SyncEngine
{
    private readonly LocalDatabaseService _dbService;
    private readonly ISettingsService _settings;
    private readonly IJellyfinApiService _apiService;
    private bool _isSyncing = false;


    public SyncEngine(LocalDatabaseService dbService, IJellyfinApiService apiService, ISettingsService settings)
    {
        _dbService = dbService;
        _apiService = apiService;
        _settings = settings;

        // Abonniere das Event für Netzwerkänderungen des Betriebssystems
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
    }

    private async void OnConnectivityChanged(object sender, ConnectivityChangedEventArgs e)
    {
        // Sobald wir wieder Internet haben, starte den Sync
        if (e.NetworkAccess == NetworkAccess.Internet)
        {
            await SyncPendingItemsAsync();
        }
    }

    public async Task SyncPendingItemsAsync()
    {
        // Verhindert doppelte Ausführungen, falls das Event mehrfach feuert
        if (_isSyncing || Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            return;

        _isSyncing = true;

        try
        {
            var pendingItems = await _dbService.GetItemsPendingSyncAsync();

            if (!pendingItems.Any())
                return;

            string currentUserId = _settings.UserId;
            string rawToken = await _settings.GetAuthTokenAsync();

            if (string.IsNullOrEmpty(currentUserId) || string.IsNullOrEmpty(rawToken))
                return; // Abbruch, wenn nicht eingeloggt

            string currentAuthToken = $"MediaBrowser Token=\"{rawToken}\"";

            foreach (var item in pendingItems)
            {
                try
                {
                    if (item.IsWatched)
                    {
                        // Komplett gesehen -> an Server melden
                        await _apiService.MarkItemAsWatchedAsync(currentAuthToken, currentUserId, item.JellyfinItemId);
                    }
                    else
                    {
                        // Nur Fortschritt updaten
                        var progressInfo = new PlaybackProgressInfo
                        {
                            ItemId = item.JellyfinItemId,
                            PositionTicks = item.ResumePositionTicks,
                            IsPaused = true, // Offline immer pausiert melden
                            SessionId = Guid.NewGuid().ToString() // Jellyfin erwartet oft eine Session-ID
                        };

                        await _apiService.ReportPlaybackStoppedAsync(currentAuthToken, progressInfo);
                    }

                    // Bei Erfolg: Lokales Sync-Flag entfernen und in DB speichern
                    item.NeedsSyncToServer = false;
                    await _dbService.SaveOrUpdateMediaItemAsync(item);

                    // Zwischenzeitlich geloeschte Eintraege (nur noch fuer den Sync behalten) jetzt entfernen
                    if (!item.IsDownloaded)
                        await _dbService.DeleteMediaItemAsync(item.JellyfinItemId);
                }
                catch (Exception ex)
                {
                    // Fehler beim Sync dieses speziellen Items (z.B. Server 500)
                    // Wird beim nächsten Durchlauf erneut versucht.
                    System.Diagnostics.Debug.WriteLine($"Fehler beim Sync von Item {item.JellyfinItemId}: {ex.Message}");
                }
            }
        }
        finally
        {
            _isSyncing = false;
        }
    }

    // Explizite Methode zum Deregistrieren (wichtig für den App-Lebenszyklus)
    public void Dispose()
    {
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
    }
}
