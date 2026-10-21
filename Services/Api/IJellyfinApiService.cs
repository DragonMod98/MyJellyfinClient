using Refit;
using static MyJellyfinClient.Models.LocalMediaItem;

namespace MyJellyfinClient.Services.Api;

public interface IJellyfinApiService
{
    // Meldet den aktuellen Zeitstempel (Fortschritt) an den Server
    [Post("/Sessions/Playing/Progress")]
    Task ReportPlaybackProgressAsync([Header("Authorization")] string authHeader, [Body] PlaybackProgressInfo progressInfo);

    // Markiert einen Film oder eine Episode vollständig als "Gesehen"
    [Post("/Users/{userId}/PlayedItems/{itemId}")]
    Task MarkItemAsWatchedAsync([Header("Authorization")] string authHeader, string userId, string itemId);

    // Holt das Media-Manifest (wird später für den Download-Manager benötigt)
    [Get("/Items/{itemId}/PlaybackInfo?userId={userId}")]
    Task<string> GetPlaybackInfoAsync([Header("Authorization")] string authHeader, string itemId, string userId);

    // Holt die Empfehlungen (Next Up) für einen Nutzer basierend auf der aktuellen Serie
    [Get("/Shows/NextUp?userId={userId}&seriesId={seriesId}")]
    Task<NextUpResponse> GetNextUpAsync([Header("Authorization")] string authHeader, string userId, string seriesId);
}