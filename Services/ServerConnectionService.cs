using Microsoft.Maui.Networking;

namespace MyJellyfinClient.Services;

public class ServerConnectionService
{
    private readonly ISettingsService _settings;
    private string _activeUrl;
    private DateTime _lastCheck = DateTime.MinValue;

    public ServerConnectionService(ISettingsService settings)
    {
        _settings = settings;
        Connectivity.Current.ConnectivityChanged += (s, e) => ResetCache();
    }

    public void ResetCache()
    {
        _activeUrl = null;
        _lastCheck = DateTime.MinValue;
    }

    public async Task<string> GetActiveServerUrlAsync()
    {
        // Cache für 5 Minuten behalten, um unnötige Pings zu vermeiden
        if (!string.IsNullOrEmpty(_activeUrl) && (DateTime.Now - _lastCheck).TotalMinutes < 5)
            return _activeUrl;

        // Wenn gar kein Internet da ist, sofort Offline melden
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            return null;

        var primary = _settings.ServerUrl;
        var backup = _settings.BackupServerUrl;

        // 1. Primäre URL (LAN) testen
        if (!string.IsNullOrEmpty(primary) && primary != "https://")
        {
            if (await PingServerAsync(primary))
            {
                _activeUrl = primary;
                _lastCheck = DateTime.Now;
                return _activeUrl;
            }
        }

        // 2. Backup URL (VPN) testen
        if (!string.IsNullOrEmpty(backup))
        {
            if (await PingServerAsync(backup))
            {
                _activeUrl = backup;
                _lastCheck = DateTime.Now;
                return _activeUrl;
            }
        }

        // Beide offline
        return null;
    }

    private async Task<bool> PingServerAsync(string url)
    {
        try
        {
            using var client = new HttpClient();
            // Extrem kurzer Timeout: Wenn LAN da ist, antwortet es in < 100ms. 
            // Nach 2 Sekunden warten wir nicht länger.
            client.Timeout = TimeSpan.FromSeconds(2);

            // Jellyfin System-Ping Endpunkt aufrufen
            var response = await client.GetAsync($"{url}/System/Info/Public");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}