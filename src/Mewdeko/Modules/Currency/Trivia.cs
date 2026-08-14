using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Currency.Services;

namespace Mewdeko.Modules.Currency;

/// <summary>
///     Trivia chain game commands for the Currency module.
/// </summary>
public partial class Currency
{
    /// <summary>
    ///     The trivia chain service for managing trivia chain games.
    /// </summary>
    public ITriviaChainService TriviaChainService { get; set; }

    /// <summary>
    ///     Play a trivia chain where you answer questions in succession for increasing rewards.
    /// </summary>
    /// <param name="betAmount">The amount to bet.</param>
    /// <param name="category">The trivia category.</param>
    /// <example>.triviachain 100 science</example>
    [Cmd]
    [Aliases]
    public async Task TriviaChain(long betAmount, string category = "general")
    {
        var validCategories = new[]
        {
            "general", "science", "history", "sports", "entertainment"
        };
        if (!validCategories.Contains(category.ToLower()))
        {
            await ReplyAsync(Strings.TriviaChainInvalidCategory(ctx.Guild.Id));
            return;
        }

        if (TriviaChainService.GetTriviaChainState(ctx.User.Id) != null)
        {
            await ReplyAsync(Strings.TriviaChainActiveGame(ctx.Guild.Id));
            return;
        }

        if (!await TryTakeBetAsync(betAmount, "triviachain"))
            return;

        // Start the trivia chain game
        var chainState =
            await TriviaChainService.StartTriviaChainAsync(ctx.User.Id, ctx.Guild.Id, betAmount, category.ToLower());

        var currentMultiplier = chainState.CurrentMultiplier;
        var potentialWin = (long)(betAmount * currentMultiplier);

        var eb = new EmbedBuilder()
            .WithTitle(Strings.TriviaChainTitle(ctx.Guild.Id))
            .WithColor(Color.Blue)
            .WithDescription(chainState.CurrentQuestion)
            .AddField(Strings.TriviaChainQuestion(ctx.Guild.Id, 1, chainState.CurrentQuestion), "_ _", true)
            .AddField(Strings.TriviaChainMultiplier(ctx.Guild.Id, $"{currentMultiplier:F1}x"), "_ _", true)
            .AddField(
                Strings.TriviaChainPotentialWin(ctx.Guild.Id, potentialWin,
                    await Service.GetCurrencyEmote(ctx.Guild.Id)), "_ _", true)
            .AddField(Strings.TriviaChainCurrentWinnings(ctx.Guild.Id, 0, await Service.GetCurrencyEmote(ctx.Guild.Id)),
                "_ _", true);

        var selectMenuBuilder = new SelectMenuBuilder()
            .WithPlaceholder(Strings.TriviaChainChooseAnswer(ctx.Guild.Id))
            .WithCustomId($"triviachain_answer_{ctx.User.Id}_0");

        for (var i = 0; i < chainState.CurrentOptions.Length; i++)
        {
            selectMenuBuilder.AddOption(chainState.CurrentOptions[i], i.ToString(),
                Strings.TriviaChainOption(ctx.Guild.Id, i + 1));
        }

        var componentBuilder = new ComponentBuilder()
            .WithSelectMenu(selectMenuBuilder);

        await ReplyAsync(embed: eb.Build(), components: componentBuilder.Build());
    }
}