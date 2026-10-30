namespace MyJellyfinClient.Services;

/// <summary>Spielt ein lokales Video mit einer externen Player-App des Geraets ab.</summary>
public interface IExternalPlayerService
{
    // Player-Apps, die Videodateien oeffnen koennen (Code = Paketname, Name = App-Name)
    IReadOnlyList<ChoiceOption> GetInstalledPlayers();

    // package: Paketname der App oder "system" fuer die Standard-App. Gibt false zurueck, wenn nichts gestartet werden konnte.
    bool Play(string filePath, string title, string package);
}

/// <summary>Feste Codes fuer die Player-Auswahl in den Einstellungen.</summary>
public static class PlayerChoice
{
    public const string BuiltIn = "";
    public const string System = "system";
}
