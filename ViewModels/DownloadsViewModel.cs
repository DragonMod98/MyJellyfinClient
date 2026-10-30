using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyJellyfinClient.Models;
using MyJellyfinClient.Services;

namespace MyJellyfinClient.ViewModels;

public partial class DownloadsViewModel : ObservableObject
{
    private readonly LocalDatabaseService _dbService;
    private readonly ISettingsService _settingsService;

    private const string MoviesGroupName = "Filme";

    // Merkt sich den Auf-/Zuklapp-Zustand beim Neuladen der Liste
    private readonly HashSet<string> _expandedGroups = new();
    private bool _collapsedMovies;

    public ObservableCollection<DownloadGroup> DownloadGroups { get; } = new();

    private async Task DeleteItemsAsync(IList<LocalMediaItem> items, string question)
    {
        if (items.Count == 0) return;
        if (!await Shell.Current.DisplayAlert("Löschen", question, "Löschen", "Abbrechen")) return;

        // Offen gebliebenen Gesehen-Status vor dem Loeschen zu Jellyfin melden.
        // Klappt das nicht (offline), bleibt er in der DB und wird spaeter synchronisiert.
        var sync = IPlatformApplication.Current?.Services.GetService<SyncEngine>();
        if (sync != null)
        {
            try { await sync.SyncPendingItemsAsync(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Sync vor dem Loeschen fehlgeschlagen: {ex.Message}"); }
        }

        foreach (var item in items)
        {
            try
            {
                if (!string.IsNullOrEmpty(item.LocalFilePath) && File.Exists(item.LocalFilePath))
                    File.Delete(item.LocalFilePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Datei loeschen fehlgeschlagen: {ex.Message}");
            }
            await _dbService.DeleteMediaItemAsync(item.JellyfinItemId);
        }

        await LoadDownloadsAsync();
    }

    // Einzelne Folge / einzelner Film
    [RelayCommand]
    private Task DeleteItemAsync(LocalMediaItem item) =>
        item == null ? Task.CompletedTask : DeleteItemsAsync(new[] { item }, $"\"{item.Title}\" vom Gerät löschen?");

    // Alle Folgen derselben Staffel
    [RelayCommand]
    private Task DeleteSeasonAsync(LocalMediaItem item)
    {
        if (item == null) return Task.CompletedTask;
        var all = DownloadGroups.SelectMany(g => g.AllItems)
            .Where(i => i.SeriesName == item.SeriesName && i.SeasonName == item.SeasonName).ToList();
        string name = string.IsNullOrEmpty(item.SeasonName) ? item.SeriesName : $"{item.SeriesName} – {item.SeasonName}";
        return DeleteItemsAsync(all, $"{all.Count} Download(s) von \"{name}\" löschen?");
    }

    // Ganze Serie bzw. alle Filme
    [RelayCommand]
    private Task DeleteGroupAsync(DownloadGroup group) =>
        group == null ? Task.CompletedTask : DeleteItemsAsync(group.AllItems.ToList(), $"\"{group.Name}\" ({group.TotalCount} Download(s)) löschen?");

    [RelayCommand]
    private void ToggleGroup(DownloadGroup group)
    {
        if (group == null) return;

        group.Toggle();

        if (!group.IsSeries)
        {
            _collapsedMovies = !group.IsExpanded;
        }
        else if (group.IsExpanded)
        {
            _expandedGroups.Add(group.Name);
        }
        else
        {
            _expandedGroups.Remove(group.Name);
        }
    }

    [ObservableProperty]
    private string storageUsageText = "0 GB belegt";

    [ObservableProperty]
    private double storageProgress = 0.0;

    [ObservableProperty]
    private bool isLoading;

    public DownloadsViewModel(LocalDatabaseService dbService, ISettingsService settingsService, IDownloadManager downloadManager, IExternalPlayerService externalPlayer)
    {
        _externalPlayer = externalPlayer;
        _dbService = dbService;
        _settingsService = settingsService;
        _downloadManager = downloadManager;

        // Laufende Downloads anzeigen; bei Abschluss/Abbruch die fertige Liste neu laden
        DownloadTracker.Items.CollectionChanged += OnActiveDownloadsChanged;
        DownloadTracker.Changed += OnDownloadsChanged;
    }

    private readonly IDownloadManager _downloadManager;
    private readonly IExternalPlayerService _externalPlayer;

    public ObservableCollection<ActiveDownload> ActiveDownloads => DownloadTracker.Items;

    public bool HasActiveDownloads => DownloadTracker.Items.Count > 0;

    private void OnActiveDownloadsChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        => OnPropertyChanged(nameof(HasActiveDownloads));

    private void OnDownloadsChanged()
        => MainThread.BeginInvokeOnMainThread(() => LoadDownloadsCommand.Execute(null));

    [RelayCommand]
    private void CancelDownload(ActiveDownload download)
    {
        if (download != null)
            _downloadManager.Cancel(download.ItemId);
    }

    [RelayCommand]
    private async Task CancelAllDownloadsAsync()
    {
        if (await Shell.Current.DisplayAlert("Downloads abbrechen", "Alle laufenden und wartenden Downloads abbrechen?", "Ja", "Nein"))
            _downloadManager.CancelAll();
    }


    [RelayCommand]
    public async Task LoadDownloadsAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            string currentUserId = _settingsService.UserId;
            if (string.IsNullOrEmpty(currentUserId)) return;

            // 1. Liste der fertigen Downloads aus der DB laden
            var items = await _dbService.GetDownloadedItemsAsync(currentUserId);

            // Gruppieren: Serien als eigene (einklappbare) Gruppe, alles andere unter "Filme"
            var comparer = StringComparer.Create(System.Globalization.CultureInfo.CurrentCulture, System.Globalization.CompareOptions.NumericOrdering);

            var groups = new List<DownloadGroup>();

            foreach (var series in items
                .Where(i => i.IsSeries || !string.IsNullOrEmpty(i.SeriesName))
                .GroupBy(i => string.IsNullOrEmpty(i.SeriesName) ? "Unbekannte Serie" : i.SeriesName)
                .OrderBy(g => g.Key, comparer))
            {
                var ordered = series
                    .OrderBy(i => i.SeasonName ?? string.Empty, comparer)
                    .ThenBy(i => i.Title ?? string.Empty, comparer);
                groups.Add(new DownloadGroup(series.Key, true, ordered, _expandedGroups.Contains(series.Key)));
            }

            var movies = items
                .Where(i => !(i.IsSeries || !string.IsNullOrEmpty(i.SeriesName)))
                .OrderBy(i => i.Title ?? string.Empty, comparer)
                .ToList();
            if (movies.Count > 0)
            {
                // "Filme" ist standardmaessig aufgeklappt
                groups.Insert(0, new DownloadGroup(MoviesGroupName, false, movies, !_collapsedMovies));
            }

            DownloadGroups.Clear();
            foreach (var group in groups)
            {
                DownloadGroups.Add(group);
            }

            // 2. Speicherplatz berechnen und formatieren
            long totalBytes = await _dbService.GetTotalUsedStorageBytesAsync();
            double gigabytes = totalBytes / (1024.0 * 1024.0 * 1024.0);

            StorageUsageText = $"{Math.Round(gigabytes, 1)} GB belegt";

            // Fiktives Limit von 64GB für die App als 100% Balken-Ausschlag
            StorageProgress = Math.Min(1.0, gigabytes / 64.0);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task PlayOfflineAsync(LocalMediaItem selectedItem)
    {
        if (selectedItem == null || string.IsNullOrEmpty(selectedItem.LocalFilePath))
            return;

        // Sicherheitscheck, ob das System die Datei im Hintergrund gelöscht hat
        if (!File.Exists(selectedItem.LocalFilePath))
        {
            await Shell.Current.DisplayAlert("Fehler", "Die physische Videodatei wurde nicht gefunden.", "OK");
            return;
        }

        // Gewaehlte externe Player-App (Einstellungen): Video dort oeffnen statt im integrierten VLC-Player
        string playerPackage = _settingsService.PlayerPackage;
        if (playerPackage != PlayerChoice.BuiltIn)
        {
            if (_externalPlayer.Play(selectedItem.LocalFilePath, selectedItem.Title, playerPackage))
                return;

            await Shell.Current.DisplayAlert("Player", "Die gewählte Player-App konnte nicht gestartet werden. Es wird der integrierte Player verwendet.", "OK");
        }

        // Parameter vorbereiten: Wir übergeben dem Player den Pfad zur lokalen .mp4 Datei 
        // und die ID für das Offline-Tracking in der Datenbank.
        var navigationParameter = new Dictionary<string, object>
        {
            { "videoPath", selectedItem.LocalFilePath },
            { "itemId", selectedItem.JellyfinItemId }
        };

        await Shell.Current.GoToAsync(nameof(Views.PlayerPage), navigationParameter);
    }
}