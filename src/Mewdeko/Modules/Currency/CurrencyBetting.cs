using Mewdeko.Database.Enums;
using Mewdeko.Modules.Currency.Services;

namespace Mewdeko.Modules.Currency;

/// <summary>
///     The shared wagering path every game goes through.
/// </summary>
/// <remarks>
///     Games used to read the player's balance, decide an outcome, and only then adjust the balance.
///     Between the read and the write nothing held the funds, so the same currency could back several
///     simultaneous wagers across different channels. Taking the stake up front with a conditional debit
///     closes that, and routing every game through one place is also what makes bet limits, the game
///     cooldown, the daily loss cap and per-game analytics apply consistently instead of game by game.
/// </remarks>
public partial class Currency
{
    /// <summary>
    ///     Validates a wager, enforces the guild's limits, and takes the stake atomically.
    /// </summary>
    /// <remarks>
    ///     Reports the reason to the channel itself when a wager is refused, so callers only branch on
    ///     the boolean.
    /// </remarks>
    /// <param name="bet">The amount being wagered.</param>
    /// <param name="gameKey">Stable game identifier recorded on the ledger entry.</param>
    /// <returns><see langword="true" /> if the stake was taken and the game may proceed.</returns>
    private async Task<bool> TryTakeBetAsync(long bet, string gameKey)
    {
        var config = await ConfigService.GetConfigAsync(ctx.Guild.Id);
        var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);

        switch (CurrencyConfigService.ValidateBet(config, bet))
        {
            case BetValidation.GamblingDisabled:
                await ErrorAsync(Strings.GamblingDisabled(ctx.Guild.Id));
                return false;
            case BetValidation.BelowMinimum:
                await ErrorAsync(Strings.BetBelowMinimum(ctx.Guild.Id, config.MinBet, emote));
                return false;
            case BetValidation.AboveMaximum:
                await ErrorAsync(Strings.BetAboveMaximum(ctx.Guild.Id, config.MaxBet, emote));
                return false;
        }

        if (config.LossLimitPerDay > 0)
        {
            var lost = await Service.GetNetGameLossAsync(ctx.User.Id, DateTime.UtcNow.AddDays(-1), ctx.Guild.Id);

            if (lost >= config.LossLimitPerDay)
            {
                await ErrorAsync(Strings.LossLimitReached(ctx.Guild.Id, config.LossLimitPerDay, emote));
                return false;
            }
        }

        if (config.GameCooldownSeconds > 0)
        {
            var claim = await CooldownService.TryClaimAsync(ctx.Guild.Id, ctx.User.Id, CurrencyCooldownService.Game,
                TimeSpan.FromSeconds(config.GameCooldownSeconds));

            if (!claim.Success)
            {
                await ErrorAsync(Strings.GameCooldown(ctx.Guild.Id, claim.Remaining.ToReadableDuration()));
                return false;
            }
        }

        if (await Service.TryDebitAsync(ctx.User.Id, bet, Strings.BetPlacedTransaction(ctx.Guild.Id, gameKey),
                CurrencyCategory.GameBet, ctx.Guild.Id, gameKey))
            return true;

        await ErrorAsync(Strings.BetInsufficientFunds(ctx.Guild.Id, bet, emote));
        return false;
    }

    /// <summary>
    ///     Pays out a winning wager.
    /// </summary>
    /// <remarks>
    ///     The stake was already taken, so the full return is credited back. The guild's payout
    ///     multiplier scales only the profit, never the returned stake, so tuning it cannot leave a
    ///     winning player worse off than they started.
    /// </remarks>
    /// <param name="stake">The amount that was wagered.</param>
    /// <param name="multiplier">Total return as a multiple of the stake. 2.0 is an even-money win.</param>
    /// <param name="gameKey">Stable game identifier recorded on the ledger entry.</param>
    /// <returns>The amount credited.</returns>
    private async Task<long> WinAsync(long stake, double multiplier, string gameKey)
    {
        var config = await ConfigService.GetConfigAsync(ctx.Guild.Id);

        var profit = (long)Math.Round(stake * (multiplier - 1) * config.PayoutMultiplier);
        var total = stake + profit;

        if (total <= 0)
            return 0;

        await Service.CreditAsync(ctx.User.Id, total, Strings.BetWonTransaction(ctx.Guild.Id, gameKey),
            CurrencyCategory.GamePayout, ctx.Guild.Id, gameKey);

        return total;
    }

    /// <summary>
    ///     Returns a stake untouched, for a tie or a game that could not be completed.
    /// </summary>
    /// <param name="stake">The amount to return.</param>
    /// <param name="gameKey">Stable game identifier recorded on the ledger entry.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private Task PushAsync(long stake, string gameKey)
    {
        return stake <= 0
            ? Task.CompletedTask
            : Service.CreditAsync(ctx.User.Id, stake, Strings.BetRefundedTransaction(ctx.Guild.Id, gameKey),
                CurrencyCategory.GamePayout, ctx.Guild.Id, gameKey);
    }

    /// <summary>
    ///     Credits an explicit amount as a game payout, for games whose return is not a clean multiple
    ///     of the stake.
    /// </summary>
    /// <param name="amount">The total amount to return to the player.</param>
    /// <param name="gameKey">Stable game identifier recorded on the ledger entry.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private Task PayoutAsync(long amount, string gameKey)
    {
        return amount <= 0
            ? Task.CompletedTask
            : Service.CreditAsync(ctx.User.Id, amount, Strings.BetWonTransaction(ctx.Guild.Id, gameKey),
                CurrencyCategory.GamePayout, ctx.Guild.Id, gameKey);
    }
}