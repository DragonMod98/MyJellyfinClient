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
    private string backupServerUrlInput;

    [ObservableProperty]
    private string userIdInput;

    [ObservableProperty]
    private string authTokenInput;

    public IReadOnlyList<ChoiceOption> QualityChoices => DownloadDefaults.Qualities;
    public IReadOnlyList<ChoiceOption> AudioChoices => DownloadDefaults.AudioLanguages;
    public IReadOnlyList<ChoiceOption> SubtitleChoices => DownloadDefaults.SubtitleLanguages;

    [ObservableProperty]
    private bool autoDownloadNext;

    [ObservableProperty]
    private ChoiceOption selectedQuality;

    [ObservableProperty]
    private ChoiceOption selectedAudio;

    [ObservableProperty]
    private ChoiceOption selectedSubtitle;

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        AutoDownloadNext = _settingsService.AutoDownloadNextEpisode;
        SelectedQuality = QualityChoices.FirstOrDefault(c => c.Code == _settingsService.DefaultQuality) ?? DownloadDefaults.NoDefault;
        SelectedAudio = AudioChoices.FirstOrDefault(c => c.Code == _settingsService.DefaultAudioLanguage) ?? DownloadDefaults.NoDefault;
        SelectedSubtitle = SubtitleChoices.FirstOrDefault(c => c.Code == _settingsService.DefaultSubtitleLanguage) ?? DownloadDefaults.NoDefault;
        LoadSettingsAsync();
    }

    private async void LoadSettingsAsync()
    {
        ServerUrlInput = _settingsService.ServerUrl;
        BackupServerUrlInput = _settingsService.BackupServerUrl;
        UserIdInput = _settingsService.UserId;
        AuthTokenInput = await _settingsService.GetAuthTokenAsync();
    }

    [RelayCommand]
    public async Task SaveSettingsAsync()
    {
        _settingsService.ServerUrl = ServerUrlInput;
        _settingsService.BackupServerUrl = BackupServerUrlInput;
        _settingsService.UserId = UserIdInput;
        _settingsService.AutoDownloadNextEpisode = AutoDownloadNext;
        _settingsService.DefaultQuality = SelectedQuality?.Code;
        _settingsService.DefaultAudioLanguage = SelectedAudio?.Code;
        _settingsService.DefaultSubtitleLanguage = SelectedSubtitle?.Code;
        await _settingsService.SetAuthTokenAsync(AuthTokenInput);

        IPlatformApplication.Current?.Services.GetService<ServerConnectionService>()?.ResetCache();

        // Optional: Kleines visuelles Feedback
        await Shell.Current.DisplayAlert("Gespeichert", "Deine Jellyfin Zugangsdaten wurden gespeichert.", "OK");
    }
}


