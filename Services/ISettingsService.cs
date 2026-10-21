namespace MyJellyfinClient.Services;

public interface ISettingsService
{
    string ServerUrl { get; set; }
    string BackupServerUrl { get; set; }
    string UserId { get; set; }

    // AuthTokens sollten asynchron über SecureStorage geladen werden
    Task<string> GetAuthTokenAsync();
    Task SetAuthTokenAsync(string token);
}