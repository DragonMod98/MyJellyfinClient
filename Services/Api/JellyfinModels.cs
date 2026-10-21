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
}