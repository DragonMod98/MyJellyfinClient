namespace MyJellyfinClient.Services;

/// <summary>
/// Fragt bei der Zurück-Geste auf den Hauptseiten nach, ob die App geschlossen werden soll.
/// </summary>
public static class ExitConfirmation
{
    private static bool _isAsking;

    public static async Task AskAsync(Page page)
    {
        // Mehrfaches Wischen soll nicht mehrere Dialoge öffnen
        if (_isAsking) return;
        _isAsking = true;

        try
        {
            bool exit = await page.DisplayAlert(
                "App beenden",
                "Möchtest du die App schließen?",
                "Ja",
                "Nein");

            if (exit)
            {
                Application.Current?.Quit();
            }
        }
        finally
        {
            _isAsking = false;
        }
    }
}
