namespace MyJellyfinClient.Services.Api;

// DTO (Data Transfer Object) für den Playback-Fortschritt
public class PlaybackProgressInfo
{
    public string ItemId { get; set; }
    public string SessionId { get; set; }
    public string MediaSourceId { get; set; }
    public long PositionTicks { get; set; }
    public bool IsPaused { get; set; }
    public bool IsMuted { get; set; }
    public string PlayMethod { get; set; } = "DirectPlay";

    public class JellyfinItemsResponse
    {
        public List<JellyfinItem> Items { get; set; }
    }

    public class JellyfinItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string SeriesName { get; set; }
        public string SeasonName { get; set; }
        public int? IndexNumber { get; set; }        // Folgennummer
        public long? RunTimeTicks { get; set; }
        public JellyfinUserData UserData { get; set; }

        // Anzeige-Hilfen fuer die Bibliothek (Gesehen-Status aus Jellyfin)
        public bool IsPlayed => UserData?.Played == true;
        public int UnplayedCount => UserData?.UnplayedItemCount ?? 0;
        // Haken: gesehen (Film/Folge/komplette Staffel)
        public bool ShowPlayedBadge => IsPlayed;
        // Zaehler: Ordner/Serie/Staffel mit noch ungesehenen Folgen
        public bool ShowUnplayedBadge => !IsPlayed && UnplayedCount > 0;
        // Fortschritt bei angefangenen Filmen/Folgen (0..1)
        public double WatchProgress => IsPlayed ? 0 : Math.Clamp((UserData?.PlayedPercentage ?? 0) / 100.0, 0, 1);
        public bool ShowWatchProgress => WatchProgress > 0;
        public string Type { get; set; } // z.B. "Movie" oder "Series"
        public bool IsFolder { get; set; }

        // Titel inkl. Folgennummer, z.B. "3. Der Titel"
        public string DisplayTitle => IndexNumber.HasValue ? $"{IndexNumber}. {Name}" : Name;

        // Eigene Property, die wir später für das UI-Binding mit der Bild-URL füllen
        public string ImageUrl { get; set; }
    }
}

public class JellyfinUserData
{
    public bool Played { get; set; }
    public double? PlayedPercentage { get; set; }
    public int? UnplayedItemCount { get; set; }
    public long PlaybackPositionTicks { get; set; }
}

public class JellyfinItemDetails
{
    public string Overview { get; set; }
    public int? ProductionYear { get; set; }
    public double? CommunityRating { get; set; }
    public string OfficialRating { get; set; }
    public List<string> Genres { get; set; }
    public long? RunTimeTicks { get; set; }
}

