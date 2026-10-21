using SQLite;

namespace MyJellyfinClient.Models;

[Table("LocalMediaItems")]
public class LocalMediaItem
{
    [PrimaryKey]
    public string JellyfinItemId { get; set; }

    [Indexed]
    public string ProfileId { get; set; } // Für Multi-User Support

    public string Title { get; set; }
    public string SeriesName { get; set; }
    public bool IsSeries { get; set; }

    // Offline Video Data
    public bool IsDownloaded { get; set; }
    public string LocalFilePath { get; set; }
    public long FileSizeBytes { get; set; } // Für den Storage Usage Analyzer

    // Playback Tracking
    public long ResumePositionTicks { get; set; }
    public bool IsWatched { get; set; }

    // Sync Status
    public bool NeedsSyncToServer { get; set; }
    public DateTime LastPlayedOffline { get; set; }

    // Watchlist
    public bool IsInWatchlist { get; set; }

    public class NextUpResponse
    {
        public List<JellyfinItem> Items { get; set; }
    }

    public class JellyfinItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string SeriesName { get; set; }
    }
}