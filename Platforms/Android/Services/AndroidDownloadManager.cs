using Android.Content;
using Application = Android.App.Application;

namespace MyJellyfinClient.Platforms.Android.Services;

public class AndroidDownloadManager : MyJellyfinClient.Services.IDownloadManager
{
    public void StartDownload(string itemId, string downloadUrl, string title, long estimatedBytes = 0)
    {
        MyJellyfinClient.Services.DownloadTracker.Register(itemId, title);

        var context = Application.Context;
        var intent = new Intent(context, typeof(ForegroundDownloadService));
        intent.PutExtra("itemId", itemId);
        intent.PutExtra("url", downloadUrl);
        intent.PutExtra("title", title);
        intent.PutExtra("estimatedBytes", estimatedBytes);

        // Startet den nativen Service
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            context.StartForegroundService(intent);
        }
        else
        {
            context.StartService(intent);
        }
    }

    public void Cancel(string itemId) => ForegroundDownloadService.Cancel(itemId);

    public void CancelAll() => ForegroundDownloadService.CancelAll();
}

