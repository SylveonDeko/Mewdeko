using Discord.Interactions;
using Fergun.Interactive;
using Mewdeko.Modules.Currency.Services;
using Mewdeko.Modules.Xp.Services;

namespace Mewdeko.Modules.Utility;

/// <summary>
///     Handles the prompt shown when a leaderboard request could mean either XP or currency.
/// </summary>
/// <remarks>
///     The prompt only appears for servers running both systems, so this exists purely to route the
///     answer. Both branches call the same renderer the text commands use.
/// </remarks>
public class LeaderboardInteractions : MewdekoSlashCommandModule
{
    private readonly ICurrencyService currencyService;
    private readonly InteractiveService interactive;
    private readonly XpService xpService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LeaderboardInteractions" /> class.
    /// </summary>
    /// <param name="xpService">The XP service.</param>
    /// <param name="currencyService">The currency service.</param>
    /// <param name="interactive">The interactive service driving the paginators.</param>
    public LeaderboardInteractions(XpService xpService, ICurrencyService currencyService,
        InteractiveService interactive)
    {
        this.xpService = xpService;
        this.currencyService = currencyService;
        this.interactive = interactive;
    }

    /// <summary>
    ///     Shows the XP leaderboard after the caller picks it from the prompt.
    /// </summary>
    /// <param name="userId">The user who ran the original command.</param>
    [ComponentInteraction($"{LeaderboardRenderer.XpButtonId}_*", true)]
    public async Task PickXp(ulong userId)
    {
        if (!await ClaimPrompt(userId))
            return;

        if (!await LeaderboardRenderer.SendXpAsync(ctx.Guild, ctx.User, ctx.Channel, interactive, xpService, Strings))
            await ctx.Channel.SendErrorAsync(Strings.XpLeaderboardEmpty(ctx.Guild.Id), Config);
    }

    /// <summary>
    ///     Shows the currency leaderboard after the caller picks it from the prompt.
    /// </summary>
    /// <param name="userId">The user who ran the original command.</param>
    [ComponentInteraction($"{LeaderboardRenderer.CurrencyButtonId}_*", true)]
    public async Task PickCurrency(ulong userId)
    {
        if (!await ClaimPrompt(userId))
            return;

        if (!await LeaderboardRenderer.SendCurrencyAsync(ctx.Guild, ctx.User, ctx.Channel, interactive,
                currencyService, Strings))
            await ctx.Channel.SendErrorAsync(Strings.LeaderboardEmpty(ctx.Guild.Id), Config);
    }

    /// <summary>
    ///     Verifies the responder owns the prompt and clears it, so the buttons cannot be reused or
    ///     hijacked by a bystander.
    /// </summary>
    private async Task<bool> ClaimPrompt(ulong userId)
    {
        if (ctx.User.Id != userId)
        {
            await RespondAsync(Strings.LeaderboardPickNotYours(ctx.Guild.Id), ephemeral: true);
            return false;
        }

        if (ctx.Interaction is IComponentInteraction component)
            await component.Message.DeleteAsync();

        return true;
    }
}