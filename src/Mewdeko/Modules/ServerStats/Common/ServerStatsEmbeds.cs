using System.IO;
using DataModel;
using Mewdeko.Modules.ServerStats.Services;
using Mewdeko.Services.Strings;
using SkiaSharp;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.ServerStats.Common;

/// <summary>
///     Builds the embeds and charts shared by the stats commands, live boards and reports.
/// </summary>
public static class ServerStatsEmbeds
{
    /// <summary>
    ///     Names a window for titles.
    /// </summary>
    /// <param name="lookbackDays">The window in days, or 0 for all time.</param>
    /// <returns>The display name.</returns>
    public static string WindowName(int lookbackDays)
    {
        return lookbackDays switch
        {
            0 => "All Time",
            1 => "Last 24 Hours",
            7 => "Last 7 Days",
            30 => "Last 30 Days",
            _ => $"Last {lookbackDays} Days"
        };
    }

    /// <summary>
    ///     Names a stat kind for titles.
    /// </summary>
    /// <param name="strings">Localized strings.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="kind">Messages or voice.</param>
    /// <returns>The display name.</returns>
    public static string KindName(GeneratedBotStrings strings, ulong guildId, StatKind kind)
    {
        return kind switch
        {
            StatKind.Messages => strings.StatsKindMessages(guildId),
            StatKind.Voice => strings.StatsKindVoice(guildId),
            _ => strings.StatsKindActivity(guildId)
        };
    }

    /// <summary>
    ///     Builds the top games and apps embed.
    /// </summary>
    /// <param name="strings">Localized strings.</param>
    /// <param name="guild">The guild.</param>
    /// <param name="lookbackDays">The window in days, or 0 for all time.</param>
    /// <param name="rows">The ranking.</param>
    /// <param name="user">The member, when the ranking is for one member.</param>
    /// <returns>The embed.</returns>
    public static EmbedBuilder Activities(GeneratedBotStrings strings, IGuild guild, int lookbackDays,
        IReadOnlyList<ActivitySummary> rows, IUser? user = null)
    {
        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(user == null
                ? strings.StatsActivitiesTitle(guild.Id, WindowName(lookbackDays))
                : strings.StatsUserActivitiesTitle(guild.Id, user.Username, WindowName(lookbackDays)));

        if (rows.Count == 0)
        {
            eb.WithDescription(strings.StatsNoData(guild.Id));
            return eb;
        }

        var total = rows.Sum(x => x.Seconds);
        eb.WithDescription(string.Join("\n", rows.Select((x, i) =>
            $"`{i + 1}.` {TypeIcon(x.Type)} **{x.Name}**: {ServerStatsService.FormatDuration(x.Seconds)}" +
            (user == null ? $" · {x.Players} {(x.Players == 1 ? "player" : "players")}" : "") +
            (total > 0 ? $" ({x.Seconds * 100.0 / total:F0}%)" : ""))));
        return eb;
    }

    /// <summary>
    ///     An emoji for an activity type.
    /// </summary>
    /// <param name="type">The activity type.</param>
    /// <returns>The emoji.</returns>
    public static string TypeIcon(ActivityType type)
    {
        return type switch
        {
            ActivityType.Streaming => "📺",
            ActivityType.Listening => "🎵",
            ActivityType.Watching => "👀",
            ActivityType.Competing => "🏁",
            _ => "🎮"
        };
    }

    /// <summary>
    ///     Formats a stat value for display: a count for messages, a duration for voice.
    /// </summary>
    /// <param name="kind">Messages or voice.</param>
    /// <param name="value">The raw value.</param>
    /// <returns>The formatted text.</returns>
    public static string FormatValue(StatKind kind, long value)
    {
        return kind == StatKind.Messages ? value.ToString("N0") : ServerStatsService.FormatDuration(value);
    }

    /// <summary>
    ///     Builds the server overview embed.
    /// </summary>
    public static EmbedBuilder Overview(GeneratedBotStrings strings, IGuild guild, ServerOverview o,
        ActivitySummary? topActivity = null)
    {
        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithAuthor(guild.Name, guild.IconUrl)
            .WithTitle(strings.StatsOverviewTitle(guild.Id, WindowName(o.LookbackDays)))
            .AddField(strings.StatsMessages(guild.Id), o.Messages.ToString("N0"), true)
            .AddField(strings.StatsVoiceTime(guild.Id), ServerStatsService.FormatDuration(o.VoiceSeconds), true)
            .AddField(strings.StatsContributors(guild.Id),
                strings.StatsContributorsValue(guild.Id, o.MessageContributors, o.VoiceContributors), true)
            .AddField(strings.StatsJoinsLeaves(guild.Id),
                strings.StatsJoinsLeavesValue(guild.Id, o.Joins, o.Leaves, (o.Joins - o.Leaves).ToString("+#;-#;0")),
                true);

        if (guild is SocketGuild sg)
        {
            eb.AddField(strings.StatsMembersNow(guild.Id),
                strings.StatsMembersNowValue(guild.Id, sg.MemberCount,
                    sg.Users.Count(u => u.Status != UserStatus.Offline)), true);
        }

        var tops = new List<string>();
        if (o.TopMessageUser != null)
            tops.Add(strings.StatsTopChatter(guild.Id, $"<@{o.TopMessageUser.Id}>",
                o.TopMessageUser.Value.ToString("N0")));
        if (o.TopVoiceUser != null)
            tops.Add(strings.StatsTopVoice(guild.Id, $"<@{o.TopVoiceUser.Id}>",
                ServerStatsService.FormatDuration(o.TopVoiceUser.Value)));
        if (o.TopMessageChannel != null)
            tops.Add(strings.StatsTopTextChannel(guild.Id, $"<#{o.TopMessageChannel.Id}>",
                o.TopMessageChannel.Value.ToString("N0")));
        if (o.TopVoiceChannel != null)
            tops.Add(strings.StatsTopVoiceChannel(guild.Id, $"<#{o.TopVoiceChannel.Id}>",
                ServerStatsService.FormatDuration(o.TopVoiceChannel.Value)));
        if (topActivity != null)
            tops.Add(strings.StatsTopGame(guild.Id, topActivity.Name,
                ServerStatsService.FormatDuration(topActivity.Seconds), topActivity.Players));
        if (tops.Count > 0)
            eb.AddField(strings.StatsLeaders(guild.Id), string.Join("\n", tops));

        return eb;
    }

    /// <summary>
    ///     Builds a member's activity embed.
    /// </summary>
    public static EmbedBuilder User(GeneratedBotStrings strings, IGuild guild, IGuildUser user, UserActivity a,
        IReadOnlyList<ActivitySummary>? topActivities = null)
    {
        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithAuthor(user.ToString(), user.RealAvatarUrl().ToString())
            .WithTitle(strings.StatsUserTitle(guild.Id, WindowName(a.LookbackDays)))
            .AddField(strings.StatsMessages(guild.Id),
                a.MessageRank.HasValue ? $"{a.Messages:N0} (#{a.MessageRank})" : a.Messages.ToString("N0"), true)
            .AddField(strings.StatsVoiceTime(guild.Id),
                a.VoiceRank.HasValue
                    ? $"{ServerStatsService.FormatDuration(a.VoiceSeconds)} (#{a.VoiceRank})"
                    : ServerStatsService.FormatDuration(a.VoiceSeconds), true)
            .AddField(strings.StatsLifetime(guild.Id),
                strings.StatsLifetimeValue(guild.Id, a.AllTimeMessages.ToString("N0"),
                    ServerStatsService.FormatDuration(a.AllTimeVoiceSeconds)), true);

        if (a.TopMessageChannels.Count > 0)
            eb.AddField(strings.StatsTopTextChannels(guild.Id),
                string.Join("\n", a.TopMessageChannels.Select(x => $"<#{x.Id}>: **{x.Value:N0}**")), true);
        if (a.TopVoiceChannels.Count > 0)
            eb.AddField(strings.StatsTopVoiceChannels(guild.Id),
                string.Join("\n",
                    a.TopVoiceChannels.Select(x => $"<#{x.Id}>: **{ServerStatsService.FormatDuration(x.Value)}**")),
                true);
        if (topActivities is { Count: > 0 })
            eb.AddField(strings.StatsTopGames(guild.Id),
                string.Join("\n", topActivities.Select(x =>
                    $"{TypeIcon(x.Type)} {x.Name}: **{ServerStatsService.FormatDuration(x.Seconds)}**")), true);

        if (user.JoinedAt.HasValue)
            eb.AddField(strings.StatsJoined(guild.Id),
                TimestampTag.FromDateTimeOffset(user.JoinedAt.Value, TimestampTagStyles.Relative).ToString(), true);
        eb.AddField(strings.StatsAccountAge(guild.Id),
            TimestampTag.FromDateTimeOffset(user.CreatedAt, TimestampTagStyles.Relative).ToString(), true);

        return eb;
    }

    /// <summary>
    ///     Builds a channel's activity embed.
    /// </summary>
    public static EmbedBuilder Channel(GeneratedBotStrings strings, IGuild guild, IGuildChannel channel,
        ChannelActivity a)
    {
        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(strings.StatsChannelTitle(guild.Id, channel.Name, WindowName(a.LookbackDays)))
            .AddField(strings.StatsMessages(guild.Id), a.Messages.ToString("N0"), true)
            .AddField(strings.StatsVoiceTime(guild.Id), ServerStatsService.FormatDuration(a.VoiceSeconds), true)
            .AddField(strings.StatsContributors(guild.Id), a.Contributors.ToString("N0"), true);

        if (a.TopMessageUsers.Count > 0)
            eb.AddField(strings.StatsTopChatters(guild.Id),
                string.Join("\n", a.TopMessageUsers.Select(x => $"<@{x.Id}>: **{x.Value:N0}**")), true);
        if (a.TopVoiceUsers.Count > 0)
            eb.AddField(strings.StatsTopVoiceUsers(guild.Id),
                string.Join("\n",
                    a.TopVoiceUsers.Select(x => $"<@{x.Id}>: **{ServerStatsService.FormatDuration(x.Value)}**")),
                true);

        return eb;
    }

    /// <summary>
    ///     Builds the settings embed.
    /// </summary>
    public static EmbedBuilder Settings(GeneratedBotStrings strings, ulong guildId, ServerStatsSetting s,
        IReadOnlyCollection<string>? activityFilters = null)
    {
        var ignoredStates = s.VoiceStates == 0
            ? "-"
            : string.Join(", ", Enum.GetValues<VoiceStateFlags>()
                .Where(f => f != VoiceStateFlags.Normal && (s.VoiceStates & (int)f) != 0)
                .Select(f => f.ToString()));

        var filterText = activityFilters == null || activityFilters.Count == 0
            ? "-"
            : string.Join(", ", activityFilters.Take(15));

        return new EmbedBuilder()
            .WithOkColor()
            .WithTitle(strings.StatsSettingsTitle(guildId))
            .AddField(strings.StatsSettingLookback(guildId), $"{s.DefaultLookbackDays}d", true)
            .AddField(strings.StatsSettingCooldown(guildId), $"{s.MessageCooldownSeconds}s", true)
            .AddField(strings.StatsSettingVoice(guildId), s.TrackVoice ? "Enabled" : "Disabled", true)
            .AddField(strings.StatsSettingSnapshots(guildId), s.TrackSnapshots ? "Enabled" : "Disabled", true)
            .AddField(strings.StatsSettingBots(guildId), s.CountBots ? "Counted" : "Ignored", true)
            .AddField(strings.StatsSettingVoiceStates(guildId), ignoredStates, true)
            .AddField(strings.StatsSettingActivities(guildId), s.TrackActivities ? "Enabled" : "Disabled", true)
            .AddField(strings.StatsSettingVerifyActivities(guildId), s.VerifyActivities ? "Enabled" : "Disabled",
                true)
            .AddField(strings.StatsSettingActivityFilter(guildId),
                $"{(ActivityFilterMode)s.ActivityFilterMode}: {filterText}", true);
    }

    /// <summary>
    ///     Renders a chart and its summary embed.
    /// </summary>
    /// <param name="service">The stats service.</param>
    /// <param name="guild">The guild.</param>
    /// <param name="kind">What to chart.</param>
    /// <param name="lookbackDays">The window in days, 1 to 90.</param>
    /// <returns>The PNG stream and an embed referencing it as graph.png.</returns>
    public static async Task<(MemoryStream Stream, Embed Embed)> ChartAsync(ServerStatsService service, IGuild guild,
        StatChartKind kind, int lookbackDays)
    {
        var window = WindowName(lookbackDays);
        MemoryStream stream;
        var eb = new EmbedBuilder().WithOkColor().WithImageUrl("attachment://graph.png").WithCurrentTimestamp();

        switch (kind)
        {
            case StatChartKind.Messages:
            {
                var series = await service.GetMessageSeriesAsync(guild.Id, lookbackDays);
                stream = StatChartRenderer.RenderLineChart($"Messages ({window})",
                    [new ChartSeries("Messages", series)]);
                var total = series.Sum(x => x.Value);
                eb.WithTitle($"Messages ({window})")
                    .AddField("Total", total.ToString("N0"), true)
                    .AddField("Average", (series.Count > 0 ? total / series.Count : 0).ToString("N1"), true)
                    .AddField("Peak", PeakLabel(series, "N0"), true);
                break;
            }
            case StatChartKind.Voice:
            {
                var series = await service.GetVoiceSeriesAsync(guild.Id, lookbackDays);
                stream = StatChartRenderer.RenderLineChart($"Voice Hours ({window})",
                    [new ChartSeries("Hours", series)], [StatChartRenderer.Palette[4]], "N1");
                var total = series.Sum(x => x.Value);
                eb.WithTitle($"Voice Hours ({window})")
                    .AddField("Total", $"{total:N1}h", true)
                    .AddField("Average", $"{(series.Count > 0 ? total / series.Count : 0):N1}h", true)
                    .AddField("Peak", PeakLabel(series, "N1"), true);
                break;
            }
            case StatChartKind.Members:
            {
                var snaps = await service.GetSnapshotSeriesAsync(guild.Id, lookbackDays);
                var series = snaps.Select(x => new SeriesPoint(x.Timestamp, x.Members)).ToList();
                stream = StatChartRenderer.RenderLineChart($"Members ({window})",
                    [new ChartSeries("Members", series)], [StatChartRenderer.Palette[2]]);
                eb.WithTitle($"Members ({window})");
                if (snaps.Count > 0)
                {
                    eb.AddField("Now", snaps[^1].Members.ToString("N0"), true)
                        .AddField("Change", (snaps[^1].Members - snaps[0].Members).ToString("+#,0;-#,0;0"), true)
                        .AddField("Peak", snaps.Max(x => x.Members).ToString("N0"), true);
                }

                break;
            }
            case StatChartKind.Status:
            {
                var snaps = await service.GetSnapshotSeriesAsync(guild.Id, lookbackDays);
                stream = StatChartRenderer.RenderLineChart($"Member Status ({window})",
                [
                    new ChartSeries("Online", snaps.Select(x => new SeriesPoint(x.Timestamp, x.Online)).ToList()),
                    new ChartSeries("Idle", snaps.Select(x => new SeriesPoint(x.Timestamp, x.Idle)).ToList()),
                    new ChartSeries("DND", snaps.Select(x => new SeriesPoint(x.Timestamp, x.Dnd)).ToList()),
                    new ChartSeries("Offline", snaps.Select(x => new SeriesPoint(x.Timestamp, x.Offline)).ToList())
                ], [
                    new SKColor(0x43, 0xB5, 0x81), new SKColor(0xFA, 0xA6, 0x1A), new SKColor(0xF0, 0x47, 0x47),
                    new SKColor(0x74, 0x7F, 0x8D)
                ]);
                eb.WithTitle($"Member Status ({window})");
                if (snaps.Count > 0)
                {
                    var last = snaps[^1];
                    eb.AddField("Online", last.Online.ToString("N0"), true)
                        .AddField("Idle", last.Idle.ToString("N0"), true)
                        .AddField("DND", last.Dnd.ToString("N0"), true)
                        .AddField("Peak Online", snaps.Max(x => x.Online).ToString("N0"), true);
                }

                break;
            }
            case StatChartKind.InVoice:
            {
                var snaps = await service.GetSnapshotSeriesAsync(guild.Id, lookbackDays);
                var series = snaps.Select(x => new SeriesPoint(x.Timestamp, x.InVoice)).ToList();
                stream = StatChartRenderer.RenderLineChart($"Members In Voice ({window})",
                    [new ChartSeries("In Voice", series)], [StatChartRenderer.Palette[4]]);
                eb.WithTitle($"Members In Voice ({window})")
                    .AddField("Peak", PeakLabel(series, "N0"), true);
                break;
            }
            case StatChartKind.Activities:
            {
                var rows = await service.GetTopActivitiesAsync(guild.Id, lookbackDays, 10);
                var title = $"Top Games & Apps ({window})";
                stream = StatChartRenderer.RenderBarChart(title,
                    rows.Select(x => (x.Name.Length > 18 ? x.Name[..17] + "…" : x.Name, x.Seconds / 3600.0)).ToList(),
                    StatChartRenderer.Palette[3]);
                eb.WithTitle(title);
                if (rows.Count > 0)
                {
                    eb.AddField("Most Played", $"{rows[0].Name} ({rows[0].Seconds / 3600.0:N1}h)", true)
                        .AddField("Players", rows.Sum(x => x.Players).ToString("N0"), true)
                        .AddField("Total", $"{rows.Sum(x => x.Seconds) / 3600.0:N1}h", true);
                }

                break;
            }
            case StatChartKind.Joins:
            case StatChartKind.Leaves:
            case StatChartKind.Growth:
            default:
            {
                var (joins, leaves) = await service.GetJoinLeaveSeriesAsync(guild.Id, lookbackDays);
                var series = new List<ChartSeries>();
                var colors = new List<SKColor>();
                if (kind != StatChartKind.Leaves)
                {
                    series.Add(new ChartSeries("Joins", joins));
                    colors.Add(StatChartRenderer.Palette[2]);
                }

                if (kind != StatChartKind.Joins)
                {
                    series.Add(new ChartSeries("Leaves", leaves));
                    colors.Add(StatChartRenderer.Palette[1]);
                }

                var title = kind switch
                {
                    StatChartKind.Joins => "Joins",
                    StatChartKind.Leaves => "Leaves",
                    _ => "Growth"
                };
                stream = StatChartRenderer.RenderLineChart($"{title} ({window})", series, colors);
                var totalJoins = joins.Sum(x => x.Value);
                var totalLeaves = leaves.Sum(x => x.Value);
                eb.WithTitle($"{title} ({window})")
                    .AddField("Joins", totalJoins.ToString("N0"), true)
                    .AddField("Leaves", totalLeaves.ToString("N0"), true)
                    .AddField("Net", (totalJoins - totalLeaves).ToString("+#,0;-#,0;0"), true);
                break;
            }
        }

        return (stream, eb.Build());
    }

    private static string PeakLabel(List<SeriesPoint> series, string format)
    {
        if (series.Count == 0)
            return "-";
        var peak = series.MaxBy(x => x.Value)!;
        return $"{peak.Value.ToString(format)} ({peak.Bucket:dd MMM})";
    }
}