namespace Mewdeko.Database.Extensions;

/// <summary>
/// Provides extension methods for various operations.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Trims a string to a specified maximum length.
    /// </summary>
    /// <param name="str">The string to trim.</param>
    /// <param name="maxLength">The maximum length of the resulting string.</param>
    /// <param name="hideDots">If true, ellipsis will not be added to the end of the trimmed string.</param>
    /// <returns>The trimmed string.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when maxLength is negative.</exception>
    public static string TrimTo(this string str, int maxLength, bool hideDots = false)
    {
        switch (maxLength)
        {
            case < 0:
                throw new ArgumentOutOfRangeException(nameof(maxLength),
                    $"Argument {nameof(maxLength)} can't be negative.");
            case 0:
                return string.Empty;
            case <= 3:
                return new string('.', maxLength);
        }

        if (str.Length < maxLength)
            return str;

        return hideDots ? string.Concat(str.Take(maxLength)) : $"{string.Concat(str.Take(maxLength - 1))}…";
    }
}