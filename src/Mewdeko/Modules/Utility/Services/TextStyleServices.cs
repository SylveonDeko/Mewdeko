using System.Text;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Converts plain text into decorative unicode styles.
/// </summary>
public static class TextStyleServices
{
    private const string SmallCapsLetters = "ᴀʙᴄᴅᴇꜰɢʜɪᴊᴋʟᴍɴᴏᴘǫʀsᴛᴜᴠᴡxʏᴢ";

    private static readonly string[] DigitNames =
        ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"];

    private static readonly Dictionary<char, string> ScriptExceptions = new()
    {
        ['B'] = "ℬ", ['E'] = "ℰ", ['F'] = "ℱ", ['H'] = "ℋ", ['I'] = "ℐ", ['L'] = "ℒ", ['M'] = "ℳ", ['R'] = "ℛ",
        ['e'] = "ℯ", ['g'] = "ℊ", ['o'] = "ℴ"
    };

    private static readonly Dictionary<char, string> FrakturExceptions = new()
    {
        ['C'] = "ℭ", ['H'] = "ℌ", ['I'] = "ℑ", ['R'] = "ℜ", ['Z'] = "ℨ"
    };

    private static readonly Dictionary<char, string> DoubleStruckExceptions = new()
    {
        ['C'] = "ℂ", ['H'] = "ℍ", ['N'] = "ℕ", ['P'] = "ℙ", ['Q'] = "ℚ", ['R'] = "ℝ", ['Z'] = "ℤ"
    };

    /// <summary>
    ///     Converts text to script letters.
    /// </summary>
    /// <param name="input">The text to convert.</param>
    /// <returns>The converted text.</returns>
    public static string Fancy(string input)
    {
        return Letters(input, 0x1D49C, 0x1D4B6, ScriptExceptions);
    }

    /// <summary>
    ///     Converts text to bold script letters.
    /// </summary>
    /// <param name="input">The text to convert.</param>
    /// <returns>The converted text.</returns>
    public static string BoldFancy(string input)
    {
        return Letters(input, 0x1D4D0, 0x1D4EA);
    }

    /// <summary>
    ///     Converts text to fraktur letters.
    /// </summary>
    /// <param name="input">The text to convert.</param>
    /// <returns>The converted text.</returns>
    public static string Fraktur(string input)
    {
        return Letters(input, 0x1D504, 0x1D51E, FrakturExceptions);
    }

    /// <summary>
    ///     Converts text to bold fraktur letters.
    /// </summary>
    /// <param name="input">The text to convert.</param>
    /// <returns>The converted text.</returns>
    public static string BoldFraktur(string input)
    {
        return Letters(input, 0x1D56C, 0x1D586);
    }

    /// <summary>
    ///     Converts text to double-struck letters and digits.
    /// </summary>
    /// <param name="input">The text to convert.</param>
    /// <returns>The converted text.</returns>
    public static string DoubleStruck(string input)
    {
        return Letters(input, 0x1D538, 0x1D552, DoubleStruckExceptions, 0x1D7D8);
    }

    /// <summary>
    ///     Converts text to small capital letters.
    /// </summary>
    /// <param name="input">The text to convert.</param>
    /// <returns>The converted text.</returns>
    public static string SmallCaps(string input)
    {
        var sb = new StringBuilder();
        foreach (var c in input)
        {
            var lower = char.ToLowerInvariant(c);
            sb.Append(lower is >= 'a' and <= 'z' ? SmallCapsLetters[lower - 'a'] : c);
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Converts text to full-width characters.
    /// </summary>
    /// <param name="input">The text to convert.</param>
    /// <returns>The converted text.</returns>
    public static string Aesthetic(string input)
    {
        var sb = new StringBuilder();
        foreach (var c in input)
        {
            sb.Append(c switch
            {
                ' ' => '　',
                >= '!' and <= '~' => (char)(c + 0xFEE0),
                _ => c
            });
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Puts a clap between each word.
    /// </summary>
    /// <param name="input">The text to convert.</param>
    /// <returns>The converted text.</returns>
    public static string Clap(string input)
    {
        var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" 👏 ", words);
    }

    /// <summary>
    ///     Puts a space between each character.
    /// </summary>
    /// <param name="input">The text to convert.</param>
    /// <returns>The converted text.</returns>
    public static string SpaceOut(string input)
    {
        var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join("   ", words.Select(w => string.Join(" ", w.EnumerateRunes().Select(r => r.ToString()))));
    }

    /// <summary>
    ///     Converts letters and digits to regional indicator and number emoji.
    /// </summary>
    /// <param name="input">The text to convert.</param>
    /// <returns>The converted text.</returns>
    public static string Emojify(string input)
    {
        var sb = new StringBuilder();
        foreach (var c in input)
        {
            var lower = char.ToLowerInvariant(c);
            if (lower is >= 'a' and <= 'z')
                sb.Append($":regional_indicator_{lower}: ");
            else if (c is >= '0' and <= '9')
                sb.Append($":{DigitNames[c - '0']}: ");
            else if (c == ' ')
                sb.Append("   ");
            else
                sb.Append(c).Append(' ');
        }

        return sb.ToString().TrimEnd();
    }

    private static string Letters(string input, int upperStart, int lowerStart,
        Dictionary<char, string>? exceptions = null, int digitStart = 0)
    {
        var sb = new StringBuilder();
        foreach (var c in input)
        {
            if (exceptions != null && exceptions.TryGetValue(c, out var exception))
                sb.Append(exception);
            else if (c is >= 'A' and <= 'Z')
                sb.Append(char.ConvertFromUtf32(upperStart + (c - 'A')));
            else if (c is >= 'a' and <= 'z')
                sb.Append(char.ConvertFromUtf32(lowerStart + (c - 'a')));
            else if (digitStart != 0 && c is >= '0' and <= '9')
                sb.Append(char.ConvertFromUtf32(digitStart + (c - '0')));
            else
                sb.Append(c);
        }

        return sb.ToString();
    }
}
