namespace MyJellyfinClient.Services;

public class SettingsService : ISettingsService
{
    public string ServerUrl
    {
        get => Preferences.Default.Get(nameof(ServerUrl), "https://");
        set => Preferences.Default.Set(nameof(ServerUrl), value?.TrimEnd('/'));
    }

    public string BackupServerUrl
    {
        get => Preferences.Default.Get(nameof(BackupServerUrl), string.Empty);
        set => Preferences.Default.Set(nameof(BackupServerUrl), value?.TrimEnd('/'));
    }

    public string UserId
    {
        get => Preferences.Default.Get(nameof(UserId), string.Empty);
        set => Preferences.Default.Set(nameof(UserId), value);
    }

    public bool AutoDownloadNextEpisode
    {
        get => Preferences.Default.Get(nameof(AutoDownloadNextEpisode), true);
        set => Preferences.Default.Set(nameof(AutoDownloadNextEpisode), value);
    }

    public string DefaultQuality
    {
        get => Preferences.Default.Get(nameof(DefaultQuality), string.Empty);
        set => Preferences.Default.Set(nameof(DefaultQuality), value ?? string.Empty);
    }

    public string PlayerPackage
    {
        get => Preferences.Default.Get(nameof(PlayerPackage), string.Empty);
        set => Preferences.Default.Set(nameof(PlayerPackage), value ?? string.Empty);
    }

    public string DefaultAudioLanguage
    {
        get => Preferences.Default.Get(nameof(DefaultAudioLanguage), string.Empty);
        set => Preferences.Default.Set(nameof(DefaultAudioLanguage), value ?? string.Empty);
    }

    public string DefaultSubtitleLanguage
    {
        get => Preferences.Default.Get(nameof(DefaultSubtitleLanguage), string.Empty);
        set => Preferences.Default.Set(nameof(DefaultSubtitleLanguage), value ?? string.Empty);
    }

    public async Task<string> GetAuthTokenAsync()
    {
        return await SecureStorage.Default.GetAsync("JellyfinAuthToken") ?? string.Empty;
    }

    public async Task SetAuthTokenAsync(string token)
    {
        if (string.IsNullOrEmpty(token))
            SecureStorage.Default.Remove("JellyfinAuthToken");
        else
            await SecureStorage.Default.SetAsync("JellyfinAuthToken", token);
    }
}
