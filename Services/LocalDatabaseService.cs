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

    public async Task UpdatePlaybackProgressAsync(string itemId, long ticks, bool isWatched)
    {
        await InitAsync();
        var item = await GetMediaItemAsync(itemId);
        if (item != null)
        {
            item.ResumePositionTicks = ticks;
            item.IsWatched = isWatched;
            item.NeedsSyncToServer = true;
            item.LastPlayedOffline = DateTime.UtcNow;

            await _database.UpdateAsync(item);
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