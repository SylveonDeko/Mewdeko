using System.IO;
using System.Text.Json;
using System.Threading;
using DataModel;
using LinqToDB.Async;
using LinqToDB.Data;
using SkiaSharp;
using StackExchange.Redis;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Service for logging user join and leave events.
///     Implements the INService interface.
/// </summary>
public class JoinLeaveLoggerService : INService, IDisposable
{
    private readonly IDataCache cache;
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly IBotCredentials credentials;
    private readonly IDataConnectionFactory dbFactory;
    private readonly EventHandler eventHandler;
    private readonly Timer flushTimer;
    private readonly GuildSettingsService guildSettingsService;
    private readonly ILogger<JoinLeaveLoggerService> logger;

    /// <summary>
    ///     Constructor for the JoinLeaveLoggerService.
    /// </summary>
    /// <param name="eventHandler">Event handler for user join and leave events.</param>
    /// <param name="cache">Data cache for storing join and leave logs.</param>
    /// <param name="dbFactory">Database service for storing join and leave logs.</param>
    /// <param name="credentials">Bot credentials for accessing the Redis database.</param>
    /// <param name="guildSettingsService">Service for getting and updating GuildConfigs in the db.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public JoinLeaveLoggerService(EventHandler eventHandler, IDataCache cache, IDataConnectionFactory dbFactory,
        IBotCredentials credentials, GuildSettingsService guildSettingsService, ILogger<JoinLeaveLoggerService> logger)
    {
        this.cache = cache;
        this.credentials = credentials;
        this.guildSettingsService = guildSettingsService;
        this.logger = logger;
        this.dbFactory = dbFactory;

        _ = LoadDataFromSqliteToRedisAsync();

        // Create a timer to flush data from Redis to SQLite every 5 minutes
        var flushInterval = TimeSpan.FromMinutes(5);
        // ReSharper disable once AsyncVoidMethod
        flushTimer = new Timer(async void (_) =>
        {
            await FlushDataToSqliteAsync();
        }, null, flushInterval, flushInterval);

        this.eventHandler = eventHandler;
        eventHandler.Subscribe("UserJoined", "JoinLeaveLoggerService", LogUserJoined);
        eventHandler.Subscribe("UserLeft", "JoinLeaveLoggerService", LogUserLeft);
    }

    /// <summary>
    ///     Disposes resources used by the service.
    /// </summary>
    public void Dispose()
    {
        eventHandler.Unsubscribe("UserJoined", "JoinLeaveLoggerService", LogUserJoined);
        eventHandler.Unsubscribe("UserLeft", "JoinLeaveLoggerService", LogUserLeft);
        flushTimer.Dispose();
        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
    }

    /// <summary>
    ///     Logs when a user joins a guild.
    /// </summary>
    /// <param name="args">The user who joined the guild.</param>
    private async Task LogUserJoined(IGuildUser args)
    {
        try
        {
            var redisDatabase = cache.Redis.GetDatabase();
            var joinEvent = new JoinLeaveLog
            {
                GuildId = args.Guild.Id, UserId = args.Id, IsJoin = true, DateAdded = DateTime.UtcNow
            };

            var serializedEvent = JsonSerializer.Serialize(joinEvent);
            var redisValues = new RedisValue[]
            {
                serializedEvent
            };
            await redisDatabase.ListRightPushAsync(GetRedisKey(args.Guild.Id), redisValues);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error logging user join event for Guild ID: {GuildId}", args.Guild.Id);
        }
    }

    /// <summary>
    ///     Logs when a user leaves a guild.
    /// </summary>
    /// <param name="guild">The guild the user left.</param>
    /// <param name="user">The user who left the guild.</param>
    private async Task LogUserLeft(IGuild guild, IUser user)
    {
        try
        {
            var redisDatabase = cache.Redis.GetDatabase();
            var leaveEvent = new JoinLeaveLog
            {
                GuildId = guild.Id, UserId = user.Id, IsJoin = false, DateAdded = DateTime.UtcNow
            };

            var serializedEvent = JsonSerializer.Serialize(leaveEvent);
            var redisValues = new RedisValue[]
            {
                serializedEvent
            };
            await redisDatabase.ListRightPushAsync(GetRedisKey(guild.Id), redisValues);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error logging user leave event for Guild ID: {GuildId}", guild.Id);
        }
    }

    /// <summary>
    ///     Generates a Redis key for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>A Redis key for the guild.</returns>
    private string GetRedisKey(ulong guildId)
    {
        return $"{credentials.RedisKey()}:joinLeaveLogs:{guildId}";
    }

    /// <summary>
    ///     Calculates the average number of joins per guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>The average number of joins per guild.</returns>
    public async Task<double> CalculateAverageJoinsPerGuildAsync(ulong guildId)
    {
        var redisDatabase = cache.Redis.GetDatabase();
        var redisKey = GetRedisKey(guildId);
        var allEvents = await redisDatabase.ListRangeAsync(redisKey);

        var joinEventsCount = allEvents
            .Select(log => JsonSerializer.Deserialize<JoinLeaveLog>((string)log))
            .Count(log => log?.IsJoin == true);

        var totalEvents = allEvents.Length;

        return totalEvents == 0 ? 0 : joinEventsCount / (double)totalEvents;
    }

    /// <summary>
    ///     Generates a graph of join events for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>A tuple containing the graph image stream and an embed for the graph.</returns>
    public async Task<(Stream ImageStream, Embed Embed)> GenerateJoinGraphAsync(ulong guildId)
    {
        var joinData = await GetGroupedJoinLeaveDataAsync(guildId, true);
        return await GenerateGraphAsync(joinData, "Join Stats Over the Last 10 Days", "Total Joins");
    }

    /// <summary>
    ///     Generates a graph of leave events for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>A tuple containing the graph image stream and an embed for the graph.</returns>
    public async Task<(Stream ImageStream, Embed Embed)> GenerateLeaveGraphAsync(ulong guildId)
    {
        var leaveData = await GetGroupedJoinLeaveDataAsync(guildId, false);
        return await GenerateGraphAsync(leaveData, "Leave Stats Over the Last 10 Days", "Total Leaves");
    }

    /// <summary>
    ///     Retrieves and groups join or leave logs for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="isJoin">Determines whether to retrieve join or leave logs.</param>
    /// <returns>A list of date-wise grouped logs.</returns>
    public async Task<List<DailyLog>> GetGroupedJoinLeaveDataAsync(ulong guildId, bool isJoin)
    {
        var redisDatabase = cache.Redis.GetDatabase();
        var redisKey = GetRedisKey(guildId);
        var allEvents = await redisDatabase.ListRangeAsync(redisKey);

        var filteredLogs = allEvents
            .Select(log => JsonSerializer.Deserialize<JoinLeaveLog>((string)log))
            .Where(log => log?.IsJoin == isJoin && log.DateAdded.HasValue)
            .Select(log => log!.DateAdded!.Value.Date)
            .GroupBy(date => date)
            .Select(group => new DailyLog
            {
                Date = group.Key, Count = group.Count()
            })
            .OrderBy(log => log.Date)
            .ToList();

        var latestDate = filteredLogs.Any() ? filteredLogs.Max(log => log.Date) : DateTime.UtcNow.Date;
        var startDate = latestDate.AddDays(-10);
        var dateRange = Enumerable.Range(0, 11)
            .Select(i => startDate.AddDays(i))
            .ToList();

        var past10DaysData = dateRange
            .GroupJoin(filteredLogs, d => d, log => log.Date, (date, logs) => new DailyLog
            {
                Date = date, Count = logs.Sum(log => log.Count)
            })
            .ToList();

        return past10DaysData;
    }

    /// <summary>
    ///     Generates a graph image and embed based on the provided data.
    /// </summary>
    /// <param name="dailyLogs">The daily logs to plot.</param>
    /// <param name="title">The title of the embed.</param>
    /// <param name="totalLabel">The label for the total count in the embed.</param>
    /// <returns>A tuple containing the graph image stream and an embed for the graph.</returns>
    private async Task<(Stream ImageStream, Embed Embed)> GenerateGraphAsync(List<DailyLog> dailyLogs,
        string title, string totalLabel)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        const int width = 1000;
        const int height = 500;
        const int padding = 60;
        const int widthWithPadding = width - 2 * padding;
        const int heightWithPadding = height - 2 * padding;

        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(new SKColor(38, 50, 56));

        // Create a gradient background for the graph line
        var shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(width, 0),
            [
                SKColors.Blue, SKColors.Cyan
            ],
            null,
            SKShaderTileMode.Clamp);

        var gridPaint = new SKPaint
        {
            Color = new SKColor(55, 71, 79), Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true
        };

        var linePaint = new SKPaint
        {
            Shader = shader, StrokeWidth = 3, IsAntialias = true, Style = SKPaintStyle.Stroke
        };

        var maxCount = dailyLogs.Max(log => (float)log.Count);
        maxCount = maxCount < 10 ? 10 : (float)Math.Ceiling(maxCount / 10) * 10;
        var scaleX = widthWithPadding / (float)(dailyLogs.Count - 1);
        var scaleY = heightWithPadding / maxCount;

        // Create font for labels
        using var labelFont = new SKFont();
        labelFont.Size = 14;

        // Create text paint
        using var textPaint = new SKPaint();
        textPaint.Color = SKColors.White;
        textPaint.IsAntialias = true;

        // Draw horizontal grid lines and y-axis labels
        var yStep = maxCount <= 30 ? 5 : 10;
        for (var i = 0; i <= maxCount; i += yStep)
        {
            var y = height - padding - i * scaleY;
            if (i != 0)
            {
                canvas.DrawLine(padding, y, width - padding, y, gridPaint);
            }

            var label = i.ToString();

            var labelWidth = labelFont.MeasureText(label, textPaint);
            var lineHeight = labelFont.Size;

            canvas.DrawText(label, padding - labelWidth - 10, y + lineHeight / 2, SKTextAlign.Left, labelFont,
                textPaint);
        }

        // Draw vertical grid lines and x-axis labels
        var xStep = scaleX;
        for (var i = 0; i < dailyLogs.Count; i++)
        {
            var x = padding + i * xStep;
            canvas.DrawLine(x, padding, x, height - padding, gridPaint);

            var label = dailyLogs[i].Date.ToString("dd MMM");

            var labelWidth = labelFont.MeasureText(label, textPaint);
            var lineHeight = labelFont.Size;

            canvas.DrawText(label, x - labelWidth / 2, height - padding + lineHeight + 5, SKTextAlign.Left, labelFont,
                textPaint);
        }

        // Draw border lines for grid (bottom and left lines)
        canvas.DrawLine(padding, height - padding, width - padding, height - padding, gridPaint);
        canvas.DrawLine(padding, padding, padding, height - padding, gridPaint);

        // Draw the graph line using a smooth curve
        var pathBuilder = new SKPathBuilder();
        if (dailyLogs.Any())
        {
            pathBuilder.MoveTo(padding, height - padding - dailyLogs[0].Count * scaleY);

            for (var i = 1; i < dailyLogs.Count; i++)
            {
                var prev = dailyLogs[i - 1];
                var current = dailyLogs[i];
                var midX = padding + (i - 0.5f) * scaleX;
                var midY = height - padding - (prev.Count + current.Count) / 2f * scaleY;
                pathBuilder.QuadTo(padding + (i - 1) * scaleX, height - padding - prev.Count * scaleY, midX, midY);
                pathBuilder.QuadTo(padding + i * scaleX, height - padding - current.Count * scaleY,
                    padding + i * scaleX,
                    height - padding - current.Count * scaleY);
            }
        }

        using var path = pathBuilder.Detach();
        canvas.DrawPath(path, linePaint);

        // Draw data points
        var pointPaint = new SKPaint
        {
            Color = SKColors.Cyan, Style = SKPaintStyle.Fill, IsAntialias = true
        };

        foreach (var (index, log) in dailyLogs.Select((log, index) => (index, log)))
        {
            var x = padding + index * scaleX;
            var y = height - padding - log.Count * scaleY;
            canvas.DrawCircle(x, y, 4, pointPaint);
        }

        // Generate the image stream
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var imageStream = new MemoryStream();
        data.SaveTo(imageStream);
        imageStream.Position = 0;

        // Build the embed
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

        return (imageStream, embedBuilder.Build());
    }

    /// <summary>
    ///     Loads data from SQLite to Redis asynchronously.
    /// </summary>
    private async Task LoadDataFromSqliteToRedisAsync()
    {
        try
        {
            var redisDatabase = cache.Redis.GetDatabase();
            await using var dbContext = await dbFactory.CreateConnectionAsync();

            var guildIds = await dbContext.JoinLeaveLogs
                .Select(e => e.GuildId)
                .Distinct()
                .ToListAsync();

            foreach (var guildId in guildIds)
            {
                var joinLeaveLogs = await dbContext.JoinLeaveLogs
                    .Where(e => e.GuildId == guildId)
                    .ToListAsync();

                var redisKey = GetRedisKey(guildId);
                var serializedLogs = joinLeaveLogs.Select(log => JsonSerializer.Serialize(log)).ToArray();

                if (serializedLogs.Any())
                {
                    var redisValues = serializedLogs.Select(x => (RedisValue)x).ToArray();
                    await redisDatabase.ListRightPushAsync(redisKey, redisValues);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading data from SQLite to Redis.");
        }
    }

    /// <summary>
    ///     Flushes data from Redis to SQLite asynchronously.
    /// </summary>
    private async Task FlushDataToSqliteAsync()
    {
        logger.LogInformation("Flushing join/leave logs to DB...");

        try
        {
            await using var dbContext = await dbFactory.CreateConnectionAsync();
            var redisDatabase = cache.Redis.GetDatabase();

            var logsToInsert = new List<JoinLeaveLog>();

            var guildIds = await dbContext.JoinLeaveLogs
                .Select(e => e.GuildId)
                .Distinct()
                .ToListAsync();

            foreach (var guildId in guildIds)
            {
                var redisKey = GetRedisKey(guildId);

                while (true)
                {
                    var serializedEvent = await redisDatabase.ListLeftPopAsync(redisKey);

                    if (serializedEvent.IsNullOrEmpty)
                        break;

                    var log = JsonSerializer.Deserialize<JoinLeaveLog>((string)serializedEvent!);
                    if (log != null)
                    {
                        logsToInsert.Add(log);
                    }
                }
            }

            if (logsToInsert.Any())
            {
                await dbContext.BulkCopyAsync(logsToInsert);
                logger.LogInformation($"Flushed {logsToInsert.Count} join/leave logs to DB.");
            }
            else
            {
                logger.LogInformation("No join/leave logs to flush.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error flushing data from Redis to SQLite.");
        }
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

    /// <summary>
    ///     Updates the graph color in the database.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="color">The color to set.</param>
    /// <param name="isJoin">Determines whether to update join or leave graph color.</param>
    private async Task UpdateGraphColorAsync(ulong guildId, uint color, bool isJoin)
    {
        try
        {
            await using var dbContext = await dbFactory.CreateConnectionAsync();
            var config = await guildSettingsService.GetGuildConfig(guildId);

            if (isJoin)
            {
                config.JoinGraphColor = color;
            }
            else
            {
                config.LeaveGraphColor = color;
            }

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