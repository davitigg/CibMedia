using Android.Content;
using CibMedia.Core.Abstractions;
using CibMedia.Core.Abstractions.LocalHttp;
using CibMedia.Core.Infrastructure;
using CibMedia.Core.Infrastructure.Playback;
using CibMedia.Core.Infrastructure.Tmdb;
using CibMedia.Core.Infrastructure.Updates;
using CibMedia.Core.Presentation.Playback;
using CibMedia.Core.Presentation.Remote;
using CibMedia.Playback;
using CibMedia.Playback.Services;
using CibMedia.AndroidTv.Leanback.Presenters;
using CibMedia.AndroidTv.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CibMedia.AndroidTv.App;

// The composition root: the one place a port is bound to a concrete implementation.
public static class AppServices
{
    private static IServiceProvider? _provider;

    public static IServiceProvider Provider =>
        _provider ?? throw new InvalidOperationException("AppServices.Initialise has not run.");

    public static void Initialise(Context context)
    {
        if (_provider is not null) return;

        _provider = Build(context.ApplicationContext ?? context);

        // The one cost a lookup cannot avoid paying on a cold process, paid here while nothing is
        // waiting on it. Not awaited and not observed: it changes no answer, and a launch does not
        // wait on an upstream.
        _ = _provider.GetRequiredService<PlaybackWarmupService>().WarmAsync(CancellationToken.None);
    }

    // The published manifest, on the branch a release is cut from. Config travels here and an
    // apk only ever travels as a release asset this names.
    private static readonly Uri ManifestUrl =
        new("https://raw.githubusercontent.com/davitigg/CibMedia/main/manifest.json");

    private static ServiceProvider Build(Context context)
    {
        var services = new ServiceCollection();

        // The noise is named, not the app: naming only the categories to keep left a trimmed build
        // mute, with nothing to read when playback failed on it.
        services.AddLogging(logging => logging
            .AddProvider(new LogcatLoggerProvider())
            .SetMinimumLevel(LogLevel.Debug)
            .AddFilter("Microsoft", LogLevel.Warning)
            .AddFilter("System", LogLevel.Warning));

        services.AddPlayback(PlaybackConfig.Read(context));

        var keys = new PreferencesApiKeys(context);
        services.AddSingleton<IApiKeys>(keys);

        var cache = new FileCacheStore(context);
        services.AddSingleton<ICacheStore>(cache);

        services.AddSingleton<IApiKeyCheck>(_ => new ApiKeyCheck());

        services.AddSingleton<TmdbClientSource>();
        services.AddSingleton<TmdbArtwork>();

        services.AddSingleton<ILocalHttpServer, HttpListenerServer>();

        // Two clients on purpose: the manifest is small and worth giving up on quickly, an apk
        // is tens of megabytes over whatever the box is on.
        services.AddSingleton<IAppUpdates>(sp => new AppUpdatesClient(
            new HttpClient { Timeout = TimeSpan.FromSeconds(10) },
            ManifestUrl,
            sp.GetRequiredService<ILogger<AppUpdatesClient>>()));
        services.AddSingleton<IAppUpdatePreferences>(_ => new PreferencesAppUpdates(context));
        services.AddSingleton(sp => new AppUpdateCheck(
            context,
            sp.GetRequiredService<IAppUpdates>(),
            sp.GetRequiredService<IAppUpdatePreferences>(),
            new HttpClient { Timeout = TimeSpan.FromMinutes(10) },
            sp.GetRequiredService<ILogger<AppUpdateCheck>>()));

        services.AddSingleton<ViewModelStore>();
        services.AddSingleton<RailPresenters>();
        services.AddSingleton<DetailPresenters>();

        services.AddSingleton(_ => new PreferencesUiScale(context));

        services.AddSingleton<IImageLoader>(_ => new GlideImageLoader(context));
        services.AddSingleton<IPlaybackHistory>(_ => new PreferencesPlaybackHistory(context));
        services.AddSingleton<IStreamProvider>(sp => ActivatorUtilities.CreateInstance<LocalStreamProvider>(sp));
        services.AddSingleton<WatchHistoryRecorder>();

        services.AddSingleton<IRemotePlaySwitch>(_ => new PreferencesRemotePlaySwitch(context));
        services.AddSingleton<IPlaybackPreferences>(_ => new PreferencesPlaybackPreferences(context));
        services.AddSingleton<IPlaybackProviders>(
            sp => ActivatorUtilities.CreateInstance<LocalPlaybackProviders>(sp));
        services.AddSingleton<IPlaybackCache>(sp => ActivatorUtilities.CreateInstance<LocalPlaybackCache>(sp));
        services.AddSingleton<PlaybackController>();
        services.AddSingleton<PlaybackEndpointHost>();

        services.AddSingleton<ICatalog>(sp => new CachedCatalog(
            ActivatorUtilities.CreateInstance<TmdbCatalog>(sp),
            sp.GetRequiredService<ICacheStore>()));

        var provider = services.BuildServiceProvider();

        if (context is Application application)
            provider.GetRequiredService<PlaybackEndpointHost>().Attach(application);

        return provider;
    }
}
