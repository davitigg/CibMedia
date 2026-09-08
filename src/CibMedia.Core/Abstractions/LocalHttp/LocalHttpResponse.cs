namespace CibMedia.Core.Abstractions.LocalHttp;

public sealed record LocalHttpResponse(int Status, string ContentType, string Body)
{
    public static LocalHttpResponse Accepted { get; } = Text(202, "Accepted");

    public static LocalHttpResponse BadRequest { get; } = Text(400, "Bad request");

    public static LocalHttpResponse NotFound { get; } = Text(404, "Not found");

    public static LocalHttpResponse Html(string html)
    {
        return new LocalHttpResponse(200, "text/html; charset=utf-8", html);
    }

    private static LocalHttpResponse Text(int status, string body)
    {
        return new LocalHttpResponse(status, "text/plain; charset=utf-8", body);
    }
}
