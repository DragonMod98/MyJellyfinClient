using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyJellyfinClient.Services;
using MyJellyfinClient.Services.Api;
using System.Collections.ObjectModel;
using System.Text.Json;
using static MyJellyfinClient.Models.LocalMediaItem;

namespace MyJellyfinClient.ViewModels;

public partial class MediaDetailViewModel : ObservableObject
{
    private readonly IJellyfinApiService _apiService;
    private readonly IDownloadManager _downloadManager;
    private readonly ISettingsService _settings;
    private readonly LocalDatabaseService _dbService;

    // UI Bindings
    [ObservableProperty]
    private string mediaTitle;

    [ObservableProperty]
    private string jellyfinItemId;

    public ObservableCollection<string> AvailableQualities { get; } = new();

    [ObservableProperty]
    private string selectedQuality;

    public MediaDetailViewModel(IJellyfinApiService apiService, IDownloadManager downloadManager, LocalDatabaseService dbService, ISettingsService settings)
    {
        _apiService = apiService;
        _downloadManager = downloadManager;
        _dbService = dbService;
        _settings = settings;
    }

    [RelayCommand]
    public async Task FetchDownloadOptionsAsync()
    {
        string token = await _settings.GetAuthTokenAsync();
        string currentUserId = _settings.UserId;
        string baseUrl = $"{_settings.ServerUrl}/Items/{JellyfinItemId}/Download?api_key={token}";

        try
        {
            // Manifest vom Server holen
            var playbackInfoJson = await _apiService.GetPlaybackInfoAsync(token, JellyfinItemId, currentUserId);

            // Simples JSON Parsing für die verfügbaren MediaSources
            using var doc = JsonDocument.Parse(playbackInfoJson);
            var mediaSources = doc.RootElement.GetProperty("MediaSources");

            AvailableQualities.Clear();
            AvailableQualities.Add("Original (Direct Play)");
            AvailableQualities.Add("1080p - 10 Mbps (Transcode)");
            AvailableQualities.Add("720p - 4 Mbps (Transcode)");

            SelectedQuality = AvailableQualities.First();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler beim Laden der Optionen: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task StartDownloadAsync()
    {
        string token = await _settings.GetAuthTokenAsync();
        string currentUserId = _settings.UserId;
        string baseUrl = $"{_settings.ServerUrl}/Items/{JellyfinItemId}/Download?api_key={token}";

        // Transcoding-Parameter anhängen, falls nicht "Original" gewählt wurde
        if (SelectedQuality.Contains("1080p"))
        {
            baseUrl += "&VideoBitrate=10000000&Width=1920";
        }
        else if (SelectedQuality.Contains("720p"))
        {
            baseUrl += "&VideoBitrate=4000000&Width=1280";
        }

        // In die lokale Datenbank eintragen, dass der Download startet
        var localItem = new Models.LocalMediaItem
        {
            JellyfinItemId = this.JellyfinItemId,
            Title = this.MediaTitle,
            ProfileId = currentUserId,
            IsDownloaded = false // Wird vom Background-Service auf true gesetzt, wenn fertig
        };
        await _dbService.SaveOrUpdateMediaItemAsync(localItem);

        // An den Android Foreground Service übergeben
        _downloadManager.StartDownload(JellyfinItemId, baseUrl, MediaTitle);
    }
}