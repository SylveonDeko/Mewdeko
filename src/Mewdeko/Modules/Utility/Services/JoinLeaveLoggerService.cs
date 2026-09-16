using System.IO;
using System.Threading;
using DataModel;
using LinqToDB.Async;
using LinqToDB.Data;
using Mewdeko.Modules.ServerStats.Common;
using Mewdeko.Modules.ServerStats.Services;
using SkiaSharp;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Records member joins and leaves and draws growth graphs from them. Events are buffered in memory and written
///     to the database in batches; every read goes to the database so history survives restarts and is never
///     duplicated.
/// </summary>
public class JoinLeaveLoggerService : INService, IDisposable
{
    /// <summary>
    ///     How many days the default graphs cover.
    /// </summary>
    public const int DefaultGraphDays = 10;

    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(30);

    private readonly IDataConnectionFactory dbFactory;
    private readonly EventHandler eventHandler;
    private readonly SemaphoreSlim flushLock = new(1, 1);
    private readonly Timer flushTimer;
    private readonly GuildSettingsService guildSettingsService;
    private readonly ILogger<JoinLeaveLoggerService> logger;
    private readonly Channel<JoinLeaveLog> pending = Channel.CreateUnbounded<JoinLeaveLog>();

    /// <summary>
    ///     Initializes a new instance of the <see cref="JoinLeaveLoggerService" /> class.
    /// </summary>
    /// <param name="eventHandler">Event handler for user join and leave events.</param>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="guildSettingsService">Service for getting and updating GuildConfigs in the db.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public JoinLeaveLoggerService(EventHandler eventHandler, IDataConnectionFactory dbFactory,
        GuildSettingsService guildSettingsService, ILogger<JoinLeaveLoggerService> logger)
    {
        this.guildSettingsService = guildSettingsService;
        this.logger = logger;
        this.dbFactory = dbFactory;
        this.eventHandler = eventHandler;

        flushTimer = new Timer(_ => _ = FlushAsync(), null, FlushInterval, FlushInterval);
        eventHandler.Subscribe("UserJoined", "JoinLeaveLoggerService", LogUserJoined);
        eventHandler.Subscribe("UserLeft", "JoinLeaveLoggerService", LogUserLeft);
    }

    /// <summary>
    ///     Disposes resources used by the service, flushing anything still buffered.
    /// </summary>
    public void Dispose()
    {
        eventHandler.Unsubscribe("UserJoined", "JoinLeaveLoggerService", LogUserJoined);
        eventHandler.Unsubscribe("UserLeft", "JoinLeaveLoggerService", LogUserLeft);
        flushTimer.Dispose();
        FlushAsync().GetAwaiter().GetResult();
        flushLock.Dispose();
    }

    private Task LogUserJoined(IGuildUser user)
    {
        pending.Writer.TryWrite(new JoinLeaveLog
        {
            GuildId = user.Guild.Id, UserId = user.Id, IsJoin = true, DateAdded = DateTime.UtcNow
        });
        return Task.CompletedTask;
    }

    private Task LogUserLeft(IGuild guild, IUser user)
    {
        pending.Writer.TryWrite(new JoinLeaveLog
        {
            GuildId = guild.Id, UserId = user.Id, IsJoin = false, DateAdded = DateTime.UtcNow
        });
        return Task.CompletedTask;
    }

    private async Task FlushAsync()
    {
        if (!await flushLock.WaitAsync(0))
            return;

        try
        {
            var batch = new List<JoinLeaveLog>();
            while (pending.Reader.TryRead(out var log))
                batch.Add(log);

            if (batch.Count == 0)
                return;

            await using var db = await dbFactory.CreateConnectionAsync();
            await db.BulkCopyAsync(batch);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error flushing join/leave logs");
        }
        finally
        {
            flushLock.Release();
        }
    }

    /// <summary>
    ///     Calculates the share of recorded events that were joins.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>Joins divided by all events, or 0 with no data.</returns>
    public async Task<double> CalculateAverageJoinsPerGuildAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var total = await db.JoinLeaveLogs.CountAsync(x => x.GuildId == guildId);
        if (total == 0)
            return 0;

        var joins = await db.JoinLeaveLogs.CountAsync(x => x.GuildId == guildId && x.IsJoin);
        return joins / (double)total;
    }

    /// <summary>
    ///     Counts joins and leaves since a point in time.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="since">The start of the window.</param>
    /// <returns>Join and leave counts.</returns>
    public async Task<(int Joins, int Leaves)> CountSinceAsync(ulong guildId, DateTime since)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var joins = await db.JoinLeaveLogs.CountAsync(x => x.GuildId == guildId && x.IsJoin && x.DateAdded >= since);
        var leaves = await db.JoinLeaveLogs.CountAsync(x =>
            x.GuildId == guildId && !x.IsJoin && x.DateAdded >= since);
        return (joins, leaves);
    }

    /// <summary>
    ///     Generates a graph of join events for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="days">How many days to cover.</param>
    /// <returns>A tuple containing the graph image stream and an embed for the graph.</returns>
    public async Task<(Stream ImageStream, Embed Embed)> GenerateJoinGraphAsync(ulong guildId,
        int days = DefaultGraphDays)
    {
        var joinData = await GetGroupedJoinLeaveDataAsync(guildId, true, days);
        var config = await guildSettingsService.GetGuildConfig(guildId);
        var color = StatChartRenderer.FromArgb(config.JoinGraphColor, StatChartRenderer.Palette[2]);
        return GenerateGraph(joinData, $"Joins Over the Last {days} Days", "Total Joins", "Joins", color);
    }

    /// <summary>
    ///     Generates a graph of leave events for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="days">How many days to cover.</param>
    /// <returns>A tuple containing the graph image stream and an embed for the graph.</returns>
    public async Task<(Stream ImageStream, Embed Embed)> GenerateLeaveGraphAsync(ulong guildId,
        int days = DefaultGraphDays)
    {
        var leaveData = await GetGroupedJoinLeaveDataAsync(guildId, false, days);
        var config = await guildSettingsService.GetGuildConfig(guildId);
        var color = StatChartRenderer.FromArgb(config.LeaveGraphColor, StatChartRenderer.Palette[1]);
        return GenerateGraph(leaveData, $"Leaves Over the Last {days} Days", "Total Leaves", "Leaves", color);
    }

    /// <summary>
    ///     Generates a graph with joins and leaves on the same axes.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="days">How many days to cover.</param>
    /// <returns>A tuple containing the graph image stream and an embed for the graph.</returns>
    public async Task<(Stream ImageStream, Embed Embed)> GenerateCombinedGraphAsync(ulong guildId,
        int days = DefaultGraphDays)
    {
        var joins = await GetGroupedJoinLeaveDataAsync(guildId, true, days);
        var leaves = await GetGroupedJoinLeaveDataAsync(guildId, false, days);
        var config = await guildSettingsService.GetGuildConfig(guildId);
        var title = $"Growth Over the Last {days} Days";

        var stream = StatChartRenderer.RenderLineChart(title,
            [
                new ChartSeries("Joins", joins.Select(x => new SeriesPoint(x.Date, x.Count)).ToList()),
                new ChartSeries("Leaves", leaves.Select(x => new SeriesPoint(x.Date, x.Count)).ToList())
            ],
            [
                StatChartRenderer.FromArgb(config.JoinGraphColor, StatChartRenderer.Palette[2]),
                StatChartRenderer.FromArgb(config.LeaveGraphColor, StatChartRenderer.Palette[1])
            ]);

        var totalJoins = joins.Sum(x => x.Count);
        var totalLeaves = leaves.Sum(x => x.Count);
        var embed = new EmbedBuilder()
            .WithTitle(title)
            .WithColor(new Color(0, 204, 255))
            .WithCurrentTimestamp()
            .WithImageUrl("attachment://graph.png")
            .AddField("Joins", totalJoins, true)
            .AddField("Leaves", totalLeaves, true)
            .AddField("Net", (totalJoins - totalLeaves).ToString("+#;-#;0"), true)
            .Build();

        return (stream, embed);
    }

    /// <summary>
    ///     Retrieves join or leave counts per day for a window, filling empty days with zero.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="isJoin">Determines whether to retrieve join or leave logs.</param>
    /// <param name="days">How many days to cover, ending today.</param>
    /// <returns>A list of date-wise grouped logs, oldest first.</returns>
    public async Task<List<DailyLog>> GetGroupedJoinLeaveDataAsync(ulong guildId, bool isJoin,
        int days = DefaultGraphDays)
    {
        await FlushAsync();

        var start = DateTime.UtcNow.Date.AddDays(-(days - 1));
        await using var db = await dbFactory.CreateConnectionAsync();

        var dates = await db.JoinLeaveLogs
            .Where(x => x.GuildId == guildId && x.IsJoin == isJoin && x.DateAdded >= start)
            .Select(x => x.DateAdded!.Value)
            .ToListAsync();

        var byDay = dates.GroupBy(x => x.Date).ToDictionary(g => g.Key, g => g.Count());

        return Enumerable.Range(0, days)
            .Select(i => start.AddDays(i))
            .Select(day => new DailyLog
            {
                Date = day, Count = byDay.GetValueOrDefault(day)
            })
            .ToList();
    }

    private static (Stream ImageStream, Embed Embed) GenerateGraph(List<DailyLog> dailyLogs, string title,
        string totalLabel, string seriesLabel, SKColor color)
    {
        var stream = StatChartRenderer.RenderLineChart(title,
        [
            new ChartSeries(seriesLabel, dailyLogs.Select(x => new SeriesPoint(x.Date, x.Count)).ToList())
        ], [color]);

        var embedBuilder = new EmbedBuilder()
            .WithTitle(title)
            .WithColor(new Color(0, 204, 255))
            .WithCurrentTimestamp()
            .WithImageUrl("attachment://graph.png");

        var total = dailyLogs.Sum(log => log.Count);
        var peakDay = dailyLogs.OrderByDescending(log => log.Count).FirstOrDefault();
        var average = dailyLogs.Count > 0 ? dailyLogs.Average(log => log.Count) : 0;

        embedBuilder.AddField(totalLabel, total, true);
        embedBuilder.AddField("Average per Day", $"{average:N2}", true);
        if (peakDay != null)
        {
            embedBuilder.AddField("Peak Day",
                $"{peakDay.Date:dd MMM} ({peakDay.Count} {totalLabel.ToLower().Replace("total ", "")})", true);
        }

        return (stream, embedBuilder.Build());
    }

    /// <summary>
    ///     Sets the color for the join graph.
    /// </summary>
    /// <param name="color">The color for the join graph.</param>
    /// <param name="guildId">The ID of the guild.</param>
    public async Task SetJoinColorAsync(uint color, ulong guildId)
    {
        await UpdateGraphColorAsync(guildId, color, true);
    }

    /// <summary>
    ///     Sets the color for the leave graph.
    /// </summary>
    /// <param name="color">The color for the leave graph.</param>
    /// <param name="guildId">The ID of the guild.</param>
    public async Task SetLeaveColorAsync(uint color, ulong guildId)
    {
        await UpdateGraphColorAsync(guildId, color, false);
    }

    private async Task UpdateGraphColorAsync(ulong guildId, uint color, bool isJoin)
    {
        try
        {
            var config = await guildSettingsService.GetGuildConfig(guildId);
            if (isJoin)
                config.JoinGraphColor = color;
            else
                config.LeaveGraphColor = color;

            await guildSettingsService.UpdateGuildConfig(guildId, config);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating graph color for Guild ID: {GuildId}", guildId);
        }
    }

    /// <summary>
    ///     Represents a daily log with date and count.
    /// </summary>
    public record DailyLog
    {
        /// <summary>
        ///     Date
        /// </summary>
        public DateTime Date { get; init; }

        /// <summary>
        ///     Count
        /// </summary>
        public int Count { get; init; }
    }
}