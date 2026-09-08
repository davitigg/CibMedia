using System.Text;
using System.Text.RegularExpressions;

namespace CibMedia.Playback.Providers.Xpass;

// Reads the string table out of an obfuscator.io bundle. Every literal the bundle uses sits in one
// array, custom-alphabet base64 encoded, and each entry decodes on its own: the rotation the bundle
// applies at load only changes which call-site index maps to which entry, so scanning the whole
// decoded table finds any literal without executing the script.
internal static partial class XpassObfuscatedBundle
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789+/=";

    public static IEnumerable<string> DecodeStrings(string bundle)
    {
        string? table = null;
        foreach (Match candidate in ArrayLiteralRegex().Matches(bundle))
            if (table is null || candidate.Length > table.Length)
                table = candidate.Value;

        if (table is null) yield break;

        foreach (Match literal in StringLiteralRegex().Matches(table))
        {
            var decoded = TryDecode(Unescape(literal.Groups[1].Value));
            if (decoded is not null) yield return decoded;
        }
    }

    private static string? TryDecode(string encoded)
    {
        var bytes = new List<byte>(encoded.Length);
        var accumulator = 0;
        var bits = 0;

        foreach (var character in encoded)
        {
            if (character == '=') break;

            var value = Alphabet.IndexOf(character);
            if (value < 0) return null;

            accumulator = (accumulator << 6) | value;
            bits += 6;
            if (bits < 8) continue;

            bits -= 8;
            bytes.Add((byte)((accumulator >> bits) & 0xFF));
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    private static string Unescape(string literal)
    {
        if (!literal.Contains('\\')) return literal;

        var result = new StringBuilder(literal.Length);
        for (var index = 0; index < literal.Length; index++)
        {
            var character = literal[index];
            if (character != '\\' || index + 1 >= literal.Length)
            {
                result.Append(character);
                continue;
            }

            var escaped = literal[++index];
            switch (escaped)
            {
                case 'x' when index + 2 < literal.Length:
                    result.Append((char)Convert.ToInt32(literal.Substring(index + 1, 2), 16));
                    index += 2;
                    break;
                case 'n':
                    result.Append('\n');
                    break;
                case 'r':
                    result.Append('\r');
                    break;
                case 't':
                    result.Append('\t');
                    break;
                default:
                    result.Append(escaped);
                    break;
            }
        }

        return result.ToString();
    }

    // The table is the largest bracketed run in the file; base64 never contains ']'.
    [GeneratedRegex(@"\[[^\]]*\]")]
    private static partial Regex ArrayLiteralRegex();

    [GeneratedRegex(@"'((?:\\.|[^'\\])*)'")]
    private static partial Regex StringLiteralRegex();
}