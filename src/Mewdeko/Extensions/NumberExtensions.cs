namespace Mewdeko.Extensions;

/// <summary>
/// Provides extension methods for numeric types.
/// </summary>
public static class NumberExtensions
{
    /// <summary>
    /// Converts a value to kilobytes.
    /// </summary>
    public static int KiB(this int value) => value * 1024;

    /// <summary>
    /// Converts a value to kilobits.
    /// </summary>
    public static int Kb(this int value) => value * 1000;

    /// <summary>
    /// Converts a value to megabytes.
    /// </summary>
    public static int MiB(this int value) => value.KiB() * 1024;

    /// <summary>
    /// Converts a value to megabits.
    /// </summary>
    public static int Mb(this int value) => value.Kb() * 1000;

    /// <summary>
    /// Converts a value to gigabytes.
    /// </summary>
    public static int GiB(this int value) => value.MiB() * 1024;

    /// <summary>
    /// Converts a value to gigabits.
    /// </summary>
    public static int Gb(this int value) => value.Mb() * 1000;

    /// <summary>
    /// Converts a value to kilobytes.
    /// </summary>
    public static ulong KiB(this ulong value) => value * 1024;

    /// <summary>
    /// Converts a value to kilobits.
    /// </summary>
    public static ulong Kb(this ulong value) => value * 1000;

    /// <summary>
    /// Converts a value to megabytes.
    /// </summary>
    public static ulong MiB(this ulong value) => value.KiB() * 1024;

    /// <summary>
    /// Converts a value to megabits.
    /// </summary>
    public static ulong Mb(this ulong value) => value.Kb() * 1000;

    /// <summary>
    /// Converts a value to gigabytes.
    /// </summary>
    public static ulong GiB(this ulong value) => value.MiB() * 1024;

    /// <summary>
    /// Converts a value to gigabits.
    /// </summary>
    public static ulong Gb(this ulong value) => value.Mb() * 1000;

    /// <summary>
    /// Determines whether a decimal number is an integer.
    /// </summary>
    public static bool IsInteger(this decimal number) => number == Math.Truncate(number);

    /// <summary>
    /// Converts a Unix timestamp (seconds since epoch) to a <see cref="DateTimeOffset"/>.
    /// </summary>
    public static DateTimeOffset ToUnixTimestamp(this double number) =>
        new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero).AddSeconds(number);
}