using LibVLCSharp.Shared;
using MyJellyfinClient.Services;

namespace MyJellyfinClient.Views;

public partial class PlayerPage : ContentPage
{
    private readonly LocalDatabaseService _dbService;
    private readonly IBrightnessService _brightnessService;
    private readonly string _itemId;

    private LibVLC _libVLC;
    private LibVLCSharp.Shared.MediaPlayer _mediaPlayer;
    private IDispatcherTimer _trackingTimer;

    private readonly StorageManager _storageManager;
    private readonly AutoDownloadManager _autoDownloadManager;
    private readonly string _seriesId; // Muss beim Aufruf des Players übergeben werden, falls es eine Serie ist
    private bool _autoActionsTriggered = false; // Verhindert mehrfaches Triggern

    // Status der Gesten
    private float _currentBrightness = 0.5f;
    private int _startVolume;
    private long _startSeekTime;
    private bool _isHorizontalSwipe;

    public PlayerPage(string videoPath, string itemId, LocalDatabaseService dbService, IBrightnessService brightnessService)
    {
        InitializeComponent();
        _dbService = dbService;
        _brightnessService = brightnessService;
        _itemId = itemId;

        // VLC Initialisieren
        Core.Initialize();
        _libVLC = new LibVLC();
        _mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVLC);
        VideoPlayer.MediaPlayer = _mediaPlayer;

        // Lokale Datei laden (Für Online-Streams nutzt man FromType.FromLocation)
        var media = new Media(_libVLC, videoPath, FromType.FromPath);
        _mediaPlayer.Media = media;

        // Tracking Timer initialisieren
        _trackingTimer = Application.Current.Dispatcher.CreateTimer();
        _trackingTimer.Interval = TimeSpan.FromSeconds(5);
        _trackingTimer.Tick += TrackingTimer_Tick;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Letzten Offline-Fortschritt laden
        var item = await _dbService.GetMediaItemAsync(_itemId);
        if (item != null && item.ResumePositionTicks > 0)
        {
            // Jellyfin Ticks (10.000 Ticks = 1 ms) zu VLC Millisekunden konvertieren
            _mediaPlayer.Time = item.ResumePositionTicks / 10000;
        }

        _mediaPlayer.Play();
        _trackingTimer.Start();

        // Helligkeit initialisieren
        _brightnessService.SetBrightness(_currentBrightness);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _trackingTimer.Stop();

        // Letzten Stand sicher in die DB schreiben
        SaveProgressAsync().ConfigureAwait(false);

        _mediaPlayer.Stop();
        _mediaPlayer.Dispose();
        _libVLC.Dispose();
    }

    // --- Offline Progress Tracking ---
    private async void TrackingTimer_Tick(object sender, EventArgs e)
    {
        await SaveProgressAsync();
    }

    private async Task SaveProgressAsync()
    {
        if (_mediaPlayer.Time <= 0 || _mediaPlayer.Length <= 0) return;

        long currentTicks = _mediaPlayer.Time * 10000;
        long totalLengthTicks = _mediaPlayer.Length * 10000;

        // Ab 90% gilt der Film/die Folge als vollständig gesehen
        bool isWatched = currentTicks >= (totalLengthTicks * 0.9);

        await _dbService.UpdatePlaybackProgressAsync(_itemId, currentTicks, isWatched);

        if (isWatched && !_autoActionsTriggered)
        {
            _autoActionsTriggered = true;

            // TODO: Aktuelle Nutzer-Daten aus dem Auth-Service holen
            string currentUserId = "USER_ID_PLACEHOLDER";
            string currentAuthToken = "MediaBrowser Token=\"YOUR_TOKEN\"";

            // 1. Auto-Download der nächsten Folge anstoßen (falls es eine Serie ist)
            if (!string.IsNullOrEmpty(_seriesId))
            {
                await _autoDownloadManager.TriggerNextEpisodeDownloadAsync(_seriesId, currentUserId, currentAuthToken);
            }

            // 2. Den Cleanup anstoßen (Löscht diese Folge beim nächsten App-Start oder sofort im Hintergrund)
            // Hinweis: Wir rufen das hier nicht synchron mit 'await' auf, damit der Player nicht ruckelt.
            // Fire-and-Forget:
            _ = Task.Run(async () =>
            {
                // Kleiner Delay, damit die Datei nicht gelöscht wird, WÄHREND der VLC noch das Ende abspielt
                await Task.Delay(5000);
                await _storageManager.CleanupWatchedItemsAsync(currentUserId);
            });
        }
    }

    // --- Gestensteuerung (Linke Bildschirmhälfte) ---
    private void OnLeftScreenPanUpdated(object sender, PanUpdatedEventArgs e)
    {
        HandleGesture(e, isBrightness: true);
    }

    // --- Gestensteuerung (Rechte Bildschirmhälfte) ---
    private void OnRightScreenPanUpdated(object sender, PanUpdatedEventArgs e)
    {
        HandleGesture(e, isBrightness: false);
    }

    private void HandleGesture(PanUpdatedEventArgs e, bool isBrightness)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _startVolume = _mediaPlayer.Volume;
                _startSeekTime = _mediaPlayer.Time;
                _isHorizontalSwipe = false;
                break;

            case GestureStatus.Running:
                // Erkennung ob vertikales (Vol/Bright) oder horizontales (Seek) Wischen
                if (Math.Abs(e.TotalX) > Math.Abs(e.TotalY) && Math.Abs(e.TotalX) > 20)
                {
                    _isHorizontalSwipe = true;
                }

                if (_isHorizontalSwipe)
                {
                    // Horizontal = Spulen
                    long seekDelta = (long)(e.TotalX * 500); // 1px = 500ms
                    _mediaPlayer.Time = Math.Clamp(_startSeekTime + seekDelta, 0, _mediaPlayer.Length);
                    ShowOsd($"{(TimeSpan.FromMilliseconds(_mediaPlayer.Time)):hh\\:mm\\:ss}");
                }
                else
                {
                    // Vertikal = Helligkeit oder Lautstärke
                    if (isBrightness)
                    {
                        // Helligkeit anpassen (Y-Achse invertieren)
                        float brightDelta = (float)(e.TotalY * -0.003);
                        _currentBrightness = Math.Clamp(_currentBrightness + brightDelta, 0.01f, 1.0f);
                        _brightnessService.SetBrightness(_currentBrightness);
                        ShowOsd($"☀️ {(int)(_currentBrightness * 100)}%");
                    }
                    else
                    {
                        // Lautstärke anpassen
                        int volumeDelta = (int)(e.TotalY * -0.5);
                        _mediaPlayer.Volume = Math.Clamp(_startVolume + volumeDelta, 0, 100);
                        ShowOsd($"🔊 {_mediaPlayer.Volume}%");
                    }
                }
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                OsdContainer.IsVisible = false;
                break;
        }
    }

    private void ShowOsd(string text)
    {
        OsdText.Text = text;
        OsdContainer.IsVisible = true;
    }
}