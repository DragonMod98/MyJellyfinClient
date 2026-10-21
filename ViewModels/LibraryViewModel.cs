using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyJellyfinClient.Services;
using MyJellyfinClient.Services.Api;
using static MyJellyfinClient.Services.Api.PlaybackProgressInfo;

namespace MyJellyfinClient.ViewModels;

public partial class LibraryViewModel : ObservableObject
{
    private readonly IJellyfinApiService _apiService;
    private readonly ISettingsService _settingsService;

    // Diese Liste wird automatisch an die XAML-UI gebunden
    public ObservableCollection<JellyfinItem> LibraryItems { get; } = new();

    [ObservableProperty]
    private bool isLoading;

    public LibraryViewModel(IJellyfinApiService apiService, ISettingsService settingsService)
    {
        _apiService = apiService;
        _settingsService = settingsService;
    }

    [RelayCommand]
    public async Task LoadLibraryAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        try
        {
            string userId = _settingsService.UserId;
            string token = await _settingsService.GetAuthTokenAsync();
            string serverUrl = _settingsService.ServerUrl;

            // Abbruch, wenn der Nutzer noch keine Settings hinterlegt hat
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token) || string.IsNullOrEmpty(serverUrl))
                return;

            var response = await _apiService.GetLibraryItemsAsync(token, userId);

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

    // Wird aufgerufen, wenn auf ein Film-Poster geklickt wird
    [RelayCommand]
    public async Task GoToDetailsAsync(JellyfinItem selectedItem)
    {
        if (selectedItem == null) return;

        // Übergibt die Film-ID und den Titel an die Detail-Seite
        var navigationParameter = new Dictionary<string, object>
        {
            { "ItemId", selectedItem.Id },
            { "Title", selectedItem.Name }
        };

        // Navigiere zur Detailseite
        await Shell.Current.GoToAsync(nameof(Views.MediaDetailPage), navigationParameter);
    }
}