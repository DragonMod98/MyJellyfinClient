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

    public DownloadsViewModel(LocalDatabaseService dbService, ISettingsService settingsService)
    {
        _dbService = dbService;
        _settingsService = settingsService;
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