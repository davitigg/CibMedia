namespace CibMedia.Core.Common;

public sealed record AppError(AppErrorKind Kind, string? Detail = null)
{
    public static readonly AppError Offline = new(AppErrorKind.Offline);
    public static readonly AppError Timeout = new(AppErrorKind.Timeout);
    public static readonly AppError NotFound = new(AppErrorKind.NotFound);

    public static AppError Unknown(string? detail = null)
    {
        return new AppError(AppErrorKind.Unknown, detail);
    }
}
