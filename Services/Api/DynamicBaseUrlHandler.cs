namespace MyJellyfinClient.Services.Api;

public class DynamicBaseUrlHandler : DelegatingHandler
{
    private readonly ISettingsService _settingsService;

    public DynamicBaseUrlHandler(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var serverUrl = _settingsService.ServerUrl;

        // Wenn eine Server-URL in den Settings hinterlegt ist, überschreiben wir das Ziel des Requests
        if (!string.IsNullOrEmpty(serverUrl) && serverUrl != "https://")
        {
            var baseUri = new Uri(serverUrl);
            var uriBuilder = new UriBuilder(request.RequestUri)
            {
                Host = baseUri.Host,
                Port = baseUri.Port,
                Scheme = baseUri.Scheme
            };
            request.RequestUri = uriBuilder.Uri;
        }

        return await base.SendAsync(request, cancellationToken);
    }
}