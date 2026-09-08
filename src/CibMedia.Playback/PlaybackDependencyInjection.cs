using System.Diagnostics.CodeAnalysis;
using System.Net;
using Microsoft.Extensions.Caching.Memory;
using CibMedia.Playback.Models;
using CibMedia.Playback.Providers;
using CibMedia.Playback.Providers.Liveball;
using CibMedia.Playback.Providers.VideoDb;
using CibMedia.Playback.Providers.Xpass;
using CibMedia.Playback.Services;

namespace CibMedia.Playback;

public static class PlaybackDependencyInjection
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(4);

    private static string Required(string value, string name)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{name} is required.")
            : value;
    }

    private static IReadOnlyList<string> RequiredList(IReadOnlyList<string> values, string name)
    {
        if (values is []) throw new InvalidOperationException($"{name} must list at least one value.");

        return values.Any(string.IsNullOrWhiteSpace)
            ? throw new InvalidOperationException($"{name} contains a blank value.")
            : values;
    }

    // Configuration can arrive from off the box, and a page is read with the box's own address.
    private static IReadOnlyList<Uri> RequiredHttpsUrls(IReadOnlyList<string> values, string name)
    {
        return
        [
            .. RequiredList(values, name)
                .Select(value =>
                    Uri.TryCreate(value, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps
                        ? url
                        : throw new InvalidOperationException($"{name} contains '{value}', which is not https."))
        ];
    }

    extension(IServiceCollection services)
    {
        public IServiceCollection AddPlayback(PlaybackOptions options)
        {
            services.AddMemoryCache();

            services.AddVideoDb(options);
            services.AddXpass(options);
            services.AddLiveball(options);

            // Order is the order sources are offered to the player, and the order they are advertised.
            services.AddTmdbProvider<VideoDbPlaybackProvider>();
            services.AddTmdbProvider<XpassPlaybackProvider>();

            // Composed here rather than by the container: a stack is this assembly's own, so a
            // constructor taking one cannot be public.
            services.AddScoped(scope => new TmdbPlaybackService(
                scope.GetServices<ITmdbPlaybackProvider>(),
                scope.GetRequiredService<ILogger<TmdbPlaybackService>>()));

            services.AddScoped(scope => new PlaybackProvidersService(scope.GetServices<ITmdbPlaybackProvider>()));

            services.AddSingleton(scope => new PlaybackCacheService(scope.GetRequiredService<IMemoryCache>()));

            return services;
        }

        private void AddVideoDb(PlaybackOptions options)
        {
            services.AddSingleton(
                new VideoDbOptions(
                    options.VideoDb.Enabled,
                    RequiredList(options.VideoDb.Hosts, "videoDb.hosts")));

            services.AddUpstreamClient<VideoDbClient>();
        }

        private void AddXpass(PlaybackOptions options)
        {
            services.AddSingleton(
                new XpassOptions(
                    options.Xpass.Enabled,
                    Required(options.Xpass.PlayerHost, "xpass.playerHost"),
                    Required(options.Xpass.SubtitleHost, "xpass.subtitleHost")));

            services.AddUpstreamClient<XpassClient>();
            services.AddUpstreamClient<XpassBuildIdResolver>();
            services.AddUpstreamClient<XpassStreamResolver>();
            services.AddUpstreamClient<XpassSubtitleClient>();
        }

        private void AddLiveball(PlaybackOptions options)
        {
            services.AddSingleton(
                new LiveballOptions(
                    options.Liveball.Enabled,
                    RequiredList(options.Liveball.PageHosts, "liveball.pageHosts"),
                    RequiredHttpsUrls(options.Liveball.KeyPages, "liveball.keyPages")));
            services.AddSingleton<LiveballPageUrls>();

            // Cloudflare scores the connection, not only the request: .NET's HTTP/2 handshake is
            // fingerprinted and challenged where the same request over 1.1 is served, and the
            // Accept headers of a page load are part of what passes.
            services.AddUpstreamClient<LiveballClient>(client =>
            {
                client.DefaultRequestHeaders.Accept.ParseAdd("*/*");
                client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
                client.DefaultRequestVersion = HttpVersion.Version11;
                client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            });

            services.AddScoped<LiveballKeyResolver>();

            // Composed here rather than by the container: the stack it takes is this assembly's
            // own, so the constructor that takes it cannot be public.
            services.AddScoped(scope => new LiveballPlaybackService(
                scope.GetRequiredService<LiveballClient>(),
                scope.GetRequiredService<LiveballKeyResolver>(),
                scope.GetRequiredService<LiveballPageUrls>(),
                scope.GetRequiredService<LiveballOptions>(),
                scope.GetRequiredService<IMemoryCache>(),
                scope.GetRequiredService<ILogger<LiveballPlaybackService>>()));
        }

        // Both registrations resolve back to one instance: scoped counts per registration, so
        // registering the class twice would put two of the same stack in one scope.
        private void AddTmdbProvider<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            TProvider>()
            where TProvider : class, ITmdbPlaybackProvider
        {
            services.AddScoped<TProvider>();
            services.AddScoped<ITmdbPlaybackProvider>(scope => scope.GetRequiredService<TProvider>());
        }

        // Rotation is off because PooledConnectionLifetime picks up DNS changes without dropping the
        // pool.
        private IHttpClientBuilder AddUpstreamClient<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            TClient>(Action<HttpClient>? configure = null)
            where TClient : class
        {
            return services.AddHttpClient<TClient>(client =>
                {
                    client.Timeout = RequestTimeout;
                    client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent.Value);
                    configure?.Invoke(client);
                })
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
                {
                    PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                    ConnectTimeout = TimeSpan.FromSeconds(2)
                })
                .SetHandlerLifetime(Timeout.InfiniteTimeSpan);
        }
    }
}