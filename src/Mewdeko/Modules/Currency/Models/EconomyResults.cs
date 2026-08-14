namespace Mewdeko.Modules.Currency.Models;

/// <summary>
///     Why an earning action did or did not pay out.
/// </summary>
public enum EarnOutcome
{
    /// <summary>
    ///     The action succeeded and currency was paid.
    /// </summary>
    Success,

    /// <summary>
    ///     The action ran but the attempt failed, costing the user instead.
    /// </summary>
    Failed,

    /// <summary>
    ///     The action is turned off in this guild.
    /// </summary>
    Disabled,

    /// <summary>
    ///     The user must wait before trying again.
    /// </summary>
    OnCooldown
}

/// <summary>
///     The result of a work or crime attempt.
/// </summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Amount">Currency gained on success, or lost as a fine on failure.</param>
/// <param name="Remaining">How long remains on the cooldown, when that is why the action was refused.</param>
/// <param name="FlavorIndex">
///     Index into the localized flavor text list for this action, so the same attempt reads differently
///     each time without the service knowing anything about wording.
/// </param>
public readonly record struct EarnResult(EarnOutcome Outcome, long Amount, TimeSpan Remaining, int FlavorIndex);

/// <summary>
///     Why a robbery attempt resolved the way it did.
/// </summary>
public enum RobOutcome
{
    /// <summary>
    ///     The robbery succeeded and currency changed hands.
    /// </summary>
    Success,

    /// <summary>
    ///     The robbery failed and the robber paid a fine.
    /// </summary>
    Caught,

    /// <summary>
    ///     Robbery is turned off in this guild.
    /// </summary>
    Disabled,

    /// <summary>
    ///     The robber must wait before trying again.
    /// </summary>
    OnCooldown,

    /// <summary>
    ///     A user cannot rob themselves.
    /// </summary>
    SelfTarget,

    /// <summary>
    ///     The target's wallet is below the threshold that protects new or broke users.
    /// </summary>
    TargetTooPoor,

    /// <summary>
    ///     The robber has nothing to lose, so has no stake in the attempt.
    /// </summary>
    RobberTooPoor
}

/// <summary>
///     The result of a robbery attempt.
/// </summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Amount">Currency stolen on success, or paid as a fine when caught.</param>
/// <param name="Remaining">How long remains on the cooldown, when that is why the attempt was refused.</param>
public readonly record struct RobResult(RobOutcome Outcome, long Amount, TimeSpan Remaining);

/// <summary>
///     Why a transfer between users did or did not go through.
/// </summary>
public enum PayOutcome
{
    /// <summary>
    ///     The transfer completed.
    /// </summary>
    Success,

    /// <summary>
    ///     Transfers are turned off in this guild.
    /// </summary>
    Disabled,

    /// <summary>
    ///     The sender must wait before sending again.
    /// </summary>
    OnCooldown,

    /// <summary>
    ///     A user cannot pay themselves.
    /// </summary>
    SelfTarget,

    /// <summary>
    ///     The amount is below the guild's configured minimum.
    /// </summary>
    BelowMinimum,

    /// <summary>
    ///     The sender's wallet did not cover the amount.
    /// </summary>
    InsufficientFunds,

    /// <summary>
    ///     Bots hold no meaningful balance, so cannot be paid.
    /// </summary>
    BotTarget
}

/// <summary>
///     The result of a transfer between users.
/// </summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Sent">The amount taken from the sender.</param>
/// <param name="Received">The amount that reached the recipient after tax.</param>
/// <param name="Tax">The amount destroyed as tax.</param>
/// <param name="Remaining">How long remains on the cooldown, when that is why the transfer was refused.</param>
public readonly record struct PayResult(PayOutcome Outcome, long Sent, long Received, long Tax, TimeSpan Remaining);