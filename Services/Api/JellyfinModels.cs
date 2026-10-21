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
        public long? RunTimeTicks { get; set; }      // Laufzeit (fuer den Fortschrittsbalken)
        public string Type { get; set; } // z.B. "Movie" oder "Series"
        public bool IsFolder { get; set; }

        // Titel inkl. Folgennummer, z.B. "3. Der Titel"
        public string DisplayTitle => IndexNumber.HasValue ? $"{IndexNumber}. {Name}" : Name;

        // Eigene Property, die wir später für das UI-Binding mit der Bild-URL füllen
        public string ImageUrl { get; set; }
    }
}