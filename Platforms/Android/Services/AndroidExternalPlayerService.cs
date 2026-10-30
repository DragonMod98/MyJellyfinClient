using Android.App;
using Android.Content;
using Android.Content.PM;
using AndroidX.Core.Content;
using MyJellyfinClient.Services;
using AndroidFileProvider = AndroidX.Core.Content.FileProvider;

namespace MyJellyfinClient.Platforms.Android.Services;

/// <summary>Stellt Videodateien aus dem App-Speicher fremden Apps ueber eine content://-URI bereit.</summary>
[ContentProvider(new[] { "${applicationId}.playerfileprovider" }, Exported = false, GrantUriPermissions = true)]
[MetaData("android.support.FILE_PROVIDER_PATHS", Resource = "@xml/player_file_paths")]
public class PlayerFileProvider : AndroidFileProvider
{
}

public class AndroidExternalPlayerService : IExternalPlayerService
{
    private static Context Ctx => Platform.CurrentActivity ?? (Context)global::Android.App.Application.Context;

    public IReadOnlyList<ChoiceOption> GetInstalledPlayers()
    {
        var result = new List<ChoiceOption>();

        try
        {
            var pm = Ctx.PackageManager;
            var probe = new Intent(Intent.ActionView);
            probe.SetDataAndType(global::Android.Net.Uri.Parse("content://media/external/video/media/1"), "video/*");

            var seen = new HashSet<string>();
            foreach (var info in pm.QueryIntentActivities(probe, PackageInfoFlags.MatchAll))
            {
                string package = info.ActivityInfo?.PackageName;
                if (string.IsNullOrEmpty(package) || package == Ctx.PackageName || !seen.Add(package)) continue;

                string label = info.LoadLabel(pm)?.ToString() ?? package;
                result.Add(new ChoiceOption(package, label));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Player-Apps ermitteln fehlgeschlagen: {ex.Message}");
        }

        return result.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public bool Play(string filePath, string title, string package)
    {
        try
        {
            var context = Ctx;
            var file = new Java.IO.File(filePath);
            var uri = AndroidFileProvider.GetUriForFile(context, $"{context.PackageName}.playerfileprovider", file);

            var intent = new Intent(Intent.ActionView);
            intent.SetDataAndType(uri, "video/*");
            intent.PutExtra(Intent.ExtraTitle, title ?? string.Empty);
            intent.PutExtra("title", title ?? string.Empty); // von einigen Playern (z. B. MX Player, VLC) ausgewertet
            intent.ClipData = ClipData.NewRawUri(string.Empty, uri);
            intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.NewTask);

            if (!string.IsNullOrEmpty(package) && package != PlayerChoice.System)
                intent.SetPackage(package);

            context.StartActivity(intent);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Externen Player starten fehlgeschlagen: {ex.Message}");
            return false;
        }
    }
}
