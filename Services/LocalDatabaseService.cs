using SQLite;
using MyJellyfinClient.Models;

namespace MyJellyfinClient.Services;

public class LocalDatabaseService
{
    private SQLiteAsyncConnection _database;
    private readonly string _dbPath;

    public LocalDatabaseService()
    {
        // Speichert die DB im App-spezifischen Datenverzeichnis
        _dbPath = Path.Combine(FileSystem.AppDataDirectory, "JellyfinOfflineCache.db3");
    }

    private async Task InitAsync()
    {
        if (_database is not null)
            return;

        _database = new SQLiteAsyncConnection(_dbPath);

        // Tabellen erstellen
        await _database.CreateTableAsync<LocalMediaItem>();
    }

    // --- Media Item CRUD ---

    public async Task<LocalMediaItem> GetMediaItemAsync(string itemId)
    {
        await InitAsync();
        return await _database.Table<LocalMediaItem>().FirstOrDefaultAsync(i => i.JellyfinItemId == itemId);
    }

    public async Task SaveOrUpdateMediaItemAsync(LocalMediaItem item)
    {
        await InitAsync();
        var existing = await GetMediaItemAsync(item.JellyfinItemId);
        if (existing == null)
        {
            await _database.InsertAsync(item);
        }
        else
        {
            await _database.UpdateAsync(item);
        }
    }

    // --- Offline & Sync Queries ---

    public async Task<List<LocalMediaItem>> GetItemsPendingSyncAsync()
    {
        await InitAsync();
        return await _database.Table<LocalMediaItem>()
                              .Where(i => i.NeedsSyncToServer)
                              .ToListAsync();
    }

    public async Task<List<LocalMediaItem>> GetDownloadedItemsAsync(string profileId)
    {
        await InitAsync();
        return await _database.Table<LocalMediaItem>()
                              .Where(i => i.IsDownloaded && i.ProfileId == profileId)
                              .ToListAsync();
    }

    public async Task UpdatePlaybackProgressAsync(string itemId, long ticks, bool isWatched, long totalTicks = 0)
    {
        await InitAsync();
        var item = await GetMediaItemAsync(itemId);
        if (item != null)
        {
            item.ResumePositionTicks = ticks;
            item.IsWatched = isWatched;
            if (totalTicks > 0)
                item.TotalTicks = totalTicks; // Fuer den Fortschrittsbalken
            item.NeedsSyncToServer = true;
            item.LastPlayedOffline = DateTime.UtcNow;

            await _database.UpdateAsync(item);
        }
    }

    public async Task DeleteMediaItemAsync(string itemId)
    {
        await InitAsync();
        var item = await GetMediaItemAsync(itemId);
        if (item == null) return;

        if (item.NeedsSyncToServer)
        {
            // Gesehen-Status noch nicht zu Jellyfin gemeldet: Eintrag ohne Datei behalten,
            // bis der Sync durch ist (wird in der Download-Liste nicht mehr angezeigt).
            item.IsDownloaded = false;
            item.LocalFilePath = null;
            item.FileSizeBytes = 0;
            await _database.UpdateAsync(item);
        }
        else
        {
            await _database.DeleteAsync<LocalMediaItem>(itemId);
        }
    }

    // --- Storage Analyzer Helper ---

    public async Task<long> GetTotalUsedStorageBytesAsync()
    {
        await InitAsync();
        var downloadedItems = await _database.Table<LocalMediaItem>().Where(i => i.IsDownloaded).ToListAsync();
        return downloadedItems.Sum(i => i.FileSizeBytes);
    }
}

