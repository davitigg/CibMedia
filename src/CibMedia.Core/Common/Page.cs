namespace CibMedia.Core.Common;

public sealed record Page<T>(IReadOnlyList<T> Items, int PageNumber, int TotalPages)
{
    public bool HasMore => PageNumber < TotalPages;

    public static Page<T> Empty { get; } = new([], 1, 1);
}
