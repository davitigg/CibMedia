namespace CibMedia.Core.Abstractions.LocalHttp;

// The reply is written before anything is done with the request, so a phone sees its post
// land even when acting on it tears down the screen that asked.
public delegate Task<LocalHttpResponse> LocalHttpHandler(LocalHttpRequest request, CancellationToken ct);
