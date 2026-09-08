namespace CibMedia.Core.Common;

public abstract record Load<T>
{
    public bool HasValue => this is Ready;

    public T? ValueOrDefault => this is Ready r ? r.Value : default;

    public sealed record Loading : Load<T>;

    public sealed record Ready(T Value) : Load<T>;

    public sealed record Failed(AppError Error) : Load<T>;
}

public static class Load
{
    public static Load<T> Loading<T>()
    {
        return new Load<T>.Loading();
    }

    public static Load<T> Ready<T>(T value)
    {
        return new Load<T>.Ready(value);
    }

    public static Load<T> Failed<T>(AppError error)
    {
        return new Load<T>.Failed(error);
    }

    public static async Task<Load<T>> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        try
        {
            return Ready(await work(ct).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (UpstreamException ex)
        {
            return Failed<T>(new AppError(ex.Kind, ex.Message));
        }
        catch (TaskCanceledException)
        {
            return Failed<T>(AppError.Timeout);
        }
        catch (HttpRequestException ex)
        {
            return Failed<T>(new AppError(AppErrorKind.Offline, ex.Message));
        }
        catch (Exception ex)
        {
            return Failed<T>(AppError.Unknown(ex.Message));
        }
    }
}
