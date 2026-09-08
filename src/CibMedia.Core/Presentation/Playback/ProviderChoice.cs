using CibMedia.Core.Common;

namespace CibMedia.Core.Presentation.Playback;

// Which of a title's providers a details page names. Preferred is where it lands: the wanted
// one when the title has it, else the first offered, and null with none. Next is the one after
// the current, round and round, which is how the Provider action moves between the few a title has.
public static class ProviderChoice
{
    public static string? Preferred(IReadOnlyList<string> providers, string? wanted)
    {
        return providers.FirstOrDefault(provider => string.Equals(provider, wanted, StringComparison.OrdinalIgnoreCase))
               ?? (providers.Count > 0 ? providers[0] : null);
    }

    public static string? Next(IReadOnlyList<string> providers, string? current)
    {
        if (providers.Count == 0) return null;

        var index = providers.FindIndex(
            provider => string.Equals(provider, current, StringComparison.OrdinalIgnoreCase));

        return providers[index < 0 ? 0 : (index + 1) % providers.Count];
    }
}
