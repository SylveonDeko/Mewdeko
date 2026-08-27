namespace Mewdeko.Modules.StatChannels.Common;

/// <summary>
///     How the resolved numeric value of a stat channel is rendered into %count%.
/// </summary>
public enum StatChannelDisplayStyle
{
    /// <summary>
    ///     Raw digits, for example 1234.
    /// </summary>
    Plain = 0,

    /// <summary>
    ///     Group separated digits, for example 1,234.
    /// </summary>
    Comma = 1,

    /// <summary>
    ///     Abbreviated magnitude, for example 1.2K.
    /// </summary>
    Compact = 2,

    /// <summary>
    ///     Zero padded to a fixed width, for example 00042.
    /// </summary>
    Padded = 3,

    /// <summary>
    ///     Ordinal suffixed, for example 1,234th.
    /// </summary>
    Ordinal = 4,

    /// <summary>
    ///     Percentage of the goal target, for example 61%.
    /// </summary>
    Percent = 5,

    /// <summary>
    ///     A progress bar toward the goal target, for example the filled and empty block run.
    /// </summary>
    ProgressBar = 6,

    /// <summary>
    ///     Keycap emoji digits, for example the digit emoji run.
    /// </summary>
    EmojiDigits = 7,

    /// <summary>
    ///     Roman numerals, for example MCCXXXIV.
    /// </summary>
    Roman = 8
}