namespace CibMedia.Core.Presentation.Remote;

// Null for a body that named no url, which the controller turns down.
public sealed record PlayLiveballCommand(string? Url);
