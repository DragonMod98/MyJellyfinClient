namespace MyJellyfinClient.Services;

/// <summary>Zustand fuer Bild-im-Bild (PiP): Player aktiv, PiP-Wechsel, PiP-Fenster geschlossen.</summary>
public static class PipState
{
    public static bool IsPlayerActive { get; set; }
    public static bool IsInPip { get; private set; }

    public static event Action<bool> ModeChanged;
    public static event Action Closed;

    public static void RaiseModeChanged(bool inPip)
    {
        IsInPip = inPip;
        ModeChanged?.Invoke(inPip);
    }

    public static void RaiseClosed() => Closed?.Invoke();
}
