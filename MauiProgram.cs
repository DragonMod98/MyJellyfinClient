using Microsoft.Extensions.Logging;
using LibVLCSharp.MAUI;
using MyJellyfinClient.Services;
using MyJellyfinClient.Services.Api;
using Refit;

namespace MyJellyfinClient
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseLibVLCSharp() // Registriert den Handler für LibVLCSharp.MAUI.VideoView
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    // Icon-Fonts für die LibVLCSharp-Player-Steuerung
                    fonts.AddFont("FontAwesome5Solid.otf", "FontAwesomeSolid");
                    fonts.AddFont("FontAwesome5Brands.otf", "FontAwesomeBrands");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif
            builder.Services.AddSingleton<ServerConnectionService>();
            builder.Services.AddSingleton<ISettingsService, SettingsService>();
            builder.Services.AddTransient<DynamicBaseUrlHandler>();

            builder.Services.AddSingleton<LocalDatabaseService>();
            builder.Services.AddRefitClient<IJellyfinApiService>()
               .ConfigureHttpClient(c => c.BaseAddress = new Uri("http://localhost")) // Dummy-URL, wird vom Handler überschrieben
               .AddHttpMessageHandler<DynamicBaseUrlHandler>();
            builder.Services.AddSingleton<SyncEngine>();
            builder.Services.AddSingleton<StorageManager>();
            builder.Services.AddSingleton<DownloadStarter>();
            builder.Services.AddSingleton<AutoDownloadManager>();

            builder.Services.AddTransient<MyJellyfinClient.ViewModels.LibraryViewModel>();
            builder.Services.AddTransient<MyJellyfinClient.ViewModels.SettingsViewModel>();
            builder.Services.AddTransient<MyJellyfinClient.ViewModels.MediaDetailViewModel>();
            builder.Services.AddTransient<MyJellyfinClient.ViewModels.DownloadsViewModel>();

            builder.Services.AddTransient<MyJellyfinClient.Views.LibraryPage>();
            builder.Services.AddTransient<MyJellyfinClient.Views.MediaDetailPage>();
            builder.Services.AddTransient<MyJellyfinClient.Views.SettingsPage>();
            builder.Services.AddTransient<MyJellyfinClient.Views.DownloadsPage>();
            builder.Services.AddTransient<MyJellyfinClient.Views.PlayerPage>();

#if ANDROID
            builder.Services.AddSingleton<MyJellyfinClient.Services.IDownloadManager, MyJellyfinClient.Platforms.Android.Services.AndroidDownloadManager>();
            builder.Services.AddSingleton<IBrightnessService, MyJellyfinClient.Platforms.Android.Services.AndroidBrightnessService>();
#endif

            var app = builder.Build();

            // Initialisiert die SyncEngine direkt beim App-Start, damit der Event-Listener aktiv ist
            var syncEngine = app.Services.GetService<SyncEngine>();

            // Offline angesehene Medien beim App-Start zu Jellyfin melden (falls Server erreichbar)
            _ = Task.Run(async () =>
            {
                try { await syncEngine?.SyncPendingItemsAsync(); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Start-Sync fehlgeschlagen: {ex.Message}"); }
            });

            return app;
        }
    }
}
