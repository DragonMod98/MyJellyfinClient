namespace MyJellyfinClient.Services;

public interface ISettingsService
{
    string ServerUrl { get; set; }
    string BackupServerUrl { get; set; }
    string UserId { get; set; }

    // Download-Vorgaben ("" = keine Vorgabe; Untertitel zusaetzlich "none")
    string DefaultQuality { get; set; }
    bool AutoDownloadNextEpisode { get; set; } // Naechste Folge nach dem Schauen automatisch laden
    string DefaultAudioLanguage { get; set; }
    string DefaultSubtitleLanguage { get; set; }

    // Wiedergabe: "" = integrierter VLC-Player, "system" = Standard-App des Geraets, sonst Paketname der Player-App
    string PlayerPackage { get; set; }

    // AuthTokens sollten asynchron über SecureStorage geladen werden
    Task<string> GetAuthTokenAsync();
    Task SetAuthTokenAsync(string token);
}
