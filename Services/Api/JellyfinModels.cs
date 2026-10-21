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
        public string Type { get; set; } // z.B. "Movie" oder "Series"

        // Eigene Property, die wir später für das UI-Binding mit der Bild-URL füllen
        public string ImageUrl { get; set; }
    }
}