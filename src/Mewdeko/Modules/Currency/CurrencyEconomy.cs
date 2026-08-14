using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Currency.Models;
using Mewdeko.Modules.Currency.Services;

namespace Mewdeko.Modules.Currency;

/// <summary>
///     The earning and circulation commands: working, crime, robbery, transfers and banking.
/// </summary>
public partial class Currency
{
    /// <summary>
    ///     The economy service handling work, crime, robbery and transfers.
    /// </summary>
    public EconomyService EconomyService { get; set; }

    /// <summary>
    ///     The per-guild economy configuration service.
    /// </summary>
    public CurrencyConfigService ConfigService { get; set; }

    /// <summary>
    ///     The cooldown service backing the daily reward and the per-game rate limit.
    /// </summary>
    public CurrencyCooldownService CooldownService { get; set; }

    /// <summary>
    ///     Works a shift for a random payout, on a cooldown the server sets.
    /// </summary>
    /// <example>.work</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Work()
    {
        var result = await EconomyService.WorkAsync(ctx.Guild.Id, ctx.User.Id);

        switch (result.Outcome)
        {
            case EarnOutcome.Disabled:
                await ErrorAsync(Strings.WorkDisabled(ctx.Guild.Id));
                return;
            case EarnOutcome.OnCooldown:
                await ErrorAsync(Strings.WorkCooldown(ctx.Guild.Id, result.Remaining.ToReadableDuration()));
                return;
        }

        var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);

        await ConfirmAsync(Strings.WorkSuccess(ctx.Guild.Id, WorkFlavor(result.FlavorIndex), result.Amount, emote));
    }

    /// <summary>
    ///     Attempts a crime for a larger payout than working, at the risk of being fined instead.
    /// </summary>
    /// <example>.crime</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Crime()
    {
        var result = await EconomyService.CrimeAsync(ctx.Guild.Id, ctx.User.Id);
        var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);

        switch (result.Outcome)
        {
            case EarnOutcome.Disabled:
                await ErrorAsync(Strings.CrimeDisabled(ctx.Guild.Id));
                return;
            case EarnOutcome.OnCooldown:
                await ErrorAsync(Strings.CrimeCooldown(ctx.Guild.Id, result.Remaining.ToReadableDuration()));
                return;
            case EarnOutcome.Success:
                await ConfirmAsync(Strings.CrimeSuccess(ctx.Guild.Id, CrimeSuccessFlavor(result.FlavorIndex),
                    result.Amount, emote));
                return;
            default:
                await ErrorAsync(Strings.CrimeFailed(ctx.Guild.Id, CrimeFailFlavor(result.FlavorIndex), result.Amount,
                    emote));
                return;
        }
    }

    /// <summary>
    ///     Attempts to steal from another user's wallet. Banked currency is out of reach.
    /// </summary>
    /// <param name="target">The user to rob.</param>
    /// <example>.rob @user</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Rob(IUser target)
    {
        var result = await EconomyService.RobAsync(ctx.Guild.Id, ctx.User.Id, target.Id);
        var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);

        switch (result.Outcome)
        {
            case RobOutcome.Disabled:
                await ErrorAsync(Strings.RobDisabled(ctx.Guild.Id));
                return;
            case RobOutcome.OnCooldown:
                await ErrorAsync(Strings.RobCooldown(ctx.Guild.Id, result.Remaining.ToReadableDuration()));
                return;
            case RobOutcome.SelfTarget:
                await ErrorAsync(Strings.RobSelf(ctx.Guild.Id));
                return;
            case RobOutcome.TargetTooPoor:
                await ErrorAsync(Strings.RobTargetTooPoor(ctx.Guild.Id, target.Mention));
                return;
            case RobOutcome.RobberTooPoor:
                await ErrorAsync(Strings.RobSelfTooPoor(ctx.Guild.Id));
                return;
            case RobOutcome.Success:
                await ConfirmAsync(Strings.RobSuccess(ctx.Guild.Id, result.Amount, emote, target.Mention));
                return;
            default:
                await ErrorAsync(Strings.RobCaught(ctx.Guild.Id, result.Amount, emote));
                return;
        }
    }

    /// <summary>
    ///     Sends currency to another user, minus any transfer tax the server charges.
    /// </summary>
    /// <param name="target">The user to send currency to.</param>
    /// <param name="amount">The amount to send before tax.</param>
    /// <example>.pay @user 500</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Pay(IUser target, long amount)
    {
        var result = await EconomyService.PayAsync(ctx.Guild.Id, ctx.User.Id, target.Id, amount, target.IsBot);
        var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);

        switch (result.Outcome)
        {
            case PayOutcome.Disabled:
                await ErrorAsync(Strings.PayDisabled(ctx.Guild.Id));
                return;
            case PayOutcome.OnCooldown:
                await ErrorAsync(Strings.PayCooldown(ctx.Guild.Id, result.Remaining.ToReadableDuration()));
                return;
            case PayOutcome.SelfTarget:
                await ErrorAsync(Strings.PaySelf(ctx.Guild.Id));
                return;
            case PayOutcome.BotTarget:
                await ErrorAsync(Strings.PayBot(ctx.Guild.Id));
                return;
            case PayOutcome.BelowMinimum:
                await ErrorAsync(Strings.PayBelowMinimum(ctx.Guild.Id,
                    (await ConfigService.GetConfigAsync(ctx.Guild.Id)).PayMinimum, emote));
                return;
            case PayOutcome.InsufficientFunds:
                await ErrorAsync(Strings.PayInsufficient(ctx.Guild.Id, amount, emote));
                return;
            default:
                await ConfirmAsync(result.Tax > 0
                    ? Strings.PaySuccessTaxed(ctx.Guild.Id, result.Received, emote, target.Mention, result.Tax)
                    : Strings.PaySuccess(ctx.Guild.Id, result.Received, emote, target.Mention));
                return;
        }
    }

    /// <summary>
    ///     Shows your wallet and bank balances, collecting any interest that has accrued.
    /// </summary>
    /// <param name="user">The user to inspect. Defaults to yourself.</param>
    /// <example>.bank</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Bank(IUser? user = null)
    {
        user ??= ctx.User;

        if (user.Id == ctx.User.Id)
            await EconomyService.AccrueBankInterestAsync(ctx.Guild.Id, ctx.User.Id);

        var (wallet, bank) = await Service.GetBalancesAsync(user.Id, ctx.Guild.Id);
        var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);
        var config = await ConfigService.GetConfigAsync(ctx.Guild.Id);

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.BankTitle(ctx.Guild.Id, user.Username))
            .AddField(Strings.BankWallet(ctx.Guild.Id), $"{wallet:N0} {emote}", true)
            .AddField(Strings.BankBanked(ctx.Guild.Id), $"{bank:N0} {emote}", true)
            .AddField(Strings.BankNetWorth(ctx.Guild.Id), $"{wallet + bank:N0} {emote}", true);

        if (config.BankCapacity > 0)
            eb.WithFooter(Strings.BankCapacityFooter(ctx.Guild.Id, config.BankCapacity, emote));

        await ctx.Channel.SendMessageAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Moves currency from your wallet into the bank, where it is safe from robbery.
    /// </summary>
    /// <param name="amount">The amount to deposit, or "all" to deposit the whole wallet.</param>
    /// <example>.deposit 500</example>
    /// <example>.deposit all</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Deposit([Remainder] string amount)
    {
        var config = await ConfigService.GetConfigAsync(ctx.Guild.Id);

        if (!config.BankEnabled)
        {
            await ErrorAsync(Strings.BankDisabled(ctx.Guild.Id));
            return;
        }

        var (wallet, _) = await Service.GetBalancesAsync(ctx.User.Id, ctx.Guild.Id);
        var value = ParseAmount(amount, wallet);

        if (value <= 0)
        {
            await ErrorAsync(Strings.BankInvalidAmount(ctx.Guild.Id));
            return;
        }

        var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);

        if (!await Service.TryDepositAsync(ctx.User.Id, value, config.BankCapacity, ctx.Guild.Id))
        {
            await ErrorAsync(config.BankCapacity > 0
                ? Strings.BankDepositFailedCapacity(ctx.Guild.Id, config.BankCapacity, emote)
                : Strings.BankDepositFailed(ctx.Guild.Id, value, emote));
            return;
        }

        await ConfirmAsync(Strings.BankDeposited(ctx.Guild.Id, value, emote));
    }

    /// <summary>
    ///     Moves currency from the bank back into your wallet so it can be spent.
    /// </summary>
    /// <param name="amount">The amount to withdraw, or "all" to withdraw everything.</param>
    /// <example>.withdraw 500</example>
    /// <example>.withdraw all</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Withdraw([Remainder] string amount)
    {
        var config = await ConfigService.GetConfigAsync(ctx.Guild.Id);

        if (!config.BankEnabled)
        {
            await ErrorAsync(Strings.BankDisabled(ctx.Guild.Id));
            return;
        }

        await EconomyService.AccrueBankInterestAsync(ctx.Guild.Id, ctx.User.Id);

        var (_, bank) = await Service.GetBalancesAsync(ctx.User.Id, ctx.Guild.Id);
        var value = ParseAmount(amount, bank);

        if (value <= 0)
        {
            await ErrorAsync(Strings.BankInvalidAmount(ctx.Guild.Id));
            return;
        }

        var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);

        if (!await Service.TryWithdrawAsync(ctx.User.Id, value, ctx.Guild.Id))
        {
            await ErrorAsync(Strings.BankWithdrawFailed(ctx.Guild.Id, value, emote));
            return;
        }

        await ConfirmAsync(Strings.BankWithdrew(ctx.Guild.Id, value, emote));
    }

    /// <summary>
    ///     Maps a flavor index to its localized line. Written out rather than composed from a key at
    ///     runtime so every variant stays visible to the localization generator and to translators.
    /// </summary>
    private string WorkFlavor(int index)
    {
        var id = ctx.Guild.Id;

        return index switch
        {
            0 => Strings.WorkFlavorZero(id),
            1 => Strings.WorkFlavorOne(id),
            2 => Strings.WorkFlavorTwo(id),
            3 => Strings.WorkFlavorThree(id),
            4 => Strings.WorkFlavorFour(id),
            _ => Strings.WorkFlavorFive(id)
        };
    }

    /// <summary>
    ///     Maps a successful crime's flavor index to its localized line.
    /// </summary>
    private string CrimeSuccessFlavor(int index)
    {
        var id = ctx.Guild.Id;

        return index switch
        {
            0 => Strings.CrimeSuccessFlavorZero(id),
            1 => Strings.CrimeSuccessFlavorOne(id),
            2 => Strings.CrimeSuccessFlavorTwo(id),
            3 => Strings.CrimeSuccessFlavorThree(id),
            _ => Strings.CrimeSuccessFlavorFour(id)
        };
    }

    /// <summary>
    ///     Maps a failed crime's flavor index to its localized line.
    /// </summary>
    private string CrimeFailFlavor(int index)
    {
        var id = ctx.Guild.Id;

        return index switch
        {
            0 => Strings.CrimeFailFlavorZero(id),
            1 => Strings.CrimeFailFlavorOne(id),
            2 => Strings.CrimeFailFlavorTwo(id),
            3 => Strings.CrimeFailFlavorThree(id),
            _ => Strings.CrimeFailFlavorFour(id)
        };
    }

    /// <summary>
    ///     Resolves an amount argument that may be a number or the word "all".
    /// </summary>
    private static long ParseAmount(string input, long available)
    {
        if (input.Equals("all", StringComparison.OrdinalIgnoreCase) ||
            input.Equals("max", StringComparison.OrdinalIgnoreCase))
            return available;

        if (input.Equals("half", StringComparison.OrdinalIgnoreCase))
            return available / 2;

        return long.TryParse(input.Replace(",", "").Replace("_", ""), out var parsed) ? parsed : 0;
    }
}