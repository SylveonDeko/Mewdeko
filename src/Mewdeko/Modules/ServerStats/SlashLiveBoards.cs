using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.ServerStats.Common;
using Mewdeko.Modules.ServerStats.Services;
using Mewdeko.Modules.Utility.Common;

namespace Mewdeko.Modules.ServerStats;

/// <summary>
///     Slash commands for pinned, auto refreshing leaderboards and charts.
/// </summary>
[Group("liveboard", "Pinned leaderboards and charts that refresh themselves")]
public class SlashLiveBoards : MewdekoSlashModuleBase<LiveBoardService>
{
    /// <summary>
    ///     Creates a live board in a channel.
    /// </summary>
    /// <param name="channel">The channel to post in.</param>
    /// <param name="kind">What to show.</param>
    /// <param name="range">The window.</param>
    /// <param name="pin">Whether to pin the message.</param>
    /// <param name="entries">Rows for leaderboards.</param>
    /// <param name="intervalMinutes">How often to refresh.</param>
    [SlashCommand("add", "Creates a self refreshing leaderboard, chart or overview")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [RequireBotPermission(ChannelPermission.ManageMessages)]
    public async Task Add(ITextChannel channel, LiveBoardKind kind, StatsRange range = StatsRange.Weekly,
        bool pin = true, [MinValue(3)] [MaxValue(25)] int entries = 10,
        [MinValue(5)] [MaxValue(1440)] int intervalMinutes = 15)
    {
        await DeferAsync();
        var existing = await Service.GetAsync(ctx.Guild.Id);
        if (existing.Count >= 10)
        {
            await ReplyErrorAsync(Strings.LiveBoardLimit(ctx.Guild.Id));
            return;
        }

        var board = await Service.CreateAsync((SocketGuild)ctx.Guild, channel, kind, range, pin, entries,
            intervalMinutes);
        await ReplyConfirmAsync(Strings.LiveBoardCreated(ctx.Guild.Id, board.Id, kind.ToString(), channel.Mention,
            board.IntervalMinutes));
    }

    /// <summary>
    ///     Lists live boards.
    /// </summary>
    [SlashCommand("list", "Lists live boards")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task List()
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
        await RespondAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Deletes a live board and its message.
    /// </summary>
    /// <param name="id">The board ID.</param>
    [SlashCommand("remove", "Deletes a live board and its message")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task Remove(int id)
    {
        await (await Service.DeleteAsync((SocketGuild)ctx.Guild, id)
            ? ReplyConfirmAsync(Strings.LiveBoardRemoved(ctx.Guild.Id, id))
            : ReplyErrorAsync(Strings.LiveBoardNotFound(ctx.Guild.Id)));
    }

    /// <summary>
    ///     Refreshes every live board right now.
    /// </summary>
    [SlashCommand("refresh", "Refreshes every live board now")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [InteractionRatelimit(60)]
    public async Task Refresh()
    {
        await DeferAsync();
        var boards = await Service.GetAsync(ctx.Guild.Id);
        foreach (var board in boards)
            await Service.RefreshAsync((SocketGuild)ctx.Guild, board);
        await ReplyConfirmAsync(Strings.LiveBoardRefreshed(ctx.Guild.Id, boards.Count));
    }
}

/// <summary>
///     Slash commands for scheduled server reports.
/// </summary>
[Group("serverreport", "Scheduled growth and activity digests")]
public class SlashServerReports : MewdekoSlashModuleBase<ServerReportService>
{
    /// <summary>
    ///     Sets the channel that receives server reports and enables them.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="frequency">Daily, weekly or monthly.</param>
    [SlashCommand("enable", "Posts a growth and activity digest to a channel on a schedule")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task Enable(ITextChannel channel, ReportFrequency frequency = ReportFrequency.Weekly)
    {
        await Service.UpdateSettingsAsync(ctx.Guild.Id, s =>
        {
            s.ChannelId = channel.Id;
            s.Frequency = (int)frequency;
            s.Enabled = true;
        });
        await ReplyConfirmAsync(Strings.ReportChannelSet(ctx.Guild.Id, channel.Mention) + " " +
                                Strings.ReportFrequencySet(ctx.Guild.Id, frequency.ToString()));
    }

    /// <summary>
    ///     Disables server reports.
    /// </summary>
    [SlashCommand("disable", "Stops posting server reports")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task Disable()
    {
        await Service.UpdateSettingsAsync(ctx.Guild.Id, s => s.Enabled = false);
        await ReplyConfirmAsync(Strings.ReportDisabled(ctx.Guild.Id));
    }

    /// <summary>
    ///     Shows the report settings.
    /// </summary>
    [SlashCommand("show", "Shows the report settings")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task Show()
    {
        var settings = await Service.GetSettingsAsync(ctx.Guild.Id);
        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.ReportSettingsTitle(ctx.Guild.Id))
            .AddField("Enabled", settings.Enabled ? "Yes" : "No", true)
            .AddField("Channel", settings.ChannelId.HasValue ? $"<#{settings.ChannelId}>" : "-", true)
            .AddField("Frequency", ((ReportFrequency)settings.Frequency).ToString(), true)
            .AddField("Last Sent",
                settings.LastSentAt.HasValue
                    ? TimestampTag.FromDateTime(settings.LastSentAt.Value, TimestampTagStyles.Relative).ToString()
                    : "-", true);
        await RespondAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Posts a server report right now in the current channel.
    /// </summary>
    /// <param name="frequency">The period to cover, or the configured one.</param>
    [SlashCommand("now", "Posts a report for the period right here")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [InteractionRatelimit(120)]
    public async Task Now(ReportFrequency? frequency = null)
    {
        await DeferAsync();
        var settings = await Service.GetSettingsAsync(ctx.Guild.Id);
        var (embed, image) = await Service.BuildAsync((SocketGuild)ctx.Guild,
            frequency ?? (ReportFrequency)settings.Frequency);
        await using (image)
        {
            await FollowupWithFileAsync(image, "graph.png", embed: embed);
        }
    }
}