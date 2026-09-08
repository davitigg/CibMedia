namespace CibMedia.Core.Common;

public static class ReadOnlyListExtensions
{
    public static int FindIndex<T>(this IReadOnlyList<T> list, Func<T, bool> match)
    {
        for (var index = 0; index < list.Count; index++)
        {
            if (match(list[index])) return index;
        }

        return -1;
    }
}
