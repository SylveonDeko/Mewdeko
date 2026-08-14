using Discord.Interactions;
using Mewdeko.Database.Enums;
using Mewdeko.Modules.Currency.Common;
using Mewdeko.Modules.Currency.Services;

namespace Mewdeko.Modules.Currency;

/// <summary>
///     Slash command module for currency game interactions.
/// </summary>
[Group("currency", "Currency and gambling games")]
public class SlashCurrency : MewdekoSlashCommandModule
{
    /// <summary>
    ///     The currency service for managing user balances.
    /// </summary>
    public ICurrencyService Service { get; set; }

    /// <summary>
    ///     The trivia chain service for managing trivia chain games.
    /// </summary>
    public ITriviaChainService TriviaChainService { get; set; }

    /// <summary>
    ///     Handle dice duel challenge acceptance.
    /// </summary>
    [ComponentInteraction("diceduel_accept_*_*", true)]
    public async Task DiceDuelAccept(ulong challengerId, long betAmount)
    {
        if (ctx.User.Id == challengerId)
        {
            await RespondAsync(Strings.DiceDuelCannotAcceptOwnChallenge(ctx.Guild.Id), ephemeral: true);
            return;
        }

        if (!await Service.TryDebitAsync(ctx.User.Id, betAmount, Strings.DiceDuelTransactionStake(ctx.Guild.Id),
                CurrencyCategory.GameBet, ctx.Guild.Id, "diceduel"))
        {
            await RespondAsync(
                Strings.DiceDuelInsufficientFundsAccept(ctx.Guild.Id, await Service.GetCurrencyEmote(ctx.Guild.Id)),
                ephemeral: true);
            return;
        }

        if (!await Service.TryDebitAsync(challengerId, betAmount, Strings.DiceDuelTransactionStake(ctx.Guild.Id),
                CurrencyCategory.GameBet, ctx.Guild.Id, "diceduel"))
        {
            await Service.CreditAsync(ctx.User.Id, betAmount, Strings.DiceDuelTransactionRefund(ctx.Guild.Id),
                CurrencyCategory.GamePayout, ctx.Guild.Id, "diceduel");

            await RespondAsync(
                Strings.DiceDuelChallengerBroke(ctx.Guild.Id, await Service.GetCurrencyEmote(ctx.Guild.Id)),
                ephemeral: true);
            return;
        }

        var challengerRoll = CurrencyRng.Next(1, 7);
        var accepterRoll = CurrencyRng.Next(1, 7);
        var pot = betAmount * 2;

        var eb = new EmbedBuilder()
            .WithTitle(Strings.DiceDuelResultTitle(ctx.Guild.Id))
            .WithColor(challengerRoll == accepterRoll ? Color.Gold : Color.Green);

        var challenger = await ctx.Guild.GetUserAsync(challengerId);

        if (challengerRoll > accepterRoll)
        {
            await Service.CreditAsync(challengerId, pot, Strings.DiceDuelTransactionWon(ctx.Guild.Id),
                CurrencyCategory.GamePayout, ctx.Guild.Id, "diceduel");

            eb.WithDescription(Strings.DiceDuelChallengerWin(ctx.Guild.Id, challenger?.Mention ?? "Challenger",
                challengerRoll, ctx.User.Mention, accepterRoll));
        }
        else if (accepterRoll > challengerRoll)
        {
            await Service.CreditAsync(ctx.User.Id, pot, Strings.DiceDuelTransactionWon(ctx.Guild.Id),
                CurrencyCategory.GamePayout, ctx.Guild.Id, "diceduel");

            eb.WithDescription(Strings.DiceDuelAccepterWin(ctx.Guild.Id, ctx.User.Mention, accepterRoll,
                challenger?.Mention ?? "Challenger", challengerRoll));
        }
        else
        {
            await Service.CreditAsync(challengerId, betAmount, Strings.DiceDuelTransactionRefund(ctx.Guild.Id),
                CurrencyCategory.GamePayout, ctx.Guild.Id, "diceduel");
            await Service.CreditAsync(ctx.User.Id, betAmount, Strings.DiceDuelTransactionRefund(ctx.Guild.Id),
                CurrencyCategory.GamePayout, ctx.Guild.Id, "diceduel");

            eb.WithDescription(Strings.DiceDuelTie(ctx.Guild.Id, challengerRoll.ToString()));
        }

        await RespondAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Handle dice duel challenge decline.
    /// </summary>
    [ComponentInteraction("diceduel_decline_*", true)]
    public async Task DiceDuelDecline(ulong challengerId)
    {
        if (ctx.User.Id == challengerId)
        {
            await RespondAsync(Strings.DiceDuelCannotDeclineOwnChallenge(ctx.Guild.Id), ephemeral: true);
            return;
        }

        await RespondAsync(Strings.DiceDuelDeclined(ctx.Guild.Id));
    }

    /// <summary>
    ///     Handle trivia chain cash out.
    /// </summary>
    [ComponentInteraction("triviachain_cashout_*_*", true)]
    public async Task TriviaChainCashOut(ulong userId, long winnings)
    {
        if (ctx.User.Id != userId)
        {
            await RespondAsync(Strings.TriviaChainNotYourButton(ctx.Guild.Id), ephemeral: true);
            return;
        }

        if (winnings > 0)
        {
            await Service.CreditAsync(userId, winnings, Strings.TriviaChainTransactionCashedOut(ctx.Guild.Id),
                CurrencyCategory.GamePayout, ctx.Guild.Id, "triviachain");

            await RespondAsync(Strings.TriviaChainCashedOut(ctx.Guild.Id, winnings,
                await Service.GetCurrencyEmote(ctx.Guild.Id)));
        }
        else
        {
            await RespondAsync(Strings.TriviaChainNothingToCashOut(ctx.Guild.Id), ephemeral: true);
        }
    }

    /// <summary>
    ///     Handle trivia chain answer selection.
    /// </summary>
    [ComponentInteraction("triviachain_answer_*_*", true)]
    public async Task TriviaChainAnswer(ulong userId, string answerIndex)
    {
        if (ctx.User.Id != userId)
        {
            await RespondAsync(Strings.TriviaChainNotYourButton(ctx.Guild.Id), ephemeral: true);
            return;
        }

        // Get the trivia chain state
        var chainState = TriviaChainService.GetTriviaChainState(userId);
        if (chainState == null)
        {
            await RespondAsync(Strings.TriviaChainExpired(ctx.Guild.Id), ephemeral: true);
            return;
        }

        // Process the answer using the service
        var result = await TriviaChainService.ProcessTriviaAnswerAsync(ctx, answerIndex, chainState, Service);

        if (result.GameCompleted || result.GameFailed)
        {
            await RespondAsync(embed: result.ResultEmbed, components: result.NextComponents);
        }
        else if (result.UpdatedState != null)
        {
            await RespondAsync(embed: result.ResultEmbed, components: result.NextComponents);
        }
    }
}