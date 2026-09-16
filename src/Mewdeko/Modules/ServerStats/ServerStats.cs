using System.IO;
using System.Text;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.ServerStats.Common;
using Mewdeko.Modules.ServerStats.Services;

namespace Mewdeko.Modules.ServerStats;

/// <summary>
///     Activity statistics: messages, voice time, member growth and status over configurable windows, with charts,
///     rankings, exports, tracking filters and a per user privacy opt out.
/// </summary>
public class ServerStats(InteractiveService interactivity, ServerStatsSettingsService statsSettings)
    : MewdekoModuleBase<ServerStatsService>
{
    /// <summary>
    ///     Shows the server's activity summary for a window.
    /// </summary>
    /// <param name="days">The window in days, 0 for all time, or omitted for the server default.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task ServerActivity(int? days = null)
    {
        var lookback = Service.ResolveLookback(ctx.Guild.Id, days);
        var overview = await Service.GetOverviewAsync(ctx.Guild, lookback);
        var topGame = statsSettings.GetCachedSettings(ctx.Guild.Id).TrackActivities
            ? (await Service.GetTopActivitiesAsync(ctx.Guild.Id, lookback, 1)).FirstOrDefault()
            : null;
        await ctx.Channel.SendMessageAsync(embed: ServerStatsEmbeds.Overview(Strings, ctx.Guild, overview, topGame)
            .Build());
    }

    /// <summary>
    ///     Shows a member's activity summary for a window.
    /// </summary>
    /// <param name="user">The member, or the caller when omitted.</param>
    /// <param name="days">The window in days, 0 for all time, or omitted for the server default.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task UserActivity(IGuildUser? user = null, int? days = null)
    {
        user ??= (IGuildUser)ctx.User;
        var lookback = Service.ResolveLookback(ctx.Guild.Id, days);
        var activity = await Service.GetUserActivityAsync(ctx.Guild, user.Id, lookback);
        var games = statsSettings.GetCachedSettings(ctx.Guild.Id).TrackActivities
            ? await Service.GetTopActivitiesAsync(ctx.Guild.Id, lookback, 5, user.Id)
            : null;
        await ctx.Channel.SendMessageAsync(embed: ServerStatsEmbeds.User(Strings, ctx.Guild, user, activity, games)
            .Build());
    }

    /// <summary>
    ///     Ranks the games and apps members spend the most time in.
    /// </summary>
    /// <param name="days">The window in days, 0 for all time, or omitted for the server default.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task TopGames(int? days = null)
    {
        if (!statsSettings.GetCachedSettings(ctx.Guild.Id).TrackActivities)
        {
            await ReplyErrorAsync(Strings.StatsActivitiesOff(ctx.Guild.Id));
            return;
        }

        var lookback = Service.ResolveLookback(ctx.Guild.Id, days);
        var rows = await Service.GetTopActivitiesAsync(ctx.Guild.Id, lookback, 15);
        await ctx.Channel.SendMessageAsync(embed: ServerStatsEmbeds.Activities(Strings, ctx.Guild, lookback, rows)
            .Build());
    }

    /// <summary>
    ///     Shows who plays a game: members in it right now and the members with the most time in it.
    /// </summary>
    /// <param name="game">The game or app name.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task WhoPlays([Remainder] string game)
    {
        if (!statsSettings.GetCachedSettings(ctx.Guild.Id).TrackActivities)
        {
            await ReplyErrorAsync(Strings.StatsActivitiesOff(ctx.Guild.Id));
            return;
        }

        var lookback = Service.ResolveLookback(ctx.Guild.Id, null);
        var totals = await Service.GetPerUserActivitySecondsAsync(ctx.Guild.Id, game, lookback);
        var now = Service.CountActiveNow(ctx.Guild.Id, game);
        if (totals.Count == 0 && now == 0)
        {
            await ReplyErrorAsync(Strings.StatsNoData(ctx.Guild.Id));
            return;
        }

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.StatsWhoPlaysTitle(ctx.Guild.Id, game))
            .AddField(Strings.StatsPlayingNow(ctx.Guild.Id), now.ToString("N0"), true)
            .AddField(Strings.StatsPlayersWindow(ctx.Guild.Id, ServerStatsEmbeds.WindowName(lookback)),
                totals.Count.ToString("N0"), true)
            .AddField(Strings.StatsTotalTime(ctx.Guild.Id),
                ServerStatsService.FormatDuration(totals.Values.Sum()), true);

        if (totals.Count > 0)
            eb.AddField(Strings.StatsTopPlayers(ctx.Guild.Id), string.Join("\n",
                totals.OrderByDescending(x => x.Value).Take(10)
                    .Select((x, i) => $"`{i + 1}.` <@{x.Key}>: **{ServerStatsService.FormatDuration(x.Value)}**")));

        await ctx.Channel.SendMessageAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Toggles game and app activity tracking.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task StatsTrackActivities()
    {
        var settings = await statsSettings.UpdateSettingsAsync(ctx.Guild.Id,
            s => s.TrackActivities = !s.TrackActivities);
        await ReplyConfirmAsync(settings.TrackActivities
            ? Strings.StatsActivitiesOn(ctx.Guild.Id)
            : Strings.StatsActivitiesOffSet(ctx.Guild.Id));
    }

    /// <summary>
    ///     Toggles whether only activities backed by a Discord application, Spotify or a stream are counted.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task StatsVerifyActivities()
    {
        var settings = await statsSettings.UpdateSettingsAsync(ctx.Guild.Id,
            s => s.VerifyActivities = !s.VerifyActivities);
        await ReplyConfirmAsync(settings.VerifyActivities
            ? Strings.StatsVerifyOn(ctx.Guild.Id)
            : Strings.StatsVerifyOff(ctx.Guild.Id));
    }

    /// <summary>
    ///     Sets whether the activity filter list is a whitelist or a blacklist.
    /// </summary>
    /// <param name="mode">Whitelist or Blacklist.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task StatsActivityFilterMode(ActivityFilterMode mode)
    {
        await statsSettings.UpdateSettingsAsync(ctx.Guild.Id, s => s.ActivityFilterMode = (int)mode);
        await ReplyConfirmAsync(Strings.StatsActivityFilterModeSet(ctx.Guild.Id, mode.ToString()));
    }

    /// <summary>
    ///     Toggles a game or app name on the activity filter list.
    /// </summary>
    /// <param name="name">The activity name.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task StatsActivityFilter([Remainder] string name)
    {
        var added = await statsSettings.ToggleActivityFilterAsync(ctx.Guild.Id, name);
        await ReplyConfirmAsync(added
            ? Strings.StatsActivityFilterAdded(ctx.Guild.Id, name)
            : Strings.StatsActivityFilterRemoved(ctx.Guild.Id, name));
    }

    /// <summary>
    ///     Lists the activity filter.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task StatsActivityFilters()
    {
        var mode = (ActivityFilterMode)statsSettings.GetCachedSettings(ctx.Guild.Id).ActivityFilterMode;
        var names = statsSettings.GetActivityFilters(ctx.Guild.Id);
        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.StatsActivityFiltersTitle(ctx.Guild.Id, mode.ToString()))
            .WithDescription(names.Count == 0 ? "-" : string.Join("\n", names.Take(50)));
        await ctx.Channel.SendMessageAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Shows a channel's activity summary for a window.
    /// </summary>
    /// <param name="channel">The channel, or the current one when omitted.</param>
    /// <param name="days">The window in days, 0 for all time, or omitted for the server default.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task ChannelActivity(IGuildChannel? channel = null, int? days = null)
    {
        channel ??= (IGuildChannel)ctx.Channel;
        var lookback = Service.ResolveLookback(ctx.Guild.Id, days);
        var activity = await Service.GetChannelActivityAsync(ctx.Guild, channel.Id, lookback);
        await ctx.Channel.SendMessageAsync(embed: ServerStatsEmbeds.Channel(Strings, ctx.Guild, channel, activity)
            .Build());
    }

    /// <summary>
    ///     Ranks members by messages or voice time.
    /// </summary>
    /// <param name="kind">Messages or voice.</param>
    /// <param name="days">The window in days, 0 for all time, or omitted for the server default.</param>
    /// <param name="role">Only include members with this role.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task ActivityTop(StatKind kind = StatKind.Messages, int? days = null, IRole? role = null)
    {
        var lookback = Service.ResolveLookback(ctx.Guild.Id, days);
        var entries = await Service.GetTopUsersAsync(ctx.Guild.Id, kind, lookback, 500);

        if (role != null)
        {
            var filtered = new List<TopEntry>();
            foreach (var entry in entries)
            {
                var member = await ctx.Guild.GetUserAsync(entry.Id);
                if (member != null && member.RoleIds.Contains(role.Id))
                    filtered.Add(entry);
            }

            entries = filtered;
        }

        if (entries.Count == 0)
        {
            await ReplyErrorAsync(Strings.StatsNoData(ctx.Guild.Id));
            return;
        }

        var title = Strings.StatsTopTitle(ctx.Guild.Id, ServerStatsEmbeds.KindName(Strings, ctx.Guild.Id, kind),
            ServerStatsEmbeds.WindowName(lookback));
        await SendRankingAsync(entries, title, kind, id => $"<@{id}>");
    }

    /// <summary>
    ///     Ranks channels by messages or voice time.
    /// </summary>
    /// <param name="kind">Messages or voice.</param>
    /// <param name="days">The window in days, 0 for all time, or omitted for the server default.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task ActivityTopChannels(StatKind kind = StatKind.Messages, int? days = null)
    {
        var lookback = Service.ResolveLookback(ctx.Guild.Id, days);
        var entries = await Service.GetTopChannelsAsync(ctx.Guild.Id, kind, lookback, 100);
        if (entries.Count == 0)
        {
            await ReplyErrorAsync(Strings.StatsNoData(ctx.Guild.Id));
            return;
        }

        var title = Strings.StatsTopChannelsTitle(ctx.Guild.Id,
            ServerStatsEmbeds.KindName(Strings, ctx.Guild.Id, kind), ServerStatsEmbeds.WindowName(lookback));
        await SendRankingAsync(entries, title, kind, id => $"<#{id}>");
    }

    private async Task SendRankingAsync(List<TopEntry> entries, string title, StatKind kind,
        Func<ulong, string> mention)
    {
        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex((entries.Count - 1) / 10)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, ctx.Channel, TimeSpan.FromMinutes(60));
        return;

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask;
            return new PageBuilder().WithOkColor().WithTitle(title)
                .WithDescription(string.Join("\n", entries.Skip(page * 10).Take(10).Select((x, i) =>
                    $"`{page * 10 + i + 1}.` {mention(x.Id)}: **{ServerStatsEmbeds.FormatValue(kind, x.Value)}**")));
        }
    }

    /// <summary>
    ///     Draws a chart of messages, voice, members, status, joins, leaves or growth over a window.
    /// </summary>
    /// <param name="kind">What to chart.</param>
    /// <param name="days">The window in days, 1 to 90, or omitted for the server default.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [Ratelimit(10)]
    public async Task ActivityChart(StatChartKind kind = StatChartKind.Messages, int? days = null)
    {
        var lookback = Math.Max(1, Service.ResolveLookback(ctx.Guild.Id, days));
        var (stream, embed) = await ServerStatsEmbeds.ChartAsync(Service, ctx.Guild, kind, lookback);
        await using (stream)
        {
            await ctx.Channel.SendFileAsync(stream, "graph.png", embed: embed);
        }
    }

    /// <summary>
    ///     Exports a member ranking as a CSV file.
    /// </summary>
    /// <param name="kind">Messages or voice.</param>
    /// <param name="days">The window in days, 0 for all time, or omitted for the server default.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    [Ratelimit(300)]
    public async Task ActivityExport(StatKind kind = StatKind.Messages, int? days = null)
    {
        var lookback = Service.ResolveLookback(ctx.Guild.Id, days);
        var csv = await Service.ExportCsvAsync(ctx.Guild, kind, lookback);
        var fileName = $"{kind}-{lookback}d.csv".ToLowerInvariant();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        await ctx.Channel.SendFileAsync(stream, fileName, Strings.StatsExportDone(ctx.Guild.Id));
    }

    /// <summary>
    ///     Shows the stats tracking settings.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task StatsSettings()
    {
        var settings = await statsSettings.GetSettingsAsync(ctx.Guild.Id);
        await ctx.Channel.SendMessageAsync(embed: ServerStatsEmbeds.Settings(Strings, ctx.Guild.Id, settings,
            statsSettings.GetActivityFilters(ctx.Guild.Id)).Build());
    }

    /// <summary>
    ///     Sets the default window for stats commands.
    /// </summary>
    /// <param name="days">The window in days, 1 to 90.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task StatsLookback(int days)
    {
        if (days is < 1 or > ServerStatsService.MaxLookbackDays)
        {
            await ReplyErrorAsync(Strings.StatsLookbackRange(ctx.Guild.Id, ServerStatsService.MaxLookbackDays));
            return;
        }

        await statsSettings.UpdateSettingsAsync(ctx.Guild.Id, s => s.DefaultLookbackDays = days);
        await ReplyConfirmAsync(Strings.StatsLookbackSet(ctx.Guild.Id, days));
    }

    /// <summary>
    ///     Sets how many seconds must pass between two counted messages from the same member.
    /// </summary>
    /// <param name="seconds">The cooldown, 0 to 300.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task StatsCooldown(int seconds)
    {
        if (seconds is < 0 or > 300)
        {
            await ReplyErrorAsync(Strings.StatsCooldownRange(ctx.Guild.Id));
            return;
        }

        await statsSettings.UpdateSettingsAsync(ctx.Guild.Id, s => s.MessageCooldownSeconds = seconds);
        await ReplyConfirmAsync(Strings.StatsCooldownSet(ctx.Guild.Id, seconds));
    }

    /// <summary>
    ///     Toggles voice time tracking.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task StatsTrackVoice()
    {
        var settings = await statsSettings.UpdateSettingsAsync(ctx.Guild.Id, s => s.TrackVoice = !s.TrackVoice);
        await ReplyConfirmAsync(settings.TrackVoice
            ? Strings.StatsVoiceOn(ctx.Guild.Id)
            : Strings.StatsVoiceOff(ctx.Guild.Id));
    }

    /// <summary>
    ///     Toggles hourly member and status snapshots.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task StatsTrackSnapshots()
    {
        var settings = await statsSettings.UpdateSettingsAsync(ctx.Guild.Id,
            s => s.TrackSnapshots = !s.TrackSnapshots);
        await ReplyConfirmAsync(settings.TrackSnapshots
            ? Strings.StatsSnapshotsOn(ctx.Guild.Id)
            : Strings.StatsSnapshotsOff(ctx.Guild.Id));
    }

    /// <summary>
    ///     Toggles whether bots are counted.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task StatsCountBots()
    {
        var settings = await statsSettings.UpdateSettingsAsync(ctx.Guild.Id, s => s.CountBots = !s.CountBots);
        await ReplyConfirmAsync(settings.CountBots
            ? Strings.StatsBotsOn(ctx.Guild.Id)
            : Strings.StatsBotsOff(ctx.Guild.Id));
    }

    /// <summary>
    ///     Toggles whether a voice state (muted, deafened, AFK, alone and so on) is left out of voice time.
    /// </summary>
    /// <param name="state">The state to toggle.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task StatsIgnoreVoiceState(VoiceStateFlags state)
    {
        if (state == VoiceStateFlags.Normal)
        {
            await ReplyErrorAsync(Strings.StatsVoiceStateInvalid(ctx.Guild.Id));
            return;
        }

        var settings = await statsSettings.UpdateSettingsAsync(ctx.Guild.Id, s => s.VoiceStates ^= (int)state);
        var ignored = (settings.VoiceStates & (int)state) != 0;
        await ReplyConfirmAsync(ignored
            ? Strings.StatsVoiceStateIgnored(ctx.Guild.Id, state.ToString())
            : Strings.StatsVoiceStateCounted(ctx.Guild.Id, state.ToString()));
    }

    /// <summary>
    ///     Toggles whether a channel is left out of stats.
    /// </summary>
    /// <param name="channel">The channel.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task StatsIgnore(IGuildChannel channel)
    {
        await ToggleExclusionAsync(channel.Id, StatsExclusionKind.Channel, $"<#{channel.Id}>");
    }

    /// <summary>
    ///     Toggles whether holders of a role are left out of stats.
    /// </summary>
    /// <param name="role">The role.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task StatsIgnore(IRole role)
    {
        await ToggleExclusionAsync(role.Id, StatsExclusionKind.Role, role.Mention);
    }

    /// <summary>
    ///     Toggles whether a member is left out of stats in this server.
    /// </summary>
    /// <param name="user">The member.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task StatsIgnore(IGuildUser user)
    {
        await ToggleExclusionAsync(user.Id, StatsExclusionKind.User, user.Mention);
    }

    private async Task ToggleExclusionAsync(ulong targetId, StatsExclusionKind kind, string mention)
    {
        if (await statsSettings.AddExclusionAsync(ctx.Guild.Id, targetId, kind))
        {
            await ReplyConfirmAsync(Strings.StatsIgnored(ctx.Guild.Id, mention));
            return;
        }

        await statsSettings.RemoveExclusionAsync(ctx.Guild.Id, targetId, kind);
        await ReplyConfirmAsync(Strings.StatsUnignored(ctx.Guild.Id, mention));
    }

    /// <summary>
    ///     Lists ignored channels, roles and members.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task StatsIgnored()
    {
        var channels = statsSettings.GetExclusions(ctx.Guild.Id, StatsExclusionKind.Channel);
        var roles = statsSettings.GetExclusions(ctx.Guild.Id, StatsExclusionKind.Role);
        var users = statsSettings.GetExclusions(ctx.Guild.Id, StatsExclusionKind.User);

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.StatsIgnoredTitle(ctx.Guild.Id))
            .AddField(Strings.StatsIgnoredChannels(ctx.Guild.Id),
                channels.Count == 0 ? "-" : string.Join(", ", channels.Select(x => $"<#{x}>")))
            .AddField(Strings.StatsIgnoredRoles(ctx.Guild.Id),
                roles.Count == 0 ? "-" : string.Join(", ", roles.Select(x => $"<@&{x}>")))
            .AddField(Strings.StatsIgnoredUsers(ctx.Guild.Id),
                users.Count == 0 ? "-" : string.Join(", ", users.Select(x => $"<@{x}>")));

        await ctx.Channel.SendMessageAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Opts you out of activity stats everywhere, scrubbing what has been recorded, or opts you back in.
    /// </summary>
    [Cmd]
    [Aliases]
    public async Task StatsPrivacy()
    {
        var guildId = ctx.Guild?.Id;
        if (statsSettings.IsOptedOut(ctx.User.Id))
        {
            await statsSettings.OptInAsync(ctx.User.Id);
            await ReplyConfirmAsync(Strings.StatsOptedIn(guildId));
            return;
        }

        if (!await PromptUserConfirmAsync(Strings.StatsOptOutConfirm(guildId), ctx.User.Id))
            return;

        await statsSettings.OptOutAsync(ctx.User.Id);
        await ReplyConfirmAsync(Strings.StatsOptedOut(guildId));
    }
}