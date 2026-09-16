using System.IO;
using System.Net;
using System.Threading;
using DataModel;
using Discord.Net;
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
///     Keeps pinned leaderboards, charts and overviews up to date by editing the same message on a schedule, so a
///     channel always shows current numbers without anyone running a command.
/// </summary>
public class LiveBoardService : INService, IReadyExecutor, IDisposable
{
    /// <summary>
    ///     The shortest refresh interval a board may use.
    /// </summary>
    public const int MinimumIntervalMinutes = 5;

    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(1);

    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly InviteCountService invites;
    private readonly ILogger<LiveBoardService> logger;

    /// <summary>
    ///     One gate per board so the scheduled tick, a manual refresh, creation and deletion never race each other
    ///     into sending the same board twice.
    /// </summary>
    private readonly ConcurrentDictionary<int, SemaphoreSlim> refreshLocks = new();

    private readonly ServerStatsService stats;
    private readonly GeneratedBotStrings strings;
    private readonly SemaphoreSlim tickLock = new(1, 1);
    private Timer? tickTimer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LiveBoardService" /> class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="stats">The stats query service.</param>
    /// <param name="invites">The invite tracking service.</param>
    /// <param name="strings">Localized strings.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public LiveBoardService(DiscordShardedClient client, IDataConnectionFactory dbFactory, ServerStatsService stats,
        InviteCountService invites, GeneratedBotStrings strings, ILogger<LiveBoardService> logger)
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
        tickTimer = new Timer(_ => _ = TickAsync(), null, TimeSpan.FromMinutes(1), TickInterval);
        return Task.CompletedTask;
    }

    #region CRUD

    /// <summary>
    ///     Lists a guild's live boards.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The boards.</returns>
    public async Task<List<LiveBoard>> GetAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.LiveBoards.Where(x => x.GuildId == guildId).OrderBy(x => x.Id).ToListAsync();
    }

    /// <summary>
    ///     Creates a live board and sends its first message.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="channel">The channel to post in.</param>
    /// <param name="kind">What to show.</param>
    /// <param name="range">The window.</param>
    /// <param name="pin">Whether to pin the message.</param>
    /// <param name="entries">Rows for leaderboards.</param>
    /// <param name="intervalMinutes">Refresh interval.</param>
    /// <returns>The board.</returns>
    public async Task<LiveBoard> CreateAsync(SocketGuild guild, ITextChannel channel, LiveBoardKind kind,
        StatsRange range, bool pin, int entries, int intervalMinutes)
    {
        var board = new LiveBoard
        {
            GuildId = guild.Id,
            ChannelId = channel.Id,
            Kind = (int)kind,
            Range = (int)range,
            Pin = pin,
            Entries = Math.Clamp(entries, 3, 25),
            IntervalMinutes = Math.Max(MinimumIntervalMinutes, intervalMinutes),
            DateAdded = DateTime.UtcNow
        };

        await using (var db = await dbFactory.CreateConnectionAsync())
        {
            board.Id = await db.InsertWithInt32IdentityAsync(board);
        }

        await RefreshAsync(guild, board);
        return board;
    }

    /// <summary>
    ///     Deletes a live board and its message.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="id">The board ID.</param>
    /// <returns>True when deleted.</returns>
    public async Task<bool> DeleteAsync(SocketGuild guild, int id)
    {
        var gate = refreshLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var board = await db.LiveBoards.FirstOrDefaultAsync(x => x.GuildId == guild.Id && x.Id == id);
            if (board == null)
                return false;

            await db.LiveBoards.Where(x => x.Id == id).DeleteAsync();
            await TryDeleteMessageAsync(guild, board);
            return true;
        }
        finally
        {
            gate.Release();
            refreshLocks.TryRemove(id, out _);
        }
    }

    /// <summary>
    ///     Deletes a board's message if it can still be found. Failures are ignored because the message may already
    ///     be gone or the channel may no longer be accessible.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="board">The board whose message to delete.</param>
    private static async Task TryDeleteMessageAsync(SocketGuild guild, LiveBoard board)
    {
        if (board.MessageId == 0 || guild.GetTextChannel(board.ChannelId) is not { } channel)
            return;

        try
        {
            await channel.DeleteMessageAsync(board.MessageId);
        }
        catch
        {
        }
    }

    #endregion

    #region Refresh

    private async Task TickAsync()
    {
        if (!await tickLock.WaitAsync(0))
            return;

        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var now = DateTime.UtcNow;
            var boards = await db.LiveBoards.ToListAsync();
            var due = boards.Where(b => b.LastUpdateAt == null ||
                                        now - b.LastUpdateAt.Value >= TimeSpan.FromMinutes(
                                            Math.Max(MinimumIntervalMinutes, b.IntervalMinutes)));

            foreach (var board in due)
            {
                var guild = client.GetGuild(board.GuildId);
                if (guild == null)
                    continue;

                try
                {
                    await RefreshAsync(guild, board);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Live board {Id} refresh failed, retrying after its interval", board.Id);
                    await db.LiveBoards.Where(x => x.Id == board.Id)
                        .Set(x => x.LastUpdateAt, now)
                        .UpdateAsync();
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Live board tick failed");
        }
        finally
        {
            tickLock.Release();
        }
    }

    /// <summary>
    ///     Re-renders a board and edits its message in place. A new message is only posted when the board has none
    ///     yet or Discord reports the old one deleted; any other failure propagates so the caller retries later rather
    ///     than leaving a trail of duplicate (and duplicate pinned) messages in the channel.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="board">The board.</param>
    public async Task RefreshAsync(SocketGuild guild, LiveBoard board)
    {
        var channel = guild.GetTextChannel(board.ChannelId);
        if (channel == null)
        {
            await using var cleanup = await dbFactory.CreateConnectionAsync();
            await cleanup.LiveBoards.Where(x => x.Id == board.Id).DeleteAsync();
            refreshLocks.TryRemove(board.Id, out _);
            return;
        }

        var gate = refreshLocks.GetOrAdd(board.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            var stored = await GetStoredMessageIdAsync(board.Id);
            if (stored == null)
                return;

            board.MessageId = stored.Value;

            var (embed, image) = await RenderAsync(guild, board);
            await using var _ = image;

            if (board.MessageId == 0 || !await TryEditAsync(channel, board.MessageId, embed, image))
                board.MessageId = await SendAsync(channel, board, embed, image);

            board.LastUpdateAt = DateTime.UtcNow;
            await using var db = await dbFactory.CreateConnectionAsync();
            await db.LiveBoards.Where(x => x.Id == board.Id)
                .Set(x => x.MessageId, board.MessageId)
                .Set(x => x.LastUpdateAt, board.LastUpdateAt)
                .UpdateAsync();
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    ///     Reads the board's current message ID from the database. Callers hold stale copies of the board (the tick
    ///     loads its own list, commands load another), so this is what decides between editing and posting.
    /// </summary>
    /// <param name="boardId">The board ID.</param>
    /// <returns>The stored message ID, or null when the board has been deleted.</returns>
    private async Task<ulong?> GetStoredMessageIdAsync(int boardId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.LiveBoards.Where(x => x.Id == boardId).Select(x => (ulong?)x.MessageId)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Edits the board's existing message by ID without fetching it first, so a missing Read Message History
    ///     permission or a transient fetch failure can no longer be mistaken for a deleted message.
    /// </summary>
    /// <param name="channel">The channel the message is in.</param>
    /// <param name="messageId">The message to edit.</param>
    /// <param name="embed">The new embed.</param>
    /// <param name="image">The new chart image, if any.</param>
    /// <returns>False only when Discord reports the message no longer exists.</returns>
    private static async Task<bool> TryEditAsync(ITextChannel channel, ulong messageId, Embed embed,
        MemoryStream? image)
    {
        try
        {
            await channel.ModifyMessageAsync(messageId, props =>
            {
                props.Embed = embed;
                props.Content = "";
                if (image != null)
                    props.Attachments = new[]
                    {
                        new FileAttachment(image, "graph.png")
                    };
            });
            return true;
        }
        catch (HttpException ex) when (ex.HttpCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    /// <summary>
    ///     Posts a fresh board message and pins it when the board asks for that.
    /// </summary>
    /// <param name="channel">The channel to post in.</param>
    /// <param name="board">The board.</param>
    /// <param name="embed">The embed.</param>
    /// <param name="image">The chart image, if any.</param>
    /// <returns>The new message ID.</returns>
    private async Task<ulong> SendAsync(ITextChannel channel, LiveBoard board, Embed embed, MemoryStream? image)
    {
        var message = image != null
            ? await channel.SendFileAsync(image, "graph.png", embed: embed)
            : await channel.SendMessageAsync(embed: embed);

        if (board.Pin)
        {
            try
            {
                await message.PinAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not pin live board {Id}", board.Id);
            }
        }

        return message.Id;
    }

    private static int RangeToDays(StatsRange range, bool chart)
    {
        return range switch
        {
            StatsRange.Daily => 1,
            StatsRange.Weekly => 7,
            StatsRange.Monthly => 30,
            _ => chart ? 90 : 0
        };
    }

    /// <summary>
    ///     Renders a board's current content.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="board">The board.</param>
    /// <returns>The embed and, for charts, the PNG to attach as graph.png.</returns>
    public async Task<(Embed Embed, MemoryStream? Image)> RenderAsync(SocketGuild guild, LiveBoard board)
    {
        var kind = (LiveBoardKind)board.Kind;
        var range = (StatsRange)board.Range;
        var footer = strings.LiveBoardFooter(guild.Id, board.IntervalMinutes);

        switch (kind)
        {
            case LiveBoardKind.InviteLeaderboard:
            {
                var rows = await invites.GetInviteLeaderboardAsync(guild, range, limit: board.Entries);
                var eb = new EmbedBuilder().WithOkColor()
                    .WithTitle($"{strings.InviteLeaderboardTitle(guild.Id)} ({range.DisplayName()})")
                    .WithDescription(rows.Count == 0
                        ? strings.NoInviteData(guild.Id)
                        : string.Join("\n", rows.Select((x, i) =>
                            $"`{i + 1}.` <@{x.UserId}>: **{x.Total}** (✅ {x.Regular} · 🚪 {x.Left} · ⚠️ {x.Fake})")))
                    .WithFooter(footer).WithCurrentTimestamp();
                return (eb.Build(), null);
            }
            case LiveBoardKind.MessageLeaderboard:
            case LiveBoardKind.VoiceLeaderboard:
            {
                var statKind = kind == LiveBoardKind.MessageLeaderboard ? StatKind.Messages : StatKind.Voice;
                var days = RangeToDays(range, false);
                var rows = await stats.GetTopUsersAsync(guild.Id, statKind, days, board.Entries);
                var title = strings.StatsTopTitle(guild.Id, ServerStatsEmbeds.KindName(strings, guild.Id, statKind),
                    ServerStatsEmbeds.WindowName(days));
                var eb = new EmbedBuilder().WithOkColor()
                    .WithTitle(title)
                    .WithDescription(rows.Count == 0
                        ? strings.StatsNoData(guild.Id)
                        : string.Join("\n", rows.Select((x, i) =>
                            $"`{i + 1}.` <@{x.Id}>: **{ServerStatsEmbeds.FormatValue(statKind, x.Value)}**")))
                    .WithFooter(footer).WithCurrentTimestamp();
                return (eb.Build(), null);
            }
            case LiveBoardKind.ServerOverview:
            {
                var days = RangeToDays(range, false);
                var overview = await stats.GetOverviewAsync(guild, days);
                var topGame = (await stats.GetTopActivitiesAsync(guild.Id, days, 1)).FirstOrDefault();
                var eb = ServerStatsEmbeds.Overview(strings, guild, overview, topGame).WithFooter(footer)
                    .WithCurrentTimestamp();
                return (eb.Build(), null);
            }
            case LiveBoardKind.ActivityLeaderboard:
            {
                var days = RangeToDays(range, false);
                var rows = await stats.GetTopActivitiesAsync(guild.Id, days, board.Entries);
                var eb = ServerStatsEmbeds.Activities(strings, guild, days, rows).WithFooter(footer)
                    .WithCurrentTimestamp();
                return (eb.Build(), null);
            }
            case LiveBoardKind.InviteStats:
            {
                var analytics = await invites.GetAnalyticsAsync(guild, range);
                var eb = new EmbedBuilder().WithOkColor()
                    .WithTitle(strings.InviteStatsTitle(guild.Id, range.DisplayName()))
                    .AddField(strings.InviteStatsJoins(guild.Id), analytics.Joins.ToString("N0"), true)
                    .AddField(strings.InviteStatsLeaves(guild.Id), analytics.Leaves.ToString("N0"), true)
                    .AddField(strings.InviteStatsNet(guild.Id), analytics.NetGrowth.ToString("+#,0;-#,0;0"), true)
                    .AddField(strings.InviteStatsRetention(guild.Id),
                        analytics.Retention.HasValue ? $"{analytics.Retention.Value:P0}" : "-", true)
                    .AddField(strings.InviteStatsFake(guild.Id), analytics.FakeJoins.ToString("N0"), true)
                    .AddField(strings.InviteStatsSources(guild.Id),
                        strings.InviteStatsSourcesValue(guild.Id, analytics.ViaInvite, analytics.ViaVanity,
                            analytics.ViaBot, analytics.Unknown), true)
                    .WithFooter(footer).WithCurrentTimestamp();
                if (analytics.TopInviters.Count > 0)
                    eb.AddField(strings.InviteStatsTopInviters(guild.Id), string.Join("\n",
                        analytics.TopInviters.Select((x, i) => $"{i + 1}. <@{x.UserId}>: **{x.Total}**")));
                return (eb.Build(), null);
            }
            default:
            {
                var chartKind = kind switch
                {
                    LiveBoardKind.JoinsChart => StatChartKind.Joins,
                    LiveBoardKind.LeavesChart => StatChartKind.Leaves,
                    LiveBoardKind.MembersChart => StatChartKind.Members,
                    LiveBoardKind.MessagesChart => StatChartKind.Messages,
                    _ => StatChartKind.Growth
                };
                var days = RangeToDays(range, true);
                var (stream, embed) = await ServerStatsEmbeds.ChartAsync(stats, guild, chartKind, days);
                var eb = embed.ToEmbedBuilder().WithFooter(footer);
                return (eb.Build(), stream);
            }
        }
    }

    #endregion
}