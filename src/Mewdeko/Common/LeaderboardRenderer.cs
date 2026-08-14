using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Modules.Currency.Services;
using Mewdeko.Modules.Xp.Services;
using Mewdeko.Services.Strings;

namespace Mewdeko.Common;

/// <summary>
///     Builds and sends the XP and currency leaderboards.
/// </summary>
/// <remarks>
///     Both leaderboards are reachable from a text command and from the disambiguation prompt shown
///     when a server uses XP and currency at once. Rendering lives here so those two entry points
///     cannot drift apart.
/// </remarks>
public static class LeaderboardRenderer
{
    /// <summary>
    ///     Custom ID prefix for the button that picks the XP leaderboard.
    /// </summary>
    public const string XpButtonId = "lb_pick_xp";

    /// <summary>
    ///     Custom ID prefix for the button that picks the currency leaderboard.
    /// </summary>
    public const string CurrencyButtonId = "lb_pick_currency";

    /// <summary>
    ///     Builds the prompt asking which leaderboard the caller meant.
    /// </summary>
    /// <param name="guildId">The guild the prompt is shown in.</param>
    /// <param name="userId">The user allowed to answer, encoded into the button IDs.</param>
    /// <param name="strings">The localized strings provider.</param>
    /// <returns>The embed and buttons to send.</returns>
    public static (Discord.Embed Embed, MessageComponent Components) BuildPicker(ulong guildId, ulong userId,
        GeneratedBotStrings strings)
    {
        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(strings.LeaderboardPickTitle(guildId))
            .WithDescription(strings.LeaderboardPickDescription(guildId))
            .Build();

        var components = new ComponentBuilder()
            .WithButton(strings.LeaderboardPickXp(guildId), $"{XpButtonId}_{userId}", ButtonStyle.Primary)
            .WithButton(strings.LeaderboardPickCurrency(guildId), $"{CurrencyButtonId}_{userId}", ButtonStyle.Secondary)
            .Build();

        return (embed, components);
    }

    /// <summary>
    ///     Sends the XP leaderboard as a paginated message.
    /// </summary>
    /// <param name="guild">The guild to rank.</param>
    /// <param name="user">The user allowed to page through the result.</param>
    /// <param name="channel">The channel to send to.</param>
    /// <param name="interactive">The interactive service driving the paginator.</param>
    /// <param name="xpService">The XP service supplying the ranking.</param>
    /// <param name="strings">The localized strings provider.</param>
    /// <param name="page">The one-based page to open on.</param>
    /// <returns><see langword="false" /> if the guild has no ranked users.</returns>
    public static async Task<bool> SendXpAsync(IGuild guild, IUser user, IMessageChannel channel,
        InteractiveService interactive, XpService xpService, GeneratedBotStrings strings, int page = 1)
    {
        if (page < 1)
            page = 1;

        var (entries, totalCount) = await xpService.GetLeaderboardAsync(guild.Id, page);

        if (entries.Count == 0)
            return false;

        var users = await guild.GetUsersAsync();
        var userDict = users.ToDictionary(u => u.Id, u => u);

        const int pageSize = 10;
        var maxPageIndex = Math.Max(0, (int)Math.Ceiling(totalCount / (double)pageSize) - 1);

        var paginator = new LazyPaginatorBuilder()
            .AddUser(user)
            .WithPageFactory(BuildPage)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(maxPageIndex)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactive.SendPaginatorAsync(paginator, channel, TimeSpan.FromMinutes(60));
        return true;

        async Task<PageBuilder> BuildPage(int pageNum)
        {
            var pageData = await xpService.GetLeaderboardAsync(guild.Id, pageNum + 1);

            var lines = pageData.Users.Select(entry => strings.XpLeaderboardLine(
                guild.Id,
                entry.Rank,
                userDict.TryGetValue(entry.UserId, out var guildUser) ? guildUser.ToString() : entry.UserId.ToString(),
                entry.Level,
                entry.TotalXp));

            return new PageBuilder()
                .WithOkColor()
                .WithTitle(strings.XpLeaderboardTitle(guild.Id))
                .WithDescription(string.Join("\n", lines));
        }
    }

    /// <summary>
    ///     Sends the currency leaderboard as a paginated message, ranked by net worth.
    /// </summary>
    /// <remarks>
    ///     Ranking uses wallet plus bank. Ranking on the wallet alone would push anyone who banked their
    ///     currency down the board for having used the bank at all.
    /// </remarks>
    /// <param name="guild">The guild to rank.</param>
    /// <param name="user">The user allowed to page through the result.</param>
    /// <param name="channel">The channel to send to.</param>
    /// <param name="interactive">The interactive service driving the paginator.</param>
    /// <param name="currencyService">The currency service supplying balances.</param>
    /// <param name="strings">The localized strings provider.</param>
    /// <returns><see langword="false" /> if nobody in the guild holds any currency.</returns>
    public static async Task<bool> SendCurrencyAsync(IGuild guild, IUser user, IMessageChannel channel,
        InteractiveService interactive, ICurrencyService currencyService, GeneratedBotStrings strings)
    {
        var holders = (await currencyService.GetAllUserBalancesAsync(guild.Id))
            .Where(x => x.NetWorth > 0)
            .OrderByDescending(x => x.NetWorth)
            .ToList();

        if (holders.Count == 0)
            return false;

        var emote = await currencyService.GetCurrencyEmote(guild.Id);

        var paginator = new LazyPaginatorBuilder()
            .AddUser(user)
            .WithPageFactory(BuildPage)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(Math.Max(0, (holders.Count - 1) / 10))
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactive.SendPaginatorAsync(paginator, channel, TimeSpan.FromMinutes(60));
        return true;

        async Task<PageBuilder> BuildPage(int index)
        {
            var page = new PageBuilder()
                .WithOkColor()
                .WithTitle(strings.LeaderboardTitle(guild.Id))
                .WithDescription(strings.LeaderboardDescription(guild.Id, holders.Count, guild.Name));

            for (var i = index * 10; i < (index + 1) * 10 && i < holders.Count; i++)
            {
                var holder = holders[i];
                var member = await guild.GetUserAsync(holder.UserId);

                page.AddField(
                    strings.LeaderboardUserEntry(guild.Id, i + 1, member?.Username ?? holder.UserId.ToString()),
                    strings.LeaderboardBalanceEntry(guild.Id, holder.NetWorth, emote),
                    true);
            }

            return page;
        }
    }
}