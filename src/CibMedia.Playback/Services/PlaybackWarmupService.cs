using CibMedia.Playback.Logging;
using CibMedia.Playback.Providers.Xpass;

namespace CibMedia.Playback.Services;

// What a stack can do before anything asks it for a title. Nothing here changes an answer — it only
// moves a cost off the first lookup — so a failure is dropped rather than reported as one, and the
// lookup that ends up paying reports for itself.
public sealed class PlaybackWarmupService
{
    private readonly XpassClient _xpass;
    private readonly XpassOptions _options;
    private readonly ILogger<PlaybackWarmupService> _logger;

    // Composed rather than constructed by the container: the stack it takes is this assembly's own,
    // so the constructor that takes it cannot be public.
    internal PlaybackWarmupService(
        XpassClient xpass,
        XpassOptions options,
        ILogger<PlaybackWarmupService> logger
    )
    {
        _xpass = xpass;
        _options = options;
        _logger = logger;
    }

    public async Task WarmAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled) return;

        try
        {
            await _xpass.WarmAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            _logger.PlaybackWarmupFailed(exception);
        }
    }
}
