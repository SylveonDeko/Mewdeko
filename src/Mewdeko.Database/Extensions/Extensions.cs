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
                return string.Concat(str.Select(_ => '.'));
        }

        if (str.Length < maxLength)
            return str;

        if (hideDots)
            return string.Concat(str.Take(maxLength));
        return string.Concat(str.Take(maxLength - 3)) + "...";
    }
}