using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using CibMedia.Playback.Providers.Liveball.Models;

namespace CibMedia.Playback.Providers.Liveball;

// A page payload is the resolve token XOR'd against a repeating key, then base64'd. The key lives in
// liveball's obfuscated bundle and changes when that is redeployed, so it is never read from there:
// the token is base64url text, and that alone pins the key down given enough payloads.
internal static class LiveballToken
{
    private const int MinKeyLength = 8;
    private const int MaxKeyLength = 64;

    // Positions the alphabet leaves open are settled by whether the claims decode; this bounds how
    // many keys that check may try before more payloads are needed instead.
    private const int MaxCombinations = 1 << 16;

    private static readonly bool[] TokenAlphabet =
        BuildAlphabet("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_.");

    public static byte[]? ReadPayload(string payload)
    {
        var buffer = new byte[payload.Length];

        return Convert.TryFromBase64String(payload, buffer, out var written) ? buffer[..written] : null;
    }

    public static string Decode(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> key)
    {
        var buffer = new byte[payload.Length];
        for (var index = 0; index < buffer.Length; index++)
            buffer[index] = (byte)(payload[index] ^ key[index % key.Length]);

        return Encoding.ASCII.GetString(buffer);
    }

    // The first segment is the claim set itself, not a JOSE header as in a standard JWT.
    public static string? ReadChannel(string token)
    {
        var separator = token.IndexOf('.');
        if (separator <= 0) return null;

        try
        {
            var claims = Base64Url.DecodeFromChars(token.AsSpan(0, separator));

            return JsonSerializer.Deserialize<LiveballClaims>(claims, JsonSerializerOptions.Web)?.Ch;
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return null;
        }
    }

    // A key byte is right only if every token byte it uncovers is in the alphabet. Letters survive a
    // case-flipped byte, so one payload leaves most positions with a few fits; several payloads close
    // them, and what stays open is tried against the claims decode. Shortest length first, so a key
    // is not reported at a multiple of its length.
    public static byte[]? RecoverKey(IReadOnlyList<byte[]> payloads)
    {
        if (payloads.Count is 0) return null;

        for (var length = MinKeyLength; length <= MaxKeyLength; length++)
        {
            var candidates = Candidates(payloads, length);
            if (candidates is null) continue;

            foreach (var key in Combinations(candidates))
                if (payloads.All(payload => ReadChannel(Decode(payload, key)) is not null))
                    return key;
        }

        return null;
    }

    // Null when a position has no fit at this length, or too many are left open to try.
    private static byte[][]? Candidates(IReadOnlyList<byte[]> payloads, int length)
    {
        var candidates = new byte[length][];
        var combinations = 1L;

        for (var position = 0; position < length; position++)
        {
            var fits = new List<byte>();
            for (var candidate = 0; candidate < 256; candidate++)
                if (Fits(payloads, position, length, (byte)candidate))
                    fits.Add((byte)candidate);

            combinations *= fits.Count;
            if (fits.Count is 0 || combinations > MaxCombinations) return null;

            candidates[position] = fits.ToArray();
        }

        return candidates;
    }

    private static bool Fits(IReadOnlyList<byte[]> payloads, int position, int length, byte candidate)
    {
        foreach (var payload in payloads)
            for (var index = position; index < payload.Length; index += length)
                if (!TokenAlphabet[payload[index] ^ candidate])
                    return false;

        return true;
    }

    private static IEnumerable<byte[]> Combinations(byte[][] candidates)
    {
        var chosen = new int[candidates.Length];

        while (true)
        {
            var key = new byte[candidates.Length];
            for (var position = 0; position < key.Length; position++)
                key[position] = candidates[position][chosen[position]];

            yield return key;

            var carry = 0;
            while (carry < chosen.Length && ++chosen[carry] == candidates[carry].Length) chosen[carry++] = 0;
            if (carry == chosen.Length) yield break;
        }
    }

    private static bool[] BuildAlphabet(string characters)
    {
        var table = new bool[256];
        foreach (var character in characters) table[character] = true;

        return table;
    }
}