using LibVLCSharp.Shared;
using LibVLCSharp.Shared.Structures;
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
    private bool _autoActionsTriggered = false; // Verhindert mehrfaches Triggern

    // Status der Gesten
    private float _currentBrightness = 0.5f;

    // Parameter (videoPath, itemId) kommen per Shell-Navigation, nicht per Dependency Injection
    public PlayerPage(
        LocalDatabaseService dbService,
        IBrightnessService brightnessService,
        StorageManager storageManager,
        AutoDownloadManager autoDownloadManager,
        ISettingsService settings)
    {
        InitializeComponent();

        // Die eingebaute Spurliste des VLC-Controls ist hier nicht lesbar (dunkler Text auf dunklem Grund).
        // Stattdessen gibt es die eigene Auswahl ("Ton & Untertitel").
        if (VideoPlayer.PlaybackControls != null)
            VideoPlayer.PlaybackControls.IsTracksButtonVisible = false;
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
            double startSeconds = item.ResumePositionTicks / 10_000_000.0;
            _mediaPlayer.Media?.AddOption($":start-time={startSeconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}");
        }

        // Schwarzes Bild vermeiden: Die Video-Oberflaeche (Surface) entsteht asynchron. Startet VLC zu frueh,
        // laeuft nur der Ton. Daher nach dem Start pruefen, ob eine Videoausgabe existiert, sonst neu starten.
        _mediaPlayer.Playing += OnMediaPlaying;

        // Kurz warten, damit die Seite samt Video-Oberflaeche aufgebaut ist
        await Task.Delay(300);
        if (_playerReleased) return;

        _mediaPlayer.Play();
        _trackingTimer.Start();

        // Vollbild: Status- und Navigationsleiste ausblenden, Helligkeit vom aktuellen Wert aus steuern
        PipState.IsPlayerActive = true;
        PipState.ModeChanged += OnPipModeChanged;
        PipState.Closed += OnPipClosed;
        _brightnessService.SetImmersive(true);
        _currentBrightness = _brightnessService.GetBrightness();
    }

    private int _videoRecoveryAttempts;

    // Wird auf einem VLC-Thread aufgerufen: keine VLC-Aufrufe direkt hier, sondern in einem eigenen Task
    private void OnMediaPlaying(object sender, EventArgs e)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(1500);
            if (_playerReleased || _isClosing || _videoRecoveryAttempts >= 3) return;

            // Es gibt eine Videospur, aber keine Videoausgabe => schwarzes Bild
            if (_mediaPlayer.VideoTrackCount > 0 && _mediaPlayer.VoutCount == 0)
            {
                _videoRecoveryAttempts++;
                long position = _mediaPlayer.Time;
                System.Diagnostics.Debug.WriteLine($"Keine Videoausgabe, starte Wiedergabe neu (Versuch {_videoRecoveryAttempts})");

                _mediaPlayer.Stop();
                _mediaPlayer.Play();

                await Task.Delay(500);
                if (!_playerReleased && position > 0) _mediaPlayer.Time = position;
            }
        });
    }

    // Im kleinen PiP-Fenster keine Gesten-Flaechen/Controls einblenden
    private void OnPipModeChanged(bool inPip)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            GestureOverlay.IsVisible = !inPip;
            if (inPip) TracksButton.IsVisible = false;
            OsdContainer.IsVisible = false;
            if (inPip) VideoPlayer.PlaybackControls?.Hide();
        });
    }

    // PiP-Fenster wurde geschlossen: Wiedergabe beenden und Seite verlassen
    private void OnPipClosed()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try { await Shell.Current.GoToAsync(".."); } catch { }
        });
    }

    private bool _isClosing;
    private bool _playerReleased;

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        if (_isClosing) return;
        _isClosing = true;

        // Leisten wieder einblenden und Systemhelligkeit wiederherstellen
        PipState.IsPlayerActive = false;
        PipState.ModeChanged -= OnPipModeChanged;
        PipState.Closed -= OnPipClosed;
        _brightnessService.SetImmersive(false);
        _brightnessService.ResetBrightness();

        _trackingTimer.Stop();
        _trackingTimer.Tick -= TrackingTimer_Tick;
        _mediaPlayer.Playing -= OnMediaPlaying;

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

    // --- Doppeltippen: links 5 s zurueck, rechts 10 s vor ---
    private void SeekBy(long ms, string osd)
    {
        if (_playerReleased || _mediaPlayer.Length <= 0) return;
        _mediaPlayer.Time = Math.Clamp(_mediaPlayer.Time + ms, 0, _mediaPlayer.Length);
        ShowOsd(osd);
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(700), () => OsdContainer.IsVisible = false);
    }

    private void OnLeftDoubleTapped(object sender, TappedEventArgs e) => SeekBy(-5000, "⏪ -5 s");

    private void OnRightDoubleTapped(object sender, TappedEventArgs e) => SeekBy(10000, "⏩ +10 s");

    // Einfaches Tippen blendet die VLC-Controls und den Button fuer Ton/Untertitel ein
    private void OnScreenTapped(object sender, TappedEventArgs e)
    {
        VideoPlayer.PlaybackControls?.Show();
        ShowTracksButton();
    }

    private int _tracksButtonToken;

    private void ShowTracksButton()
    {
        TracksButton.IsVisible = true;
        int token = ++_tracksButtonToken;
        Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(4), () =>
        {
            if (token == _tracksButtonToken) TracksButton.IsVisible = false;
        });
    }

    private async void OnTracksClicked(object sender, EventArgs e)
    {
        _tracksButtonToken++; // Button bleibt sichtbar, solange ein Dialog offen ist
        if (_playerReleased) return;

        string kind = await DisplayActionSheet("Ton & Untertitel", "Abbrechen", null, "Tonspur", "Untertitel");
        if (_playerReleased) return;

        if (kind == "Tonspur")
            await PickTrackAsync("Tonspur", _mediaPlayer.AudioTrackDescription, _mediaPlayer.AudioTrack, id => _mediaPlayer.SetAudioTrack(id));
        else if (kind == "Untertitel")
            await PickTrackAsync("Untertitel", _mediaPlayer.SpuDescription, _mediaPlayer.Spu, id => _mediaPlayer.SetSpu(id));

        ShowTracksButton();
    }

    private async Task PickTrackAsync(string title, TrackDescription[] tracks, int currentId, Action<int> apply)
    {
        if (tracks == null || tracks.Length == 0)
        {
            await DisplayAlert(title, "Keine Spuren vorhanden.", "OK");
            return;
        }

        var names = tracks.Select((t, i) => $"{(t.Id == currentId ? "✓ " : string.Empty)}{TrackName(t)}").ToArray();

        // Gleiche Namen unterscheidbar machen, damit die Auswahl eindeutig zugeordnet werden kann
        if (names.Distinct().Count() != names.Length)
            names = names.Select((n, i) => $"{i + 1}. {n}").ToArray();

        string selected = await DisplayActionSheet(title, "Abbrechen", null, names);
        int index = Array.IndexOf(names, selected);
        if (index >= 0 && !_playerReleased)
            apply(tracks[index].Id);
    }

    private static string TrackName(TrackDescription track) =>
        track.Id == -1 ? "Aus" : string.IsNullOrWhiteSpace(track.Name) ? $"Spur {track.Id}" : track.Name;

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

    // Wischweg (in Einheiten), der dem gesamten Bereich 0-100 % entspricht (hoeher = unempfindlicher).
    // Berechnet wird immer vom Startwert der Geste aus.
    private const double VerticalRange = 400.0;

    private float _startBrightness;
    private float _startSystemVolume;

    private void HandleGesture(PanUpdatedEventArgs e, bool isBrightness)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _startBrightness = _brightnessService.GetBrightness();
                _startSystemVolume = _brightnessService.GetVolume();
                break;

            case GestureStatus.Running:
                // Nur vertikales Wischen (Helligkeit/Lautstaerke); kleine Bewegungen ignorieren
                if (Math.Abs(e.TotalY) > 10)
                {
                    float delta = (float)(-e.TotalY / VerticalRange);

                    if (isBrightness)
                    {
                        _currentBrightness = Math.Clamp(_startBrightness + delta, 0.01f, 1.0f);
                        _brightnessService.SetBrightness(_currentBrightness);
                        ShowOsd($"☀️ {(int)(_currentBrightness * 100)}%");
                    }
                    else
                    {
                        // Systemlautstaerke (Medien)
                        float volume = Math.Clamp(_startSystemVolume + delta, 0f, 1f);
                        _brightnessService.SetVolume(volume);
                        ShowOsd($"🔊 {(int)(volume * 100)}%");
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

