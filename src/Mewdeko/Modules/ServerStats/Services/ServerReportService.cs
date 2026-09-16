using System.IO;
using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.ServerStats.Common;
using Mewdeko.Modules.Utility.Common;
using Mewdeko.Modules.Utility.Services;
using Mewdeko.Services.Strings;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.ServerStats.Services;

/// <summary>
///     Posts a scheduled digest of growth and activity: joins, leaves, retention, top inviters, chatters and voice
///     members, busiest channels and a growth chart, once a day, week or month.
/// </summary>
public class ServerReportService : INService, IReadyExecutor, IDisposable
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(15);

    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly InviteCountService invites;
    private readonly ILogger<ServerReportService> logger;
    private readonly ServerStatsService stats;
    private readonly GeneratedBotStrings strings;
    private readonly SemaphoreSlim tickLock = new(1, 1);
    private Timer? tickTimer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ServerReportService" /> class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="stats">The stats query service.</param>
    /// <param name="invites">The invite tracking service.</param>
    /// <param name="strings">Localized strings.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public ServerReportService(DiscordShardedClient client, IDataConnectionFactory dbFactory,
        ServerStatsService stats, InviteCountService invites, GeneratedBotStrings strings,
        ILogger<ServerReportService> logger)
    {
        this.client = client;
        this.dbFactory = dbFactory;
        this.stats = stats;
        this.invites = invites;
        this.strings = strings;
        this.logger = logger;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        tickTimer?.Dispose();
        tickLock.Dispose();
    }

    /// <inheritdoc />
    public Task OnReadyAsync()
    {
        tickTimer = new Timer(_ => _ = TickAsync(), null, TimeSpan.FromMinutes(3), TickInterval);
        return Task.CompletedTask;
    }

    #region Settings

    /// <summary>
    ///     Gets a guild's report settings, creating defaults on first read.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The settings.</returns>
    public async Task<ServerReportSetting> GetSettingsAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var row = await db.ServerReportSettings.FirstOrDefaultAsync(x => x.GuildId == guildId);
        if (row != null)
            return row;

        row = new ServerReportSetting
        {
            GuildId = guildId, DateAdded = DateTime.UtcNow
        };
        row.Id = await db.InsertWithInt32IdentityAsync(row);
        return row;
    }

    /// <summary>
    ///     Updates a guild's report settings.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="mutate">Applies the change.</param>
    /// <returns>The updated settings.</returns>
    public async Task<ServerReportSetting> UpdateSettingsAsync(ulong guildId, Action<ServerReportSetting> mutate)
    {
        var row = await GetSettingsAsync(guildId);
        mutate(row);
        if (row.ChannelId == null)
            row.Enabled = false;

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(row);
        return row;
    }

    #endregion

    #region Scheduling

    private async Task TickAsync()
    {
        if (!await tickLock.WaitAsync(0))
            return;

        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var enabled = await db.ServerReportSettings.Where(x => x.Enabled && x.ChannelId != null).ToListAsync();
            var now = DateTime.UtcNow;

            foreach (var settings in enabled)
            {
                if (!IsDue(settings, now))
                    continue;

                var guild = client.GetGuild(settings.GuildId);
                if (guild == null)
                    continue;

                try
                {
                    await SendAsync(guild, settings);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Server report failed for {GuildId}", settings.GuildId);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Server report tick failed");
        }
        finally
        {
            tickLock.Release();
        }
    }

    private static bool IsDue(ServerReportSetting settings, DateTime now)
    {
        if (settings.LastSentAt == null)
            return true;

        var last = settings.LastSentAt.Value;
        return (ReportFrequency)settings.Frequency switch
        {
            ReportFrequency.Daily => now.Date > last.Date,
            ReportFrequency.Weekly => StartOfWeek(now) > StartOfWeek(last),
            _ => new DateTime(now.Year, now.Month, 1) > new DateTime(last.Year, last.Month, 1)
        };
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        var diff = ((int)date.DayOfWeek + 6) % 7;
        return date.Date.AddDays(-diff);
    }

    private static int PeriodDays(ReportFrequency frequency)
    {
        return frequency switch
        {
            ReportFrequency.Daily => 1,
            ReportFrequency.Weekly => 7,
            _ => 30
        };
    }

    /// <summary>
    ///     Builds and posts the report now, and records the send time.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="settings">The report settings.</param>
    /// <returns>True when posted.</returns>
    public async Task<bool> SendAsync(SocketGuild guild, ServerReportSetting settings)
    {
        if (settings.ChannelId is not { } channelId || guild.GetTextChannel(channelId) is not { } channel)
        {
            await UpdateSettingsAsync(guild.Id, s => s.Enabled = false);
            return false;
        }

        var (embed, image) = await BuildAsync(guild, (ReportFrequency)settings.Frequency);
        await using (image)
        {
            await channel.SendFileAsync(image, "graph.png", embed: embed);
        }

        await UpdateSettingsAsync(guild.Id, s => s.LastSentAt = DateTime.UtcNow);
        return true;
    }

    /// <summary>
    ///     Builds the report for a period.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="frequency">The period length.</param>
    /// <returns>The embed and growth chart to attach as graph.png.</returns>
    public async Task<(Embed Embed, MemoryStream Image)> BuildAsync(SocketGuild guild, ReportFrequency frequency)
    {
        var days = PeriodDays(frequency);
        var range = frequency switch
        {
            ReportFrequency.Daily => StatsRange.Daily,
            ReportFrequency.Weekly => StatsRange.Weekly,
            _ => StatsRange.Monthly
        };

        var overview = await stats.GetOverviewAsync(guild, days);
        var analytics = await invites.GetAnalyticsAsync(guild, range);
        var topChatters = await stats.GetTopUsersAsync(guild.Id, StatKind.Messages, days, 5);
        var topVoice = await stats.GetTopUsersAsync(guild.Id, StatKind.Voice, days, 5);
        var topChannels = await stats.GetTopChannelsAsync(guild.Id, StatKind.Messages, days, 5);
        var topGames = await stats.GetTopActivitiesAsync(guild.Id, days, 5);
        var snapshots = await stats.GetSnapshotSeriesAsync(guild.Id, days);

        var title = strings.ReportTitle(guild.Id, frequency.ToString(), ServerStatsEmbeds.WindowName(days));
        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithAuthor(guild.Name, guild.IconUrl)
            .WithTitle(title)
            .WithImageUrl("attachment://graph.png")
            .WithCurrentTimestamp()
            .AddField(strings.ReportGrowth(guild.Id),
                strings.ReportGrowthValue(guild.Id, overview.Joins, overview.Leaves,
                    (overview.Joins - overview.Leaves).ToString("+#;-#;0"),
                    analytics.Retention.HasValue ? $"{analytics.Retention.Value:P0}" : "-"), true)
            .AddField(strings.ReportActivity(guild.Id),
                strings.ReportActivityValue(guild.Id, overview.Messages.ToString("N0"),
                    ServerStatsService.FormatDuration(overview.VoiceSeconds), overview.MessageContributors), true);

        if (snapshots.Count > 1)
        {
            eb.AddField(strings.ReportMembers(guild.Id),
                strings.ReportMembersValue(guild.Id, guild.MemberCount.ToString("N0"),
                    (snapshots[^1].Members - snapshots[0].Members).ToString("+#,0;-#,0;0")), true);
        }
        else
        {
            eb.AddField(strings.ReportMembers(guild.Id), guild.MemberCount.ToString("N0"), true);
        }

        if (analytics.TopInviters.Count > 0)
            eb.AddField(strings.ReportTopInviters(guild.Id), string.Join("\n",
                analytics.TopInviters.Select((x, i) => $"{i + 1}. <@{x.UserId}>: **{x.Total}**")), true);
        if (topChatters.Count > 0)
            eb.AddField(strings.ReportTopChatters(guild.Id), string.Join("\n",
                topChatters.Select((x, i) => $"{i + 1}. <@{x.Id}>: **{x.Value:N0}**")), true);
        if (topVoice.Count > 0)
            eb.AddField(strings.ReportTopVoice(guild.Id), string.Join("\n",
                    topVoice.Select((x, i) => $"{i + 1}. <@{x.Id}>: **{ServerStatsService.FormatDuration(x.Value)}**")),
                true);
        if (topChannels.Count > 0)
            eb.AddField(strings.ReportTopChannels(guild.Id), string.Join("\n",
                topChannels.Select((x, i) => $"{i + 1}. <#{x.Id}>: **{x.Value:N0}**")), true);
        if (analytics.TopCodes.Count > 0)
            eb.AddField(strings.ReportTopCodes(guild.Id), string.Join("\n",
                analytics.TopCodes.Take(5).Select(x =>
                    $"`{x.Code}`{(x.Label != null ? $" ({x.Label})" : "")}: **{x.Joins}**")), true);
        if (topGames.Count > 0)
            eb.AddField(strings.ReportTopGames(guild.Id), string.Join("\n",
                    topGames.Select((x, i) =>
                        $"{i + 1}. {ServerStatsEmbeds.TypeIcon(x.Type)} {x.Name}: **{ServerStatsService.FormatDuration(x.Seconds)}**")),
                true);

        var (joins, leaves) = await stats.GetJoinLeaveSeriesAsync(guild.Id, Math.Max(days, 7));
        var image = StatChartRenderer.RenderLineChart(strings.ReportChartTitle(guild.Id),
        [
            new ChartSeries("Joins", joins),
            new ChartSeries("Leaves", leaves)
        ], [StatChartRenderer.Palette[2], StatChartRenderer.Palette[1]]);

        return (eb.Build(), image);
    }

    #endregion
}