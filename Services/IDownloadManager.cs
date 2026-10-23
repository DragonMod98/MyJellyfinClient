namespace MyJellyfinClient.Services;

public interface IDownloadManager
{
    // Startet den Download. itemId wird benötigt, um später die DB zu updaten.
    void StartDownload(string itemId, string downloadUrl, string title, long estimatedBytes = 0);

    // Bricht einen laufenden oder wartenden Download ab
    void Cancel(string itemId);

    // Bricht alle laufenden und wartenden Downloads ab
    void CancelAll();
}

