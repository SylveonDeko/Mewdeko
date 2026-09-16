using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.ServerStats.Common;
using Mewdeko.Modules.ServerStats.Services;
using Mewdeko.Modules.Utility.Common;

namespace Mewdeko.Modules.ServerStats;

/// <summary>
///     Pinned, auto refreshing leaderboards and charts, plus scheduled server reports.
/// </summary>
public class LiveBoardCommands(ServerReportService reports) : MewdekoModuleBase<LiveBoardService>
{
    /// <summary>
    ///     Creates a live board in a channel: a leaderboard, chart or overview the bot keeps up to date.
    /// </summary>
    /// <param name="channel">The channel to post in.</param>
    /// <param name="kind">What to show.</param>
    /// <param name="range">The window.</param>
    /// <param name="entries">Rows for leaderboards.</param>
    /// <param name="intervalMinutes">How often to refresh.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    [BotPerm(ChannelPermission.ManageMessages)]
    public async Task LiveBoardAdd(ITextChannel channel, LiveBoardKind kind, StatsRange range = StatsRange.Weekly,
        int entries = 10, int intervalMinutes = 15)
    {
        var existing = await Service.GetAsync(ctx.Guild.Id);
        if (existing.Count >= 10)
        {
            await ReplyErrorAsync(Strings.LiveBoardLimit(ctx.Guild.Id));
            return;
        }

        var board = await Service.CreateAsync((SocketGuild)ctx.Guild, channel, kind, range, true, entries,
            intervalMinutes);
        await ReplyConfirmAsync(Strings.LiveBoardCreated(ctx.Guild.Id, board.Id, kind.ToString(), channel.Mention,
            board.IntervalMinutes));
    }

    /// <summary>
    ///     Lists live boards.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task LiveBoardList()
    {
        var boards = await Service.GetAsync(ctx.Guild.Id);
        if (boards.Count == 0)
        {
            await ReplyErrorAsync(Strings.LiveBoardNone(ctx.Guild.Id));
            return;
        }

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.LiveBoardListTitle(ctx.Guild.Id))
            .WithDescription(string.Join("\n", boards.Select(b =>
                $"`#{b.Id}` **{(LiveBoardKind)b.Kind}** ({((StatsRange)b.Range).DisplayName()}) in <#{b.ChannelId}>, every {b.IntervalMinutes}m")));
        await ctx.Channel.SendMessageAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Deletes a live board and its message.
    /// </summary>
    /// <param name="id">The board ID.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task LiveBoardRemove(int id)
    {
        await (await Service.DeleteAsync((SocketGuild)ctx.Guild, id)
            ? ReplyConfirmAsync(Strings.LiveBoardRemoved(ctx.Guild.Id, id))
            : ReplyErrorAsync(Strings.LiveBoardNotFound(ctx.Guild.Id)));
    }

    /// <summary>
    ///     Refreshes every live board right now.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    [Ratelimit(60)]
    public async Task LiveBoardRefresh()
    {
        var boards = await Service.GetAsync(ctx.Guild.Id);
        foreach (var board in boards)
            await Service.RefreshAsync((SocketGuild)ctx.Guild, board);
        await ReplyConfirmAsync(Strings.LiveBoardRefreshed(ctx.Guild.Id, boards.Count));
    }

    /// <summary>
    ///     Sets the channel that receives server reports and enables them.
    /// </summary>
    /// <param name="channel">The channel, or omitted to disable reports.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task ServerReportChannel(ITextChannel? channel = null)
    {
        await reports.UpdateSettingsAsync(ctx.Guild.Id, s =>
        {
            s.ChannelId = channel?.Id;
            s.Enabled = channel != null;
        });
        await ReplyConfirmAsync(channel == null
            ? Strings.ReportDisabled(ctx.Guild.Id)
            : Strings.ReportChannelSet(ctx.Guild.Id, channel.Mention));
    }

    /// <summary>
    ///     Sets how often server reports are posted.
    /// </summary>
    /// <param name="frequency">Daily, weekly or monthly.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task ServerReportFrequency(ReportFrequency frequency)
    {
        await reports.UpdateSettingsAsync(ctx.Guild.Id, s => s.Frequency = (int)frequency);
        await ReplyConfirmAsync(Strings.ReportFrequencySet(ctx.Guild.Id, frequency.ToString()));
    }

    /// <summary>
    ///     Posts a server report right now, to the configured channel or the current one.
    /// </summary>
    /// <param name="frequency">The period to cover, or the configured one.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    [Ratelimit(120)]
    public async Task ServerReportNow(ReportFrequency? frequency = null)
    {
        var settings = await reports.GetSettingsAsync(ctx.Guild.Id);
        var (embed, image) = await reports.BuildAsync((SocketGuild)ctx.Guild,
            frequency ?? (ReportFrequency)settings.Frequency);
        await using (image)
        {
            await ctx.Channel.SendFileAsync(image, "graph.png", embed: embed);
        }
    }
}