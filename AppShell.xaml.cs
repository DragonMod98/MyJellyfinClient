namespace MyJellyfinClient;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Registriere Seiten, die nicht als feste Tabs existieren
        Routing.RegisterRoute(nameof(Views.MediaDetailPage), typeof(Views.MediaDetailPage));
        Routing.RegisterRoute(nameof(Views.PlayerPage), typeof(Views.PlayerPage));
    }
}