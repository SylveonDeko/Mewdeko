namespace Mewdeko.Database.Extensions;

public static class Extensions
{
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

    public static bool ParseBoth(this bool _, string value)
    {
        switch (value)
        {
            case null:
                throw new ArgumentNullException(nameof(value));
            case "0":
            case "1":
                return value == "1";
        }

        if (bool.TryParse(value, out var result))
            return result;

        throw new FormatException($"The value '{value}' is not a valid boolean representation.");
    }

    public static bool ParseBoth(this bool _, long value)
    {
        switch (value)
        {
            case > 1:
                throw new ArgumentNullException(nameof(value));
            case 0:
            case 1:
                return value == 1;
        }

        throw new FormatException($"The value '{value}' is not a valid boolean representation.");
    }
}