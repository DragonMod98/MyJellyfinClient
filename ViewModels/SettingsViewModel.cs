using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyJellyfinClient.Services;

namespace MyJellyfinClient.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private string serverUrlInput;

    [ObservableProperty]
    private string userIdInput;

    [ObservableProperty]
    private string authTokenInput;

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        LoadSettingsAsync();
    }

    private async void LoadSettingsAsync()
    {
        ServerUrlInput = _settingsService.ServerUrl;
        UserIdInput = _settingsService.UserId;
        AuthTokenInput = await _settingsService.GetAuthTokenAsync();
    }

    [RelayCommand]
    public async Task SaveSettingsAsync()
    {
        _settingsService.ServerUrl = ServerUrlInput;
        _settingsService.UserId = UserIdInput;
        await _settingsService.SetAuthTokenAsync(AuthTokenInput);

        // Optional: Kleines visuelles Feedback
        await Shell.Current.DisplayAlert("Gespeichert", "Deine Jellyfin Zugangsdaten wurden gespeichert.", "OK");
    }
}