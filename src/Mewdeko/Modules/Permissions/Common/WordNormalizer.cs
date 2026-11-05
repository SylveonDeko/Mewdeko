using System.Globalization;
using System.Text;

namespace Mewdeko.Modules.Permissions.Common;

/// <summary>
///     Undoes the usual tricks used to get a word past a filter: look-alike characters, symbols standing in for letters,
///     separators between letters and repeated letters.
/// </summary>
public static class WordNormalizer
{
    private static readonly Dictionary<char, char> LookAlikes = new()
    {
        ['а'] = 'a', ['е'] = 'e', ['о'] = 'o', ['р'] = 'p', ['с'] = 'c', ['у'] = 'y', ['х'] = 'x', ['і'] = 'i',
        ['ј'] = 'j', ['ѕ'] = 's', ['ԁ'] = 'd', ['һ'] = 'h', ['к'] = 'k', ['м'] = 'm', ['н'] = 'h', ['т'] = 't',
        ['в'] = 'b', ['ѵ'] = 'v', ['α'] = 'a', ['β'] = 'b', ['ε'] = 'e', ['ι'] = 'i', ['κ'] = 'k', ['ν'] = 'v',
        ['ο'] = 'o', ['ρ'] = 'p', ['τ'] = 't', ['υ'] = 'u', ['χ'] = 'x', ['ɑ'] = 'a', ['ı'] = 'i', ['ł'] = 'l',
        ['ø'] = 'o', ['đ'] = 'd', ['ɡ'] = 'g', ['ʏ'] = 'y', ['ᴀ'] = 'a', ['ʙ'] = 'b', ['ᴄ'] = 'c', ['ᴅ'] = 'd',
        ['ᴇ'] = 'e', ['ɢ'] = 'g', ['ʜ'] = 'h', ['ɪ'] = 'i', ['ᴊ'] = 'j', ['ᴋ'] = 'k', ['ʟ'] = 'l', ['ᴍ'] = 'm',
        ['ɴ'] = 'n', ['ᴏ'] = 'o', ['ᴘ'] = 'p', ['ʀ'] = 'r', ['ꜱ'] = 's', ['ᴛ'] = 't', ['ᴜ'] = 'u', ['ᴠ'] = 'v',
        ['ᴡ'] = 'w', ['ᴢ'] = 'z'
    };

    private static readonly Dictionary<char, char> Swaps = new()
    {
        ['0'] = 'o', ['3'] = 'e', ['4'] = 'a', ['5'] = 's', ['7'] = 't', ['8'] = 'b', ['9'] = 'g', ['@'] = 'a',
        ['$'] = 's', ['!'] = 'i', ['|'] = 'i', ['+'] = 't', ['€'] = 'e', ['£'] = 'l', ['¢'] = 'c', ['§'] = 's'
    };

    /// <summary>
    ///     Checks whether a filter entry is plain text rather than a regular expression.
    /// </summary>
    /// <param name="pattern">The filter entry.</param>
    public static bool IsPlain(string pattern)
    {
        return pattern.Length > 0 && pattern.All(c => char.IsLetterOrDigit(c) || c == ' ');
    }

    /// <summary>
    ///     Lowercases text, strips accents and invisible characters and swaps look-alike characters and symbols for the
    ///     letters they stand in for. Spacing and punctuation stay where they are.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="oneAsL">Whether the digit 1 stands for l instead of i.</param>
    public static string Clean(string text, bool oneAsL = false)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var raw in text.ToLowerInvariant().Normalize(NormalizationForm.FormKD))
        {
            switch (CharUnicodeInfo.GetUnicodeCategory(raw))
            {
                case UnicodeCategory.NonSpacingMark or UnicodeCategory.Format or UnicodeCategory.EnclosingMark:
                    continue;
            }

            var c = LookAlikes.GetValueOrDefault(raw, raw);
            if (c == '1')
                c = oneAsL ? 'l' : 'i';
            else if (Swaps.TryGetValue(c, out var swapped))
                c = swapped;

            builder.Append(c);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Cleans text, then keeps only letters and digits and folds runs of the same character into one, so "b.a-aad"
    ///     and "baaad" both become "bad".
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="oneAsL">Whether the digit 1 stands for l instead of i.</param>
    public static string Squash(string text, bool oneAsL = false)
    {
        var cleaned = Clean(text, oneAsL);
        var builder = new StringBuilder(cleaned.Length);
        foreach (var c in cleaned)
        {
            if (!char.IsLetterOrDigit(c) || (builder.Length > 0 && builder[^1] == c))
                continue;

            builder.Append(c);
        }

        return builder.ToString();
    }
}
