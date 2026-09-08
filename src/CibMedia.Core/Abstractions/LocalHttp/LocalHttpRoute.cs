namespace CibMedia.Core.Abstractions.LocalHttp;

public sealed record LocalHttpRoute(string Method, string Path, LocalHttpHandler Handler);
