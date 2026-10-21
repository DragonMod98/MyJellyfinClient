using MyJellyfinClient.Models;

namespace MyJellyfinClient.Services;

public class StorageManager
{
    private readonly LocalDatabaseService _dbService;

    public StorageManager(LocalDatabaseService dbService)
    {
        _dbService = dbService;
    }

    /// <summary>
    /// Sucht nach lokal gespeicherten Medien, die als 'IsWatched = true' markiert sind,
    /// löscht die physische Videodatei und aktualisiert die Datenbank.
    /// </summary>
    public async Task CleanupWatchedItemsAsync(string currentProfileId)
    {
        // Alle heruntergeladenen Items des aktuellen Nutzers abfragen
        var downloadedItems = await _dbService.GetDownloadedItemsAsync(currentProfileId);

        // Nur die filtern, die vollständig gesehen wurden
        var watchedItems = downloadedItems.Where(i => i.IsWatched).ToList();

        foreach (var item in watchedItems)
        {
            try
            {
                // Physische Datei vom Gerät löschen
                if (File.Exists(item.LocalFilePath))
                {
                    File.Delete(item.LocalFilePath);
                    System.Diagnostics.Debug.WriteLine($"[StorageManager] Datei gelöscht: {item.Title}");
                }

                // Datenbank-Status zurücksetzen
                item.IsDownloaded = false;
                item.LocalFilePath = null;
                item.FileSizeBytes = 0;

                await _dbService.SaveOrUpdateMediaItemAsync(item);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StorageManager] Fehler beim Löschen von {item.Title}: {ex.Message}");
            }
        }
    }
}