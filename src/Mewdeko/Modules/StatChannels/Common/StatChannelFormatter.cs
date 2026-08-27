using System.Globalization;
using System.Text;

namespace Mewdeko.Modules.StatChannels.Common;

/// <summary>
///     Renders a raw stat value into display text according to a <see cref="StatChannelDisplayStyle" />.
/// </summary>
public static class StatChannelFormatter
{
    private static readonly string[] CompactSuffixes =
    [
        "", "K", "M", "B", "T"
    ];

    private static readonly string[] KeycapDigits =
    [
        "0️⃣", "1️⃣", "2️⃣", "3️⃣", "4️⃣",
        "5️⃣", "6️⃣", "7️⃣", "8️⃣", "9️⃣"
    ];

    private static readonly (int Value, string Numeral)[] RomanNumerals =
    [
        (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"),
        (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")
    ];

    /// <summary>
    ///     Formats a value for display.
    /// </summary>
    /// <param name="value">The raw value.</param>
    /// <param name="style">The style to apply.</param>
    /// <param name="options">Style tuning options.</param>
    /// <param name="target">The goal target, used by the percent and progress bar styles.</param>
    /// <returns>The formatted text.</returns>
    public static string Format(long value, StatChannelDisplayStyle style, StatChannelStyleOptions options,
        long target = 0)
    {
        return style switch
        {
            StatChannelDisplayStyle.Plain => value.ToString(CultureInfo.InvariantCulture),
            StatChannelDisplayStyle.Comma => value.ToString("N0", CultureInfo.InvariantCulture),
            StatChannelDisplayStyle.Compact => FormatCompact(value, options.Decimals),
            StatChannelDisplayStyle.Padded => FormatPadded(value, options.PadWidth),
            StatChannelDisplayStyle.Ordinal => FormatOrdinal(value),
            StatChannelDisplayStyle.Percent => FormatPercent(value, target, options.Decimals),
            StatChannelDisplayStyle.ProgressBar => FormatProgressBar(value, target, options),
            StatChannelDisplayStyle.EmojiDigits => FormatEmojiDigits(value),
            StatChannelDisplayStyle.Roman => FormatRoman(value),
            _ => value.ToString("N0", CultureInfo.InvariantCulture)
        };
    }

    /// <summary>
    ///     Formats a boolean stat using the configured true and false text.
    /// </summary>
    /// <param name="value">The boolean value.</param>
    /// <param name="options">Style tuning options.</param>
    /// <returns>The formatted text.</returns>
    public static string FormatBoolean(bool value, StatChannelStyleOptions options)
    {
        return value ? options.TrueText : options.FalseText;
    }

    private static string FormatCompact(long value, int decimals)
    {
        var negative = value < 0;
        var abs = Math.Abs((double)value);
        var magnitude = 0;

        while (abs >= 1000 && magnitude < CompactSuffixes.Length - 1)
        {
            abs /= 1000;
            magnitude++;
        }

        var places = Math.Clamp(decimals, 0, 3);
        if (magnitude == 0) places = 0;

        var rendered = abs.ToString($"F{places}", CultureInfo.InvariantCulture);
        if (places > 0 && rendered.Contains('.'))
            rendered = rendered.TrimEnd('0').TrimEnd('.');

        return $"{(negative ? "-" : "")}{rendered}{CompactSuffixes[magnitude]}";
    }

    private static string FormatPadded(long value, int width)
    {
        var negative = value < 0;
        var digits = Math.Abs(value).ToString(CultureInfo.InvariantCulture)
            .PadLeft(Math.Clamp(width, 1, 20), '0');
        return negative ? $"-{digits}" : digits;
    }

    private static string FormatOrdinal(long value)
    {
        var formatted = value.ToString("N0", CultureInfo.InvariantCulture);
        var abs = Math.Abs(value);
        var suffix = abs % 100 is >= 11 and <= 13
            ? "th"
            : (abs % 10) switch
            {
                1 => "st", 2 => "nd", 3 => "rd", _ => "th"
            };
        return formatted + suffix;
    }

    private static string FormatPercent(long value, long target, int decimals)
    {
        if (target <= 0)
            return "0%";

        var percent = (double)value / target * 100;
        return percent.ToString($"F{Math.Clamp(decimals, 0, 3)}", CultureInfo.InvariantCulture) + "%";
    }

    private static string FormatProgressBar(long value, long target, StatChannelStyleOptions options)
    {
        var width = Math.Clamp(options.BarWidth, 1, 40);
        var ratio = target <= 0 ? 0 : Math.Clamp((double)value / target, 0, 1);
        var filled = (int)Math.Round(ratio * width, MidpointRounding.AwayFromZero);

        var filledGlyph = string.IsNullOrEmpty(options.BarFilled) ? "▰" : options.BarFilled;
        var emptyGlyph = string.IsNullOrEmpty(options.BarEmpty) ? "▱" : options.BarEmpty;

        var sb = new StringBuilder();
        for (var i = 0; i < width; i++)
            sb.Append(i < filled ? filledGlyph : emptyGlyph);

        return sb.ToString();
    }

    private static string FormatEmojiDigits(long value)
    {
        var sb = new StringBuilder();
        if (value < 0) sb.Append('-');

        foreach (var c in Math.Abs(value).ToString(CultureInfo.InvariantCulture))
            sb.Append(KeycapDigits[c - '0']);

        return sb.ToString();
    }

    private static string FormatRoman(long value)
    {
        if (value <= 0 || value > 3999)
            return value.ToString("N0", CultureInfo.InvariantCulture);

        var remaining = (int)value;
        var sb = new StringBuilder();

        foreach (var (numeralValue, numeral) in RomanNumerals)
        {
            while (remaining >= numeralValue)
            {
                sb.Append(numeral);
                remaining -= numeralValue;
            }
        }

        return sb.ToString();
    }
}