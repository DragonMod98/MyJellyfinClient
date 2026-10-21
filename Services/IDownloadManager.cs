namespace MyJellyfinClient.Services;

public interface IDownloadManager
{
    // Startet den Download. itemId wird benötigt, um später die DB zu updaten.
    void StartDownload(string itemId, string downloadUrl, string title);
}