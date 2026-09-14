using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Database.Enums;
using Mewdeko.Modules.Currency.Models;
using Mewdeko.Modules.Currency.Services;

namespace Mewdeko.Modules.Currency;

/// <summary>
///     The earning and circulation slash commands: balances, daily reward, working, crime, robbery,
///     transfers, banking, transactions and the leaderboard.
/// </summary>
public partial class SlashCurrency
{
    /// <summary>
    ///     The economy service handling work, crime, robbery and transfers.
    /// </summary>
    public EconomyService EconomyService { get; set; }

    /// <summary>
    ///     The interactive service driving paginators.
    /// </summary>
    public InteractiveService Interactive { get; set; }

    /// <summary>
    ///     Checks the current balance of the user.
    /// </summary>
    [SlashCommand("cash", "Shows your wallet and bank balance")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Cash()
    {
        var (wallet, bank) = await Service.GetBalancesAsync(ctx.User.Id, ctx.Guild.Id);
        var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithDescription(bank > 0
                ? Strings.CashBalanceWithBank(ctx.Guild.Id, wallet, emote, bank, wallet + bank)
                : Strings.CashBalance(ctx.Guild.Id, wallet, emote));

        await ctx.Interaction.RespondAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Allows the user to claim their daily reward.
    /// </summary>
    [SlashCommand("daily", "Claims your daily reward")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task DailyReward()
    {
        var (rewardAmount, cooldownSeconds) = await Service.GetReward(ctx.Guild.Id);
        if (rewardAmount == 0)
        {
            await ErrorAsync(Strings.DailyRewardNotSet(ctx.Guild.Id));
            return;
        }

        var claim = await CooldownService.TryClaimAsync(ctx.Guild.Id, ctx.User.Id, CurrencyCooldownService.Daily,
            TimeSpan.FromSeconds(cooldownSeconds), true);

        if (!claim.Success)
        {
            await ErrorAsync(Strings.DailyRewardAlreadyClaimed(ctx.Guild.Id,
                TimestampTag.FromDateTime(DateTime.UtcNow + claim.Remaining)));
            return;
        }

        var config = await ConfigService.GetConfigAsync(ctx.Guild.Id);
        long bonus = 0;

        if (config.DailyStreakEnabled && config.DailyStreakBonus > 0 && claim.Streak > 1)
        {
            bonus = config.DailyStreakBonus * (claim.Streak - 1);

            if (config.DailyStreakMaxBonus > 0)
                bonus = Math.Min(bonus, config.DailyStreakMaxBonus);
        }

        var total = rewardAmount + bonus;
        var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);

        await Service.CreditAsync(ctx.User.Id, total, Strings.DailyRewardTransaction(ctx.Guild.Id),
            CurrencyCategory.Daily, ctx.Guild.Id, "daily");

        await ConfirmAsync(bonus > 0
            ? Strings.DailyRewardClaimedStreak(ctx.Guild.Id, total, emote, claim.Streak, bonus)
            : Strings.DailyRewardClaimed(ctx.Guild.Id, total, emote));
    }

    /// <summary>
    ///     Works a shift for a random payout, on a cooldown the server sets.
    /// </summary>
    [SlashCommand("work", "Works a shift for a random payout")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
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
    [SlashCommand("crime", "Attempts a crime for a bigger payout, at the risk of a fine")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
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
    [SlashCommand("rob", "Attempts to steal from another user's wallet")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Rob([Summary("target", "The user to rob")] IUser target)
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
    [SlashCommand("pay", "Sends currency to another user")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Pay([Summary("target", "The user to send currency to")] IUser target,
        [Summary("amount", "The amount to send before tax")]
        long amount)
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
    [SlashCommand("bank", "Shows wallet and bank balances, collecting accrued interest")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Bank([Summary("user", "The user to inspect, defaults to yourself")] IUser? user = null)
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

        await ctx.Interaction.RespondAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Moves currency from your wallet into the bank, where it is safe from robbery.
    /// </summary>
    /// <param name="amount">The amount to deposit, or "all" to deposit the whole wallet.</param>
    [SlashCommand("deposit", "Moves currency from your wallet into the bank")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Deposit([Summary("amount", "A number, or all, half, max")] string amount)
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
    [SlashCommand("withdraw", "Moves currency from the bank back into your wallet")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Withdraw([Summary("amount", "A number, or all, half, max")] string amount)
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
    ///     Retrieves and displays the transactions for a specified user or the current user.
    /// </summary>
    /// <param name="user">The user whose transactions are to be displayed. Defaults to the current user.</param>
    [SlashCommand("transactions", "Shows the currency transaction history of a user")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Transactions([Summary("user", "The user to inspect, defaults to yourself")] IUser? user = null)
    {
        await DeferAsync();
        user ??= ctx.User;

        var transactions = await Service.GetTransactionsAsync(user.Id, ctx.Guild.Id, 250);

        if (transactions.Count == 0)
        {
            await ErrorAsync(Strings.TransactionsNone(ctx.Guild.Id, user.Username));
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex((transactions.Count - 1) / 10)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await Interactive.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
            TimeSpan.FromMinutes(60),
            InteractionResponseType.DeferredChannelMessageWithSource).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int index)
        {
            var pageBuilder = new PageBuilder()
                .WithTitle(Strings.TransactionsTitle(ctx.Guild.Id))
                .WithDescription(Strings.TransactionsDescription(ctx.Guild.Id, user.Username))
                .WithColor(Color.Blue);

            var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);

            for (var i = index * 10; i < (index + 1) * 10 && i < transactions.Count; i++)
            {
                var entry = transactions[i];
                pageBuilder.AddField(
                    Strings.TransactionsEntry(ctx.Guild.Id, i + 1, entry.Description),
                    Strings.TransactionsDetails(ctx.Guild.Id, entry.Amount, emote,
                        TimestampTag.FromDateTime(entry.DateAdded ?? DateTime.UtcNow)));
            }

            return pageBuilder;
        }
    }

    /// <summary>
    ///     Displays the leaderboard of users with the highest net worth.
    /// </summary>
    [SlashCommand("leaderboard", "Shows the users with the highest net worth")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task CashLeaderboard()
    {
        await DeferAsync();

        var holders = (await Service.GetAllUserBalancesAsync(ctx.Guild.Id))
            .Where(x => x.NetWorth > 0)
            .OrderByDescending(x => x.NetWorth)
            .ToList();

        if (holders.Count == 0)
        {
            await ErrorAsync(Strings.LeaderboardEmpty(ctx.Guild.Id));
            return;
        }

        var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(BuildPage)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(Math.Max(0, (holders.Count - 1) / 10))
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await Interactive.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
            TimeSpan.FromMinutes(60),
            InteractionResponseType.DeferredChannelMessageWithSource).ConfigureAwait(false);

        async Task<PageBuilder> BuildPage(int index)
        {
            var page = new PageBuilder()
                .WithOkColor()
                .WithTitle(Strings.LeaderboardTitle(ctx.Guild.Id))
                .WithDescription(Strings.LeaderboardDescription(ctx.Guild.Id, holders.Count, ctx.Guild.Name));

            for (var i = index * 10; i < (index + 1) * 10 && i < holders.Count; i++)
            {
                var holder = holders[i];
                var member = await ctx.Guild.GetUserAsync(holder.UserId);

                page.AddField(
                    Strings.LeaderboardUserEntry(ctx.Guild.Id, i + 1, member?.Username ?? holder.UserId.ToString()),
                    Strings.LeaderboardBalanceEntry(ctx.Guild.Id, holder.NetWorth, emote),
                    true);
            }

            return page;
        }
    }

    /// <summary>
    ///     Maps a flavor index to its localized line.
    /// </summary>
    /// <param name="index">The flavor index chosen by the economy service.</param>
    /// <returns>The localized flavor line.</returns>
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
    /// <param name="index">The flavor index chosen by the economy service.</param>
    /// <returns>The localized flavor line.</returns>
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
    /// <param name="index">The flavor index chosen by the economy service.</param>
    /// <returns>The localized flavor line.</returns>
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
    /// <param name="input">The raw amount text.</param>
    /// <param name="available">The balance the words all, max and half are relative to.</param>
    /// <returns>The resolved amount, or zero when unparseable.</returns>
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