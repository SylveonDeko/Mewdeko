namespace Mewdeko.Modules.StatChannels.Common;

/// <summary>
///     The raw outcome of resolving a stat type, before templating and styling are applied.
/// </summary>
public class ResolvedStat
{
    /// <summary>
    ///     Gets the shape of this value.
    /// </summary>
    public StatChannelValueKind Kind { get; private init; }

    /// <summary>
    ///     Gets the numeric value, when the kind is a number.
    /// </summary>
    public long Number { get; private init; }

    /// <summary>
    ///     Gets the denominator this value is measured against, used by the percent and progress bar styles.
    /// </summary>
    public long Target { get; private init; }

    /// <summary>
    ///     Gets the text value, when the kind is text.
    /// </summary>
    public string Text { get; private init; } = "";

    /// <summary>
    ///     Gets the boolean value, when the kind is boolean.
    /// </summary>
    public bool Flag { get; private init; }

    /// <summary>
    ///     Gets the stat specific placeholders this resolution contributes.
    /// </summary>
    public Dictionary<string, string> Extras { get; } = new();

    /// <summary>
    ///     Creates a numeric result.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="target">An optional denominator.</param>
    /// <returns>The result.</returns>
    public static ResolvedStat Num(long value, long target = 0)
    {
        return new ResolvedStat
        {
            Kind = StatChannelValueKind.Number, Number = value, Target = target
        };
    }

    /// <summary>
    ///     Creates a text result.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result.</returns>
    public static ResolvedStat Str(string value)
    {
        return new ResolvedStat
        {
            Kind = StatChannelValueKind.Text, Text = value
        };
    }

    /// <summary>
    ///     Creates a boolean result.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The result.</returns>
    public static ResolvedStat Bool(bool value)
    {
        return new ResolvedStat
        {
            Kind = StatChannelValueKind.Boolean, Flag = value, Number = value ? 1 : 0
        };
    }

    /// <summary>
    ///     Adds a stat specific placeholder.
    /// </summary>
    /// <param name="token">The placeholder token, including the surrounding percent signs.</param>
    /// <param name="value">The replacement value.</param>
    /// <returns>This result, for chaining.</returns>
    public ResolvedStat With(string token, string value)
    {
        Extras[token] = value;
        return this;
    }
}