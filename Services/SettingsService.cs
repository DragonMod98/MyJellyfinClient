namespace MyJellyfinClient.Services;

public class SettingsService : ISettingsService
{
    public string ServerUrl
    {
        get => Preferences.Default.Get(nameof(ServerUrl), "https://");
        set => Preferences.Default.Set(nameof(ServerUrl), value?.TrimEnd('/'));
    }

    public string UserId
    {
        get => Preferences.Default.Get(nameof(UserId), string.Empty);
        set => Preferences.Default.Set(nameof(UserId), value);
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