namespace CibMedia.Core.Common;

// A failure infrastructure already classified, carried up in the one shape Load.RunAsync
// keeps as an AppError kind rather than Unknown.
public sealed class UpstreamException(AppErrorKind kind, string message) : Exception(message)
{
    public AppErrorKind Kind { get; } = kind;
}
