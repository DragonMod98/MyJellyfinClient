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
[QueryProperty(nameof(SeriesId), "SeriesId")]
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
    private string seriesId;

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

    // --- Info-Bereich (Chips, Genres, Beschreibung) ---
    public ObservableCollection<string> InfoChips { get; } = new();

    [ObservableProperty]
    private string genresText;

    [ObservableProperty]
    private string overview;

    [ObservableProperty]
    private bool isOverviewExpanded;

    public bool HasOverview => !string.IsNullOrWhiteSpace(Overview);
    public bool HasGenres => !string.IsNullOrWhiteSpace(GenresText);
    public int OverviewMaxLines => IsOverviewExpanded ? 100 : 4;

    partial void OnOverviewChanged(string value) => OnPropertyChanged(nameof(HasOverview));
    partial void OnGenresTextChanged(string value) => OnPropertyChanged(nameof(HasGenres));
    partial void OnIsOverviewExpandedChanged(bool value) => OnPropertyChanged(nameof(OverviewMaxLines));

    [RelayCommand]
    private void ToggleOverview() => IsOverviewExpanded = !IsOverviewExpanded;

    private static string LangName(string code) =>
        DownloadDefaults.AudioLanguages.FirstOrDefault(l => l.Code == code)?.Name ?? code?.ToUpperInvariant();

    private static string FormatRuntime(long ticks)
    {
        var t = TimeSpan.FromTicks(ticks);
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours} Std. {t.Minutes} Min." : $"{Math.Max(1, t.Minutes)} Min.";
    }

    private static string FormatSize(long bytes) =>
        bytes >= 1L << 30 ? $"{bytes / (1024.0 * 1024 * 1024):0.0} GB" : $"{bytes / (1024.0 * 1024):0} MB";

    private static string ResolutionLabel(int width, int height)
    {
        if (height == 0) return null;
        if (width >= 3500 || height >= 2000) return "4K";
        if (height >= 1300) return "1440p";
        if (height >= 1000 || width >= 1800) return "1080p";
        if (height >= 700 || width >= 1200) return "720p";
        return $"{height}p";
    }

    private static string ChannelLabel(int ch) => ch switch { 1 => "Mono", 2 => "Stereo", 6 => "5.1", 8 => "7.1", > 0 => $"{ch} Kanäle", _ => null };

    // Liest Details (Beschreibung, Jahr, Bewertung) und baut die uebersichtlichen Info-Chips
    private async Task LoadInfoAsync()
    {
        JellyfinItemDetails details = null;
        try
        {
            string token = await _settings.GetAuthTokenAsync();
            details = await _apiService.GetItemDetailsAsync($"MediaBrowser Token=\"{token}\"", _settings.UserId, JellyfinItemId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Details laden fehlgeschlagen: {ex.Message}");
        }

        var chips = new List<string>();

        // Laufzeit (Server-Details, sonst Medieninfo, sonst Wert aus der Bibliothek)
        long ticks = details?.RunTimeTicks ?? _streamInfo?.RunTimeTicks ?? 0;
        if (ticks == 0) long.TryParse(RunTimeTicksText, out ticks);
        if (ticks > 0) chips.Add("⏱ " + FormatRuntime(ticks));

        if (details?.ProductionYear is int year) chips.Add($"📅 {year}");
        if (details?.CommunityRating is double rating && rating > 0) chips.Add($"★ {rating:0.0}");
        if (!string.IsNullOrEmpty(details?.OfficialRating)) chips.Add($"FSK {details.OfficialRating}");

        if (_streamInfo != null)
        {
            // Qualitaet des Originals, z.B. "1080p · HEVC · HDR10"
            var quality = new List<string>();
            string res = ResolutionLabel(_streamInfo.Width, _streamInfo.Height);
            if (res != null) quality.Add(res);
            if (!string.IsNullOrEmpty(_streamInfo.VideoCodec)) quality.Add(_streamInfo.VideoCodec.ToUpperInvariant());
            if (!string.IsNullOrEmpty(_streamInfo.VideoRange) && !_streamInfo.VideoRange.StartsWith("SDR", StringComparison.OrdinalIgnoreCase))
                quality.Add(_streamInfo.VideoRange);
            if (quality.Count > 0) chips.Add("🎞 " + string.Join(" · ", quality));

            if (_streamInfo.SizeBytes > 0) chips.Add("💾 " + FormatSize(_streamInfo.SizeBytes));
            if (!string.IsNullOrEmpty(_streamInfo.Container)) chips.Add(_streamInfo.Container.Split(",")[0].ToUpperInvariant());

            if (_streamInfo.Audio.Count > 0)
            {
                var langs = _streamInfo.Audio.Select(a => LangName(a.Language)).Where(n => !string.IsNullOrEmpty(n)).Distinct().Take(3);
                string ch = ChannelLabel(_streamInfo.AudioChannels);
                string audio = $"🔊 {string.Join(", ", langs)}";
                chips.Add(ch != null ? $"{audio} ({ch})" : audio);
            }

            if (_streamInfo.Subtitles.Count > 0)
            {
                var langs = _streamInfo.Subtitles.Select(s => LangName(s.Language)).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();
                string shown = string.Join(", ", langs.Take(3)) + (langs.Count > 3 ? $" +{langs.Count - 3}" : "");
                chips.Add($"💬 {(string.IsNullOrEmpty(shown) ? _streamInfo.Subtitles.Count + " Untertitel" : shown)}");
            }
        }

        InfoChips.Clear();
        foreach (var chip in chips) InfoChips.Add(chip);

        GenresText = details?.Genres is { Count: > 0 } g ? string.Join(" · ", g) : null;
        Overview = details?.Overview;
    }

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

        // Infos (Laenge, Qualitaet, Beschreibung ...) zuletzt laden, damit die Auswahl sofort steht
        await LoadInfoAsync();
    }

    [RelayCommand]
    public async Task StartDownloadAsync()
    {
        var options = new DownloadOptions { MediaSourceId = _streamInfo?.MediaSourceId, SourceSizeBytes = _streamInfo?.SizeBytes ?? 0 };

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
            SeriesId = SeriesId,
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






