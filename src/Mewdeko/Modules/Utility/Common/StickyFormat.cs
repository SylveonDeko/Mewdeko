namespace Mewdeko.Modules.Utility.Common;

/// <summary>
///     Formatting helpers for sticky message settings.
/// </summary>
public static class StickyFormat
{
    /// <summary>
    ///     Formats a stored time window as a short duration such as 15s or 5m.
    /// </summary>
    /// <param name="window">The stored time span text.</param>
    /// <returns>The short duration text.</returns>
    public static string Window(string? window)
    {
        if (!TimeSpan.TryParse(window, out var span))
            span = StickyDefaults.Window;

        if (span.TotalHours >= 1 && span.Minutes == 0 && span.Seconds == 0)
            return $"{(int)span.TotalHours}h";

        if (span.TotalMinutes >= 1 && span.Seconds == 0)
            return $"{(int)span.TotalMinutes}m";

        return $"{(int)span.TotalSeconds}s";
    }
}

/// <summary>
///     Default sticky message settings.
/// </summary>
public static class StickyDefaults
{
    /// <summary>
    ///     How many new messages bring a sticky back to the bottom by default.
    /// </summary>
    public const int Messages = 5;

    /// <summary>
    ///     How long after the first new message a sticky comes back to the bottom by default.
    /// </summary>
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(15);
}
