namespace CibMedia.Core.Abstractions.LocalHttp;

public sealed record LocalHttpRequest(string Method, string Path, string Body)
{
    public bool IsPost => string.Equals(Method, "POST", StringComparison.OrdinalIgnoreCase);

    // Reads an application/x-www-form-urlencoded field. Null for one that is absent or blank.
    public string? Form(string name)
    {
        foreach (var pair in Body.Split('&'))
        {
            var split = pair.IndexOf('=');
            if (split < 0 || !pair.AsSpan(0, split).Trim().SequenceEqual(name)) continue;

            return Uri.UnescapeDataString(pair[(split + 1)..].Replace('+', ' ')).Trim() is { Length: > 0 } value
                ? value
                : null;
        }

        return null;
    }
}
