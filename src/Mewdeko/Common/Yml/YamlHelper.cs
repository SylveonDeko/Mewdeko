namespace Mewdeko.Common.Yml;

/// <summary>
///     Provides helper methods for YAML-related operations.
/// </summary>
public class YamlHelper
{
    /// <summary>
    ///     Unescapes a Unicode code point and returns the corresponding character.
    /// </summary>
    /// <param name="point">The Unicode code point to unescape.</param>
    /// <returns>The actual character represented by the Unicode code point.</returns>
    /// <remarks>
    ///     This method is a modified version of the code from the YamlDotNet library's Scanner class,
    ///     allowing for the parsing of Unicode code points.
    /// </remarks>
    public static string UnescapeUnicodeCodePoint(string point)
    {
        var character = 0;

        // Scan the character value.

        foreach (var c in point)
        {
            if (!IsHex(c)) return point;
            character = (character << 4) + AsHex(c);
        }

        // Check the value and write the character.

        if (character is >= 0xD800 and <= 0xDFFF or > 0x10FFFF) return point;

        return char.ConvertFromUtf32(character);
    }

    /// <summary>
    ///     Determines whether the specified character is a hexadecimal digit.
    /// </summary>
    /// <param name="c">The character to check.</param>
    /// <returns>True if the character is a hexadecimal digit; otherwise, false.</returns>
    public static bool IsHex(char c)
    {
        return c is >= '0' and <= '9' or
            >= 'A' and <= 'F' or
            >= 'a' and <= 'f';
    }

    /// <summary>
    ///     Converts the specified character to its hexadecimal value.
    /// </summary>
    /// <param name="c">The character to convert.</param>
    /// <returns>The hexadecimal value of the character.</returns>
    public static int AsHex(char c)
    {
        if (c <= '9') return c - '0';
        if (c <= 'F') return c - 'A' + 10;
        return c - 'a' + 10;
    }
}