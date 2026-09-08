using Android.Content;
using Android.Text;
using Android.Text.Style;
using Java.Lang;

namespace CibMedia.AndroidTv.Design;

// The "2026 • 1h 54m • ★ 8.3 • PG" lines. Blank parts are dropped so a title with no rating
// does not render a stray separator.
public static class MetaText
{
    private const char Star = '★';

    public static string Join(params string?[] parts)
    {
        return string.Join(" • ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    public static string? Rating(double? rating)
    {
        return rating is { } value and > 0 ? $"{Star} {value:0.0}" : null;
    }

    public static ICharSequence Highlighted(Context context, params string?[] parts)
    {
        return HighlightedOver(context, null, parts);
    }

    // The star and its score in gold, found rather than passed by index because a card leads
    // with the rating and the details header carries it in the middle. The second line is
    // for genres, which read better under the year and rating than tacked onto them.
    public static ICharSequence HighlightedOver(Context context, string? secondLine, params string?[] parts)
    {
        var head = Join(parts);
        var text = string.IsNullOrWhiteSpace(secondLine) ? head : $"{head}\n\n{secondLine}";
        var span = new SpannableString(text);
        var start = text.IndexOf(Star);

        if (start >= 0)
        {
            var separator = text.IndexOf('•', start);
            var end = separator < 0 ? text.Length : separator - 1;

            span.SetSpan(
                new ForegroundColorSpan(Tokens.AccentGold(context)),
                start,
                end,
                SpanTypes.ExclusiveExclusive);
        }

        return span;
    }
}
