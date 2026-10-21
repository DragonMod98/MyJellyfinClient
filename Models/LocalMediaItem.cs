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
    public string SeasonName { get; set; }
    public bool IsSeries { get; set; }

    // Offline Video Data
    public bool IsDownloaded { get; set; }
    public string LocalFilePath { get; set; }
    public long FileSizeBytes { get; set; } // F�r den Storage Usage Analyzer

    // Playback Tracking
    public long ResumePositionTicks { get; set; }
    public long TotalTicks { get; set; } // Gesamtlaenge (fuer den Fortschrittsbalken)
    public bool IsWatched { get; set; }

    // Fortschritt 0..1 fuer die Anzeige in der Download-Uebersicht
    [Ignore]
    public double Progress =>
        IsWatched ? 1.0
        : TotalTicks > 0 ? Math.Clamp((double)ResumePositionTicks / TotalTicks, 0.0, 1.0)
        : 0.0;

    [Ignore]
    public string ProgressText =>
        IsWatched ? "Gesehen"
        : Progress > 0 ? $"{(int)(Progress * 100)} % gesehen"
        : "Nicht angesehen";

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