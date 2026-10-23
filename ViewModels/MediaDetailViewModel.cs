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

    // Tonspuren/Untertitel des Mediums (erster Eintrag = "Alle" bzw. "Keine")
    public ObservableCollection<TrackOption> AudioTracks { get; } = new();
    public ObservableCollection<TrackOption> SubtitleTracks { get; } = new();

    [ObservableProperty]
    private TrackOption selectedAudio;

    [ObservableProperty]
    private TrackOption selectedSubtitle;

    private MediaStreamInfo _streamInfo;

    [RelayCommand]
    public async Task FetchDownloadOptionsAsync()
    {
        // Grundauswahl ist immer verfuegbar, auch wenn der Server nicht antwortet
        AvailableQualities.Clear();
        AvailableQualities.Add("Original (Direct Play)");
        AudioTracks.Clear();
        AudioTracks.Add(new TrackOption { Index = -1, Name = "Alle Tonspuren (Original)" });
        SubtitleTracks.Clear();
        SubtitleTracks.Add(new TrackOption { Index = -1, Name = "Alle Untertitel (Original)" });
        SubtitleTracks.Add(new TrackOption { Index = -2, Name = "Keine Untertitel" });

        try
        {
            string token = await _settings.GetAuthTokenAsync();
            string authHeader = $"MediaBrowser Token=\"{token}\"";

            var json = await _apiService.GetPlaybackInfoAsync(authHeader, JellyfinItemId, _settings.UserId);
            _streamInfo = MediaStreamParser.Parse(json);

            // Qualitaeten nur anbieten, wenn das Original gross genug ist (Hoehe 0 = unbekannt)
            int height = _streamInfo.Height;
            if (height == 0 || height >= 1080)
                AvailableQualities.Add("1080p - 10 Mbps (Transcode)");
            if (height == 0 || height > 720)
                AvailableQualities.Add("720p - 4 Mbps (Transcode)");

            foreach (var a in _streamInfo.Audio) AudioTracks.Add(a);
            foreach (var s in _streamInfo.Subtitles) SubtitleTracks.Add(s);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler beim Laden der Optionen: {ex.Message}");
        }

        // Vorgaben aus den Einstellungen vorauswaehlen, soweit verfuegbar
        SelectedQuality = AvailableQualities.FirstOrDefault(q => !string.IsNullOrEmpty(_settings.DefaultQuality) && q.StartsWith(_settings.DefaultQuality))
                          ?? AvailableQualities.First();

        string audioLang = DownloadDefaults.NormalizeLanguage(_settings.DefaultAudioLanguage);
        SelectedAudio = (!string.IsNullOrEmpty(audioLang) ? AudioTracks.FirstOrDefault(t => t.Index >= 0 && t.Language == audioLang) : null)
                        ?? AudioTracks.First();

        string subLang = _settings.DefaultSubtitleLanguage;
        SelectedSubtitle = subLang == "none"
            ? SubtitleTracks.First(t => t.Index == -2)
            : (!string.IsNullOrEmpty(subLang) ? SubtitleTracks.FirstOrDefault(t => t.Index >= 0 && t.Language == DownloadDefaults.NormalizeLanguage(subLang)) : null)
              ?? SubtitleTracks.First();
    }

    [RelayCommand]
    public async Task StartDownloadAsync()
    {
        var options = new DownloadOptions { MediaSourceId = _streamInfo?.MediaSourceId };

        if (SelectedQuality?.Contains("1080p") == true) options.Quality = "1080p";
        else if (SelectedQuality?.Contains("720p") == true) options.Quality = "720p";

        // -1 = alle Spuren (nichts angeben), -2 = keine Untertitel
        if (SelectedAudio is { Index: >= 0 }) options.AudioStreamIndex = SelectedAudio.Index;
        if (SelectedSubtitle is { Index: >= 0 }) options.SubtitleStreamIndex = SelectedSubtitle.Index;
        else if (SelectedSubtitle is { Index: -2 }) options.SubtitleStreamIndex = -1;

        long.TryParse(RunTimeTicksText, out long runTimeTicks);

        var request = new DownloadRequest
        {
            ItemId = JellyfinItemId,
            Title = MediaTitle,
            SeriesName = SeriesName,
            SeasonName = SeasonName,
            IsSeries = IsSeriesText == "true" || !string.IsNullOrEmpty(SeriesName),
            RunTimeTicks = runTimeTicks,
            Options = options
        };

        int started = await _downloadStarter.StartAsync(new[] { request });

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
