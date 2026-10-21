using MyJellyfinClient.Services;

namespace MyJellyfinClient.Services.Api;

public class DynamicBaseUrlHandler : DelegatingHandler
{
    private readonly ServerConnectionService _connectionService;

    public DynamicBaseUrlHandler(ServerConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Welche URL ist gerade aktiv? (LAN oder VPN)
        var activeUrl = await _connectionService.GetActiveServerUrlAsync();

        if (string.IsNullOrEmpty(activeUrl))
        {
            // Verhindert, dass der Request überhaupt ins Leere läuft und wirft direkt einen Fehler,
            // den wir in unserem ViewModel als "Offline-Modus" abfangen können.
            throw new HttpRequestException("OfflineMode");
        }

        var baseUri = new Uri(activeUrl);
        var uriBuilder = new UriBuilder(request.RequestUri)
        {
            Host = baseUri.Host,
            Port = baseUri.Port,
            Scheme = baseUri.Scheme
        };
        request.RequestUri = uriBuilder.Uri;

        return await base.SendAsync(request, cancellationToken);
    }
}