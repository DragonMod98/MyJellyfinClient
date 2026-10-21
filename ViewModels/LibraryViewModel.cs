using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyJellyfinClient.Services;
using MyJellyfinClient.Services.Api;
using System.Collections.ObjectModel;
using static MyJellyfinClient.Services.Api.PlaybackProgressInfo;

namespace MyJellyfinClient.ViewModels;

public partial class LibraryViewModel : ObservableObject
{
    private readonly Stack<string> _navigationHistory = new();
    private readonly List<string> _folderPath = new(); // Namen der geoeffneten Ordner (Bibliothek > Serie > Staffel)
    private readonly IJellyfinApiService _apiService;
    private readonly ISettingsService _settingsService;
    private readonly ServerConnectionService _connectionService;
    private readonly DownloadStarter _downloadStarter;

    // Diese Liste wird automatisch an die XAML-UI gebunden
    public ObservableCollection<JellyfinItem> LibraryItems { get; } = new();

    [ObservableProperty]
    private string currentParentId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInSubFolder))]
    [NotifyPropertyChangedFor(nameof(CanDownloadFolder))]
    private bool isRootLevel = true;

    // Zurück-Button nur sichtbar, wenn man in einem Unterordner ist
    public bool IsInSubFolder => !IsRootLevel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDownloadFolder))]
    private bool hasDownloadableItems;

    // "Alle herunterladen" nur in Ordnern, die Videos (z.B. Folgen) enthalten
    public bool CanDownloadFolder => IsInSubFolder && HasDownloadableItems;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isOfflineMode;

    public LibraryViewModel(IJellyfinApiService apiService, ISettingsService settingsService, DownloadStarter downloadStarter)
    {
        _apiService = apiService;
        _settingsService = settingsService;
        _downloadStarter = downloadStarter;
        _connectionService = IPlatformApplication.Current?.Services.GetService<ServerConnectionService>();
    }

    public async Task InitializeAsync()
    {
        _navigationHistory.Clear();
        _folderPath.Clear();
        await LoadLibraryAsync();
    }

    [RelayCommand]
    public async Task LoadLibraryAsync(string? parentId = null)
    {
        if (IsLoading) return;

        IsLoading = true;
        IsOfflineMode = false;
        try
        {
            CurrentParentId = parentId;
            IsRootLevel = _navigationHistory.Count == 0;

            // Alte Kacheln sofort ausblenden, damit der Lade-Indikator sichtbar ist
            LibraryItems.Clear();
            HasDownloadableItems = false;

            string userId = _settingsService.UserId;
            string token = await _settingsService.GetAuthTokenAsync();
            string serverUrl = await _connectionService.GetActiveServerUrlAsync();
            string authHeader = $"MediaBrowser Token=\"{token}\"";

            // Abbruch, wenn der Nutzer noch keine Settings hinterlegt hat
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
                return;

            // Zugangsdaten vorhanden, aber Server nicht erreichbar => Offline-Modus
            if (string.IsNullOrEmpty(serverUrl))
            {
                await HandleOfflineAsync();
                return;
            }

            var response = new JellyfinItemsResponse();

            if (string.IsNullOrEmpty(parentId))
            {
                response = await _apiService.GetLibraryItemsAsync(authHeader, userId);
            }
            else
            {
                response = await _apiService.GetFolderItemsAsync(authHeader, userId, parentId);
            }


            LibraryItems.Clear();
            if (response?.Items != null)
            {
                foreach (var item in response.Items)
                {
                    // Baue die direkte URL zum Poster-Bild des Films zusammen
                    // fillHeight/fillWidth spart extrem viel Datenvolumen!
                    item.ImageUrl = $"{serverUrl}/Items/{item.Id}/Images/Primary?fillHeight=300&fillWidth=200&quality=90";
                    LibraryItems.Add(item);
                }
            }

            HasDownloadableItems = LibraryItems.Any(i => !(i.IsFolder || i.Type == "CollectionFolder" || i.Type == "Folder"));
        }
        catch (HttpRequestException ex) when (ex.Message == "OfflineMode")
        {
            await HandleOfflineAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler beim Laden der Bibliothek: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool _offlineRedirectDone;

    private async Task HandleOfflineAsync()
    {
        IsOfflineMode = true; // Setzt das UI auf Offline
        LibraryItems.Clear();

        // Beim App-Start einmalig direkt zu den Downloads wechseln
        if (!_offlineRedirectDone)
        {
            _offlineRedirectDone = true;
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (Shell.Current != null)
                    await Shell.Current.GoToAsync("//DownloadsPage");
            });
        }
    }

    [RelayCommand]
    private async Task GoBackAsync()
    {
        if (_navigationHistory.Count > 0)
        {
            // Vorherige ParentId vom Stack holen und neu laden
            string previousParentId = _navigationHistory.Pop();
            if (_folderPath.Count > 0)
                _folderPath.RemoveAt(_folderPath.Count - 1);
            await LoadLibraryAsync(previousParentId);
        }
    }

    // Serien-/Staffelname aus Jellyfin-Metadaten, sonst aus der Ordnerstruktur
    // (Bibliothek > Serie > "Staffel 1" > Folgen). Gibt (series, season) zurueck.
    private (string Series, string Season) ResolveSeriesInfo(JellyfinItem item, bool forceSeries)
    {
        string series = item.SeriesName;
        string season = item.SeasonName;

        if (string.IsNullOrEmpty(series))
        {
            if (_folderPath.Count >= 3)
            {
                series = _folderPath[^2];
                season ??= _folderPath[^1];
            }
            else if (forceSeries && _folderPath.Count >= 1)
            {
                series = _folderPath[^1];
            }
        }

        return (series, season);
    }

    private DownloadRequest BuildRequest(JellyfinItem item, bool forceSeries)
    {
        var (series, season) = ResolveSeriesInfo(item, forceSeries);
        return new DownloadRequest
        {
            ItemId = item.Id,
            Title = item.DisplayTitle,
            SeriesName = series,
            SeasonName = season,
            IsSeries = !string.IsNullOrEmpty(series) || item.Type == "Episode",
            RunTimeTicks = item.RunTimeTicks ?? 0
        };
    }

    // Laedt alle Videos (z.B. alle Folgen einer Staffel) des aktuell geoeffneten Ordners
    [RelayCommand]
    public async Task DownloadFolderAsync()
    {
        var items = LibraryItems
            .Where(i => !(i.IsFolder || i.Type == "CollectionFolder" || i.Type == "Folder"))
            .ToList();

        if (items.Count == 0) return;

        string folderName = _folderPath.Count > 0 ? _folderPath[^1] : "Ordner";
        bool confirm = await Shell.Current.DisplayAlert(
            "Alle herunterladen",
            $"{items.Count} Titel aus \"{folderName}\" herunterladen?",
            "Ja", "Abbrechen");
        if (!confirm) return;

        var requests = items.Select(i => BuildRequest(i, forceSeries: true)).ToList();
        int started = await _downloadStarter.StartAsync(requests);

        string message = started switch
        {
            < 0 => "Server nicht erreichbar.",
            0 => "Alle Titel sind bereits heruntergeladen.",
            _ => $"{started} Download(s) gestartet. Sie werden nacheinander geladen, den Fortschritt siehst du in der Benachrichtigung."
        };
        await Shell.Current.DisplayAlert("Download", message, "OK");
    }

    // Wird aufgerufen, wenn auf ein Film-Poster geklickt wird
    [RelayCommand]
    public async Task GoToDetailsAsync(JellyfinItem selectedItem)
    {
        if (selectedItem == null) return;

        if (selectedItem.IsFolder || selectedItem.Type == "CollectionFolder" || selectedItem.Type == "Folder")
        {
            _navigationHistory.Push(CurrentParentId);
            _folderPath.Add(selectedItem.Name);
            await LoadLibraryAsync(selectedItem.Id);
        }
        else
        {
            var request = BuildRequest(selectedItem, forceSeries: false);

            // Uebergibt die Film-ID, den Titel und die Serien-Metadaten an die Detail-Seite
            var navigationParameter = new Dictionary<string, object>
            {
                { "ItemId", selectedItem.Id },
                { "Title", request.Title },
                { "SeriesName", request.SeriesName ?? string.Empty },
                { "SeasonName", request.SeasonName ?? string.Empty },
                { "IsSeries", request.IsSeries ? "true" : "false" },
                { "RunTimeTicks", request.RunTimeTicks.ToString() }
            };

            // Navigiere zur Detailseite
            await Shell.Current.GoToAsync(nameof(Views.MediaDetailPage), navigationParameter);
        }
    }
}