using Microsoft.Extensions.Logging;
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
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            builder.Services.AddSingleton<ISettingsService, SettingsService>();
            builder.Services.AddTransient<DynamicBaseUrlHandler>();

            builder.Services.AddSingleton<LocalDatabaseService>();
            builder.Services.AddRefitClient<IJellyfinApiService>()
               .ConfigureHttpClient(c => c.BaseAddress = new Uri("http://localhost")) // Dummy-URL, wird vom Handler überschrieben
               .AddHttpMessageHandler<DynamicBaseUrlHandler>();
            builder.Services.AddSingleton<SyncEngine>();
            builder.Services.AddSingleton<StorageManager>();
            builder.Services.AddSingleton<AutoDownloadManager>();

            builder.Services.AddTransient<MyJellyfinClient.ViewModels.LibraryViewModel>();
            builder.Services.AddTransient<MyJellyfinClient.ViewModels.SettingsViewModel>();
            builder.Services.AddTransient<MyJellyfinClient.ViewModels.MediaDetailViewModel>();

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
            app.Services.GetService<SyncEngine>();

            return app;
        }
    }
}
