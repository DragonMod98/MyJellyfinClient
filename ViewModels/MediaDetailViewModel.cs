using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyJellyfinClient.Services;
using MyJellyfinClient.Services.Api;
using System.Collections.ObjectModel;
using System.Text.Json;
using static MyJellyfinClient.Models.LocalMediaItem;

namespace MyJellyfinClient.ViewModels;

// Diese Attribute weisen MAUI an, die übergebenen Shell-Parameter 
// automatisch in die Variablen "JellyfinItemId" und "MediaTitle" zu schreiben.
[QueryProperty(nameof(JellyfinItemId), "ItemId")]
[QueryProperty(nameof(MediaTitle), "Title")]
[QueryProperty(nameof(SeriesName), "SeriesName")]
[QueryProperty(nameof(SeasonName), "SeasonName")]
[QueryProperty(nameof(IsSeriesText), "IsSeries")]
[QueryProperty(nameof(RunTimeTicksText), "RunTimeTicks")]
public partial class MediaDetailViewModel : ObservableObject
{
    private readonly IJellyfinApiService _apiService;
    private readonly ISettingsService _settings;
    private readonly DownloadStarter _downloadStarter;

    // UI Bindings
    [ObservableProperty]
    private string mediaTitle;

    [ObservableProperty]
    private string jellyfinItemId;

    // Metadaten aus der Bibliothek (fuer die Gruppierung der Downloads)
    [ObservableProperty]
    private string seriesName;

    [ObservableProperty]
    private string seasonName;

    [ObservableProperty]
    private string isSeriesText;

    [ObservableProperty]
    private string runTimeTicksText;

    public ObservableCollection<string> AvailableQualities { get; } = new();

    [ObservableProperty]
    private string selectedQuality;

    public MediaDetailViewModel(IJellyfinApiService apiService, DownloadStarter downloadStarter, ISettingsService settings)
    {
        _apiService = apiService;
        _downloadStarter = downloadStarter;
        _settings = settings;
    }

    [RelayCommand]
    public async Task FetchDownloadOptionsAsync()
    {
        // Original ist immer verfügbar, auch wenn der Server nicht antwortet
        AvailableQualities.Clear();
        AvailableQualities.Add("Original (Direct Play)");
        SelectedQuality = AvailableQualities.First();

        try
        {
            string token = await _settings.GetAuthTokenAsync();
            string currentUserId = _settings.UserId;
            string authHeader = $"MediaBrowser Token=\"{token}\"";

            // Manifest vom Server holen
            var playbackInfoJson = await _apiService.GetPlaybackInfoAsync(authHeader, JellyfinItemId, currentUserId);

            // Höhe des Originalvideos ermitteln, um nur sinnvolle Optionen anzubieten
            int height = 0;
            using var doc = JsonDocument.Parse(playbackInfoJson);
            if (doc.RootElement.TryGetProperty("MediaSources", out var mediaSources)
                && mediaSources.GetArrayLength() > 0
                && mediaSources[0].TryGetProperty("MediaStreams", out var streams))
            {
                foreach (var stream in streams.EnumerateArray())
                {
                    if (stream.TryGetProperty("Type", out var type) && type.GetString() == "Video"
                        && stream.TryGetProperty("Height", out var h) && h.ValueKind == JsonValueKind.Number)
                    {
                        height = h.GetInt32();
                        break;
                    }
                }
            }

            // Höhe unbekannt (0) => alle Optionen anbieten
            if (height == 0 || height > 720)
            {
                if (height == 0 || height >= 1080)
                    AvailableQualities.Add("1080p - 10 Mbps (Transcode)");
                AvailableQualities.Add("720p - 4 Mbps (Transcode)");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler beim Laden der Optionen: {ex.Message}");
        }
    }


    [RelayCommand]
    public async Task StartDownloadAsync()
    {
        // Transcoding-Parameter anhaengen, falls nicht "Original" gewaehlt wurde
        // Ohne Auswahl (null) wird die Originaldatei geladen
        string suffix = null;
        if (SelectedQuality?.Contains("1080p") == true)
            suffix = "&VideoBitrate=10000000&Width=1920";
        else if (SelectedQuality?.Contains("720p") == true)
            suffix = "&VideoBitrate=4000000&Width=1280";

        long.TryParse(RunTimeTicksText, out long runTimeTicks);

        var request = new DownloadRequest
        {
            ItemId = JellyfinItemId,
            Title = MediaTitle,
            SeriesName = SeriesName,
            SeasonName = SeasonName,
            IsSeries = IsSeriesText == "true" || !string.IsNullOrEmpty(SeriesName),
            RunTimeTicks = runTimeTicks
        };

        int started = await _downloadStarter.StartAsync(new[] { request }, suffix);

        if (started < 0)
        {
            await Shell.Current.DisplayAlert("Download", "Server nicht erreichbar.", "OK");
        }
        else if (started == 0)
        {
            await Shell.Current.DisplayAlert("Download", "Dieses Medium ist bereits heruntergeladen.", "OK");
        }
        else
        {
            await Shell.Current.DisplayAlert("Download", $"Download von \"{MediaTitle}\" wurde gestartet. Den Fortschritt siehst du in der Benachrichtigung.", "OK");
        }
    }
}
