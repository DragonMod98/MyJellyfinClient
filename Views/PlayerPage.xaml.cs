using LibVLCSharp.Shared;
using MyJellyfinClient.Services;

namespace MyJellyfinClient.Views;

public partial class PlayerPage : ContentPage, IQueryAttributable
{
    private readonly LocalDatabaseService _dbService;
    private readonly IBrightnessService _brightnessService;
    private readonly ISettingsService _settings;
    private string _itemId;

    private LibVLC _libVLC;
    private LibVLCSharp.Shared.MediaPlayer _mediaPlayer;
    private IDispatcherTimer _trackingTimer;

    private readonly StorageManager _storageManager;
    private readonly AutoDownloadManager _autoDownloadManager;
    private string _seriesId; // Wird optional per Query-Parameter "seriesId" übergeben
    private bool _autoActionsTriggered = false; // Verhindert mehrfaches Triggern

    // Status der Gesten
    private float _currentBrightness = 0.5f;
    private int _startVolume;
    private long _startSeekTime;
    private bool _isHorizontalSwipe;

    // Parameter (videoPath, itemId) kommen per Shell-Navigation, nicht per Dependency Injection
    public PlayerPage(
        LocalDatabaseService dbService,
        IBrightnessService brightnessService,
        StorageManager storageManager,
        AutoDownloadManager autoDownloadManager,
        ISettingsService settings)
    {
        InitializeComponent();
        _dbService = dbService;
        _brightnessService = brightnessService;
        _storageManager = storageManager;
        _autoDownloadManager = autoDownloadManager;
        _settings = settings;

        // VLC Initialisieren
        Core.Initialize();
        _libVLC = new LibVLC();
        _mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVLC);
        VideoPlayer.MediaPlayer = _mediaPlayer;

        // Tracking Timer initialisieren
        _trackingTimer = Application.Current.Dispatcher.CreateTimer();
        _trackingTimer.Interval = TimeSpan.FromSeconds(5);
        _trackingTimer.Tick += TrackingTimer_Tick;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("itemId", out var id))
            _itemId = id?.ToString();

        if (query.TryGetValue("seriesId", out var sid))
            _seriesId = sid?.ToString();

        if (query.TryGetValue("videoPath", out var path) && path is string videoPath)
        {
            // Lokale Datei laden (Für Online-Streams nutzt man FromType.FromLocation)
            _mediaPlayer.Media = new Media(_libVLC, videoPath, FromType.FromPath);
        }
    }

    // Zurück-Geste/-Taste: zurück zur vorherigen Seite (Downloads) statt App beenden
    protected override bool OnBackButtonPressed()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try { await Shell.Current.GoToAsync(".."); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Zurück-Navigation fehlgeschlagen: {ex.Message}"); }
        });
        return true;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Letzten Offline-Fortschritt laden
        var item = await _dbService.GetMediaItemAsync(_itemId);
        if (item != null && item.ResumePositionTicks > 0 && !item.IsWatched)
        {
            // Jellyfin Ticks (10.000.000 Ticks = 1 s). Ein Setzen von Time VOR dem Abspielen
            // wird von VLC ignoriert, daher als Start-Option an das Medium haengen.
            double startSeconds = item.ResumePositionTicks / 10_000_000.0;
            _mediaPlayer.Media?.AddOption($":start-time={startSeconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}");
        }

        _mediaPlayer.Play();
        _trackingTimer.Start();

        // Helligkeit initialisieren
        _brightnessService.SetBrightness(_currentBrightness);
    }

    private bool _isClosing;
    private bool _playerReleased;

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        if (_isClosing) return;
        _isClosing = true;

        _trackingTimer.Stop();
        _trackingTimer.Tick -= TrackingTimer_Tick;

        var player = _mediaPlayer;
        var libVlc = _libVLC;

        // Fortschritt sichern, solange der Player noch gültig ist
        try { await SaveProgressAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Fortschritt speichern fehlgeschlagen: {ex.Message}"); }

        // Gesehen-Status/Position direkt an Jellyfin melden (bei fehlender Verbindung spaeter per Sync)
        var sync = IPlatformApplication.Current?.Services.GetService<SyncEngine>();
        if (sync != null)
        {
            _ = Task.Run(async () =>
            {
                try { await sync.SyncPendingItemsAsync(); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Sync fehlgeschlagen: {ex.Message}"); }
            });
        }

        // Das MediaPlayerElement darf den Player nicht mehr verwenden (sonst nativer VLC-Crash)
        VideoPlayer.MediaPlayer = null;
        _playerReleased = true;

        // Native Objekte nicht auf dem UI-Thread freigeben
        _ = Task.Run(() =>
        {
            try
            {
                player.Stop();
                player.Dispose();
                libVlc.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"VLC Aufräumen fehlgeschlagen: {ex.Message}");
            }
        });
    }

    // --- Offline Progress Tracking ---
    private async void TrackingTimer_Tick(object sender, EventArgs e)
    {
        await SaveProgressAsync();
    }

    private async Task SaveProgressAsync()
    {
        if (_playerReleased) return;
        if (_mediaPlayer.Time <= 0 || _mediaPlayer.Length <= 0) return;

        long currentTicks = _mediaPlayer.Time * 10000;
        long totalLengthTicks = _mediaPlayer.Length * 10000;

        // Ab 90% gilt der Film/die Folge als vollständig gesehen
        bool isWatched = currentTicks >= (totalLengthTicks * 0.9);

        await _dbService.UpdatePlaybackProgressAsync(_itemId, currentTicks, isWatched, totalLengthTicks);

        if (isWatched && !_autoActionsTriggered)
        {
            _autoActionsTriggered = true;

            string currentUserId = _settings.UserId;
            // Auto-Download der naechsten Folge passiert nach dem Sync zu Jellyfin (SyncEngine/AutoDownloadManager).
            // Den Cleanup anstoßen (Löscht diese Folge beim nächsten App-Start oder sofort im Hintergrund)
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

    // --- Tippen blendet die VLC-Controls ein (blenden sich selbst wieder aus) ---
    private void OnScreenTapped(object sender, TappedEventArgs e)
    {
        VideoPlayer.PlaybackControls?.Show();
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
