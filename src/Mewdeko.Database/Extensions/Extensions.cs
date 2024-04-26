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
}