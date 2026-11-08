namespace Mewdeko.Modules.Currency.Services;

/// <summary>
///     Checks for the economy setup and tuning values the dashboard sends.
/// </summary>
public static class CurrencySetupRules
{
    /// <summary>
    ///     Longest currency emoji, in characters.
    /// </summary>
    public const int MaxEmojiLength = 64;

    /// <summary>
    ///     Largest daily reward.
    /// </summary>
    public const int MaxRewardAmount = 1_000_000_000;

    /// <summary>
    ///     Shortest delay between daily rewards, in seconds.
    /// </summary>
    public const int MinRewardCooldown = 60;

    /// <summary>
    ///     Longest delay between daily rewards, in seconds.
    /// </summary>
    public const int MaxRewardCooldown = 2_592_000;

    /// <summary>
    ///     Checks a currency emoji.
    /// </summary>
    /// <param name="emoji">The emoji as typed.</param>
    /// <returns>What is wrong with it, or null.</returns>
    public static string? ValidateEmoji(string? emoji)
    {
        var value = emoji?.Trim();
        if (string.IsNullOrEmpty(value))
            return "Currency emoji can't be empty.";
        if (value.Length > MaxEmojiLength)
            return $"Currency emoji can't be longer than {MaxEmojiLength} characters.";

        return value.TryToIEmote(out _) ? null : "Currency emoji must be a standard emoji or a custom emoji.";
    }

    /// <summary>
    ///     Checks the daily reward amount and cooldown together.
    /// </summary>
    /// <param name="amount">The reward amount.</param>
    /// <param name="cooldownSeconds">The delay between claims.</param>
    /// <returns>What is wrong with them, or null.</returns>
    public static string? ValidateReward(int amount, int cooldownSeconds)
    {
        if (amount is < 0 or > MaxRewardAmount)
            return $"Reward amount must be between 0 and {MaxRewardAmount:N0}.";
        if (amount == 0)
            return cooldownSeconds is < 0 or > MaxRewardCooldown
                ? $"Reward cooldown must be at most {MaxRewardCooldown:N0} seconds."
                : null;

        return cooldownSeconds is < MinRewardCooldown or > MaxRewardCooldown
            ? $"Reward cooldown must be between {MinRewardCooldown} and {MaxRewardCooldown:N0} seconds."
            : null;
    }

    /// <summary>
    ///     Checks that every minimum is at most its maximum, for the pairs a request touches.
    /// </summary>
    /// <param name="work">The work reward range.</param>
    /// <param name="crime">The crime reward range.</param>
    /// <param name="fine">The crime fine range.</param>
    /// <param name="bet">The bet range. A maximum of 0 means no maximum.</param>
    /// <returns>What is wrong, or null.</returns>
    public static string? ValidateRanges((long Min, long Max)? work, (long Min, long Max)? crime,
        (long Min, long Max)? fine, (long Min, long Max)? bet)
    {
        if (work is { } w && w.Min > w.Max)
            return "Work minimum can't be above the maximum.";
        if (crime is { } c && c.Min > c.Max)
            return "Crime minimum reward can't be above the maximum.";
        if (fine is { } f && f.Min > f.Max)
            return "Crime minimum fine can't be above the maximum.";
        if (bet is { } b && b.Max > 0 && b.Min > b.Max)
            return "Minimum bet can't be above the maximum bet.";

        return null;
    }
}
