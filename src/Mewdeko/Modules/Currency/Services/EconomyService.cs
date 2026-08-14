using Mewdeko.Database.Enums;
using Mewdeko.Modules.Currency.Common;
using Mewdeko.Modules.Currency.Models;

namespace Mewdeko.Modules.Currency.Services;

/// <summary>
///     The earning and circulation half of the economy: work, crime, robbery, transfers and the bank.
/// </summary>
/// <remarks>
///     Before this existed the only ways to gain currency were the daily reward and gambling, and the
///     only way to lose it was gambling. That gives players no reason to hold a balance and no activity
///     that rewards showing up. These actions create the faucet-and-sink pressure the rest of the module
///     depends on, all of it tunable per guild through <see cref="CurrencyConfigService" />.
/// </remarks>
public class EconomyService : INService
{
    /// <summary>
    ///     Number of distinct flavor messages the work command chooses between.
    /// </summary>
    public const int WorkFlavorCount = 6;

    /// <summary>
    ///     Number of distinct flavor messages a successful crime chooses between.
    /// </summary>
    public const int CrimeSuccessFlavorCount = 5;

    /// <summary>
    ///     Number of distinct flavor messages a failed crime chooses between.
    /// </summary>
    public const int CrimeFailFlavorCount = 5;

    private readonly CurrencyConfigService configService;
    private readonly CurrencyCooldownService cooldownService;
    private readonly ICurrencyService currencyService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="EconomyService" /> class.
    /// </summary>
    /// <param name="currencyService">The currency service.</param>
    /// <param name="configService">The per-guild economy configuration service.</param>
    /// <param name="cooldownService">The cooldown service.</param>
    public EconomyService(ICurrencyService currencyService, CurrencyConfigService configService,
        CurrencyCooldownService cooldownService)
    {
        this.currencyService = currencyService;
        this.configService = configService;
        this.cooldownService = cooldownService;
    }

    /// <summary>
    ///     Performs a shift of work, paying a random amount from the guild's configured range.
    /// </summary>
    /// <param name="guildId">The guild the command was used in.</param>
    /// <param name="userId">The working user.</param>
    /// <returns>The outcome and the amount earned.</returns>
    public async Task<EarnResult> WorkAsync(ulong guildId, ulong userId)
    {
        var config = await configService.GetConfigAsync(guildId);

        if (!config.WorkEnabled)
            return new EarnResult(EarnOutcome.Disabled, 0, TimeSpan.Zero, 0);

        var claim = await cooldownService.TryClaimAsync(guildId, userId, CurrencyCooldownService.Work,
            TimeSpan.FromSeconds(config.WorkCooldownSeconds));

        if (!claim.Success)
            return new EarnResult(EarnOutcome.OnCooldown, 0, claim.Remaining, 0);

        var amount = CurrencyRng.NextAmount(config.WorkMinReward, config.WorkMaxReward);
        var flavor = CurrencyRng.Next(WorkFlavorCount);

        await currencyService.CreditAsync(userId, amount, "Work", CurrencyCategory.Work, guildId, "work");

        return new EarnResult(EarnOutcome.Success, amount, TimeSpan.Zero, flavor);
    }

    /// <summary>
    ///     Attempts a crime, which pays considerably better than work but fails often enough that the
    ///     expected return stays below it at the default rates.
    /// </summary>
    /// <param name="guildId">The guild the command was used in.</param>
    /// <param name="userId">The user attempting the crime.</param>
    /// <returns>The outcome, and the amount gained or fined.</returns>
    public async Task<EarnResult> CrimeAsync(ulong guildId, ulong userId)
    {
        var config = await configService.GetConfigAsync(guildId);

        if (!config.CrimeEnabled)
            return new EarnResult(EarnOutcome.Disabled, 0, TimeSpan.Zero, 0);

        var claim = await cooldownService.TryClaimAsync(guildId, userId, CurrencyCooldownService.Crime,
            TimeSpan.FromSeconds(config.CrimeCooldownSeconds));

        if (!claim.Success)
            return new EarnResult(EarnOutcome.OnCooldown, 0, claim.Remaining, 0);

        if (CurrencyRng.Chance(config.CrimeSuccessChance))
        {
            var amount = CurrencyRng.NextAmount(config.CrimeMinReward, config.CrimeMaxReward);
            await currencyService.CreditAsync(userId, amount, "Crime", CurrencyCategory.Crime, guildId, "crime");

            return new EarnResult(EarnOutcome.Success, amount, TimeSpan.Zero,
                CurrencyRng.Next(CrimeSuccessFlavorCount));
        }

        var fine = CurrencyRng.NextAmount(config.CrimeFineMin, config.CrimeFineMax);
        var wallet = await currencyService.GetUserBalanceAsync(userId, guildId);
        var charged = Math.Min(fine, wallet);

        if (charged > 0)
            await currencyService.TryDebitAsync(userId, charged, "Crime fine", CurrencyCategory.CrimeFine, guildId,
                "crime");

        return new EarnResult(EarnOutcome.Failed, charged, TimeSpan.Zero, CurrencyRng.Next(CrimeFailFlavorCount));
    }

    /// <summary>
    ///     Attempts to rob another user's wallet. The bank is deliberately out of reach, which is what
    ///     gives banking a purpose beyond storage.
    /// </summary>
    /// <param name="guildId">The guild the command was used in.</param>
    /// <param name="robberId">The user attempting the robbery.</param>
    /// <param name="targetId">The user being robbed.</param>
    /// <returns>The outcome, and the amount stolen or fined.</returns>
    public async Task<RobResult> RobAsync(ulong guildId, ulong robberId, ulong targetId)
    {
        var config = await configService.GetConfigAsync(guildId);

        if (!config.RobEnabled)
            return new RobResult(RobOutcome.Disabled, 0, TimeSpan.Zero);
        if (robberId == targetId)
            return new RobResult(RobOutcome.SelfTarget, 0, TimeSpan.Zero);

        var targetWallet = await currencyService.GetUserBalanceAsync(targetId, guildId);

        if (targetWallet < config.RobMinimumWallet)
            return new RobResult(RobOutcome.TargetTooPoor, 0, TimeSpan.Zero);

        var robberWallet = await currencyService.GetUserBalanceAsync(robberId, guildId);

        if (robberWallet <= 0)
            return new RobResult(RobOutcome.RobberTooPoor, 0, TimeSpan.Zero);

        var claim = await cooldownService.TryClaimAsync(guildId, robberId, CurrencyCooldownService.Rob,
            TimeSpan.FromSeconds(config.RobCooldownSeconds));

        if (!claim.Success)
            return new RobResult(RobOutcome.OnCooldown, 0, claim.Remaining);

        if (CurrencyRng.Chance(config.RobSuccessChance))
        {
            var maximum = Math.Max(1, targetWallet * config.RobMaxStealPercent / 100);
            var stolen = CurrencyRng.NextAmount(1, maximum);

            return await currencyService.TryTransferAsync(targetId, robberId, stolen, stolen, "Robbery",
                CurrencyCategory.Rob, guildId, "rob")
                ? new RobResult(RobOutcome.Success, stolen, TimeSpan.Zero)
                : new RobResult(RobOutcome.TargetTooPoor, 0, TimeSpan.Zero);
        }

        var fine = Math.Max(1, robberWallet * config.RobFinePercent / 100);
        var charged = Math.Min(fine, robberWallet);

        await currencyService.TryDebitAsync(robberId, charged, "Caught robbing", CurrencyCategory.RobFine, guildId,
            "rob");

        return new RobResult(RobOutcome.Caught, charged, TimeSpan.Zero);
    }

    /// <summary>
    ///     Transfers currency from one user to another, destroying the configured tax percentage on the way.
    /// </summary>
    /// <param name="guildId">The guild the command was used in.</param>
    /// <param name="fromUserId">The sending user.</param>
    /// <param name="toUserId">The receiving user.</param>
    /// <param name="amount">The amount to send before tax.</param>
    /// <param name="targetIsBot">Whether the recipient is a bot.</param>
    /// <returns>The outcome, and the amounts sent, received and taxed.</returns>
    public async Task<PayResult> PayAsync(ulong guildId, ulong fromUserId, ulong toUserId, long amount,
        bool targetIsBot)
    {
        var config = await configService.GetConfigAsync(guildId);

        if (!config.PayEnabled)
            return new PayResult(PayOutcome.Disabled, 0, 0, 0, TimeSpan.Zero);
        if (fromUserId == toUserId)
            return new PayResult(PayOutcome.SelfTarget, 0, 0, 0, TimeSpan.Zero);
        if (targetIsBot)
            return new PayResult(PayOutcome.BotTarget, 0, 0, 0, TimeSpan.Zero);
        if (amount < config.PayMinimum)
            return new PayResult(PayOutcome.BelowMinimum, 0, 0, 0, TimeSpan.Zero);

        var claim = await cooldownService.TryClaimAsync(guildId, fromUserId, CurrencyCooldownService.Pay,
            TimeSpan.FromSeconds(config.PayCooldownSeconds));

        if (!claim.Success)
            return new PayResult(PayOutcome.OnCooldown, 0, 0, 0, claim.Remaining);

        var tax = config.PayTaxPercent <= 0 ? 0 : amount * config.PayTaxPercent / 100;
        var received = amount - tax;

        if (!await currencyService.TryTransferAsync(fromUserId, toUserId, amount, received, "Transfer",
                CurrencyCategory.PaySent, guildId, "pay"))
        {
            await cooldownService.ResetAsync(guildId, fromUserId, CurrencyCooldownService.Pay);
            return new PayResult(PayOutcome.InsufficientFunds, 0, 0, 0, TimeSpan.Zero);
        }

        return new PayResult(PayOutcome.Success, amount, received, tax, TimeSpan.Zero);
    }

    /// <summary>
    ///     Pays any interest a user's banked balance has accrued since it was last collected.
    /// </summary>
    /// <remarks>
    ///     Accrual is lazy rather than scheduled: a background job would have to walk every balance row
    ///     in every guild on a timer, while this costs one cooldown check on the commands that already
    ///     read the balance.
    /// </remarks>
    /// <param name="guildId">The guild the balance belongs to.</param>
    /// <param name="userId">The user to pay.</param>
    /// <returns>The interest paid, or zero if none had accrued.</returns>
    public async Task<long> AccrueBankInterestAsync(ulong guildId, ulong userId)
    {
        var config = await configService.GetConfigAsync(guildId);

        if (!config.BankEnabled || config.BankInterestPercent <= 0 || config.BankInterestHours <= 0)
            return 0;

        var (_, bank) = await currencyService.GetBalancesAsync(userId, guildId);

        if (bank <= 0)
            return 0;

        var claim = await cooldownService.TryClaimAsync(guildId, userId, CurrencyCooldownService.BankInterest,
            TimeSpan.FromHours(config.BankInterestHours));

        if (!claim.Success)
            return 0;

        var interest = (long)(bank * config.BankInterestPercent / 100);

        if (interest <= 0)
            return 0;

        await currencyService.CreditAsync(userId, interest, "Bank interest", CurrencyCategory.BankInterest, guildId,
            "bank");
        return interest;
    }
}