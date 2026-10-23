using Refit;
using static MyJellyfinClient.Models.LocalMediaItem;
using static MyJellyfinClient.Services.Api.PlaybackProgressInfo;

namespace MyJellyfinClient.Services.Api;

public interface IJellyfinApiService
{
    // Meldet den aktuellen Zeitstempel (Fortschritt) an den Server
    [Post("/Sessions/Playing/Progress")]
    Task ReportPlaybackProgressAsync([Header("Authorization")] string authHeader, [Body] PlaybackProgressInfo progressInfo);

    // Beendet eine Wiedergabe und speichert die Position (funktioniert auch ohne vorherigen Session-Start)
    [Post("/Sessions/Playing/Stopped")]
    Task ReportPlaybackStoppedAsync([Header("Authorization")] string authHeader, [Body] PlaybackProgressInfo progressInfo);

    // Markiert einen Film oder eine Episode vollständig als "Gesehen"
    [Post("/Users/{userId}/PlayedItems/{itemId}")]
    Task MarkItemAsWatchedAsync([Header("Authorization")] string authHeader, string userId, string itemId);

    // Holt das Media-Manifest (wird später für den Download-Manager benötigt)
    [Get("/Items/{itemId}/PlaybackInfo?userId={userId}")]
    Task<string> GetPlaybackInfoAsync([Header("Authorization")] string authHeader, string itemId, string userId);

    // Holt die Empfehlungen (Next Up) für einen Nutzer basierend auf der aktuellen Serie
    [Get("/Shows/NextUp?userId={userId}&seriesId={seriesId}")]
    Task<NextUpResponse> GetNextUpAsync([Header("Authorization")] string authHeader, string userId, string seriesId);

    // Details zu einem Film/einer Folge (Beschreibung, Jahr, Bewertung, Genres)
    [Get("/Users/{userId}/Items/{itemId}")]
    Task<JellyfinItemDetails> GetItemDetailsAsync([Header("Authorization")] string authHeader, string userId, string itemId);

    // Holt die neuesten Filme und Serien des Nutzers
    [Get("/Users/{userId}/Items?EnableUserData=true&Fields=RunTimeTicks")]
    Task<JellyfinItemsResponse> GetLibraryItemsAsync([Header("Authorization")] string authHeader, string userId);

    // Holt die Elemente des Unterordners
    [Get("/Users/{userId}/Items?ParentId={selectedFolderId}&EnableUserData=true&Fields=RunTimeTicks")]
    Task<JellyfinItemsResponse> GetFolderItemsAsync([Header("Authorization")] string authHeader, string userId, string selectedFolderId);
}


