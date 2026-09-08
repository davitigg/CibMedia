using CibMedia.Core.Abstractions;
using TMDbLib.Client;

namespace CibMedia.Core.Infrastructure;

public sealed class ApiKeyCheck : IApiKeyCheck
{
    // TMDbLib throws UnauthorizedAccessException for a refused key whatever ThrowApiExceptions
    // is set to.
    public async Task<bool> IsValidAsync(ApiKeyKind kind, string apiKey, CancellationToken ct)
    {
        using var client = new TMDbClient(apiKey);

        try
        {
            await client.GetConfigAsync().ConfigureAwait(false);

            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
