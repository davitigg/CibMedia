namespace CibMedia.Playback.Extensions;

internal static class UpstreamFaults
{
    // The client's own timeout surfaces as a cancellation the caller never asked for.
    public static bool IsUpstreamFault(this Exception exception, CancellationToken cancellationToken)
    {
        return exception is HttpRequestException
               || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested);
    }
}
