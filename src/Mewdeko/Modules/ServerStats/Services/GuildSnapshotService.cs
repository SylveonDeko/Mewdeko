using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Data;
using Mewdeko.Common.ModuleBehaviors;

namespace Mewdeko.Modules.ServerStats.Services;

/// <summary>
///     Takes an hourly sample of every guild's member and presence counts, and ages out old samples and voice
///     segments so the tables stay bounded.
/// </summary>
public class GuildSnapshotService : INService, IReadyExecutor, IDisposable
{
    /// <summary>
    ///     How long snapshots and voice segments are kept. Matches the message timestamp window.
    /// </summary>
    public static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(90);

    private static readonly TimeSpan SnapshotInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(6);

    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<GuildSnapshotService> logger;
    private readonly ServerStatsSettingsService settings;
    private readonly SemaphoreSlim snapshotLock = new(1, 1);
    private Timer? snapshotTimer;
    private Timer? sweepTimer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GuildSnapshotService" /> class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="settings">The stats settings service.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public GuildSnapshotService(DiscordShardedClient client, IDataConnectionFactory dbFactory,
        ServerStatsSettingsService settings, ILogger<GuildSnapshotService> logger)
    {
        this.client = client;
        this.dbFactory = dbFactory;
        this.settings = settings;
        this.logger = logger;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        snapshotTimer?.Dispose();
        sweepTimer?.Dispose();
        snapshotLock.Dispose();
    }

    /// <inheritdoc />
    public Task OnReadyAsync()
    {
        // Align the first snapshot to the next full hour so buckets line up across restarts.
        var now = DateTime.UtcNow;
        var nextHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc).AddHours(1);
        snapshotTimer = new Timer(_ => _ = TakeSnapshotsAsync(), null, nextHour - now, SnapshotInterval);
        sweepTimer = new Timer(_ => _ = SweepAsync(), null, TimeSpan.FromMinutes(15), SweepInterval);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Samples every guild that has snapshots enabled.
    /// </summary>
    public async Task TakeSnapshotsAsync()
    {
        if (!await snapshotLock.WaitAsync(0))
            return;

        try
        {
            var now = DateTime.UtcNow;
            var stamp = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);
            var rows = new List<GuildSnapshot>();

            foreach (var guild in client.Guilds)
            {
                if (!settings.GetCachedSettings(guild.Id).TrackSnapshots)
                    continue;

                rows.Add(Sample(guild, stamp));
            }

            if (rows.Count == 0)
                return;

            await using var db = await dbFactory.CreateConnectionAsync();
            await db.BulkCopyAsync(rows);
            logger.LogDebug("Recorded {Count} guild snapshots", rows.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Guild snapshot pass failed");
        }
        finally
        {
            snapshotLock.Release();
        }
    }

    /// <summary>
    ///     Samples one guild right now without persisting it, for live views.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="stamp">The timestamp to record.</param>
    /// <returns>The sample.</returns>
    public static GuildSnapshot Sample(SocketGuild guild, DateTime stamp)
    {
        var users = guild.Users;
        return new GuildSnapshot
        {
            GuildId = guild.Id,
            Timestamp = stamp,
            Members = guild.MemberCount,
            Humans = users.Count(u => !u.IsBot),
            Bots = users.Count(u => u.IsBot),
            Online = users.Count(u => u.Status == UserStatus.Online),
            Idle = users.Count(u => u.Status == UserStatus.Idle),
            Dnd = users.Count(u => u.Status == UserStatus.DoNotDisturb),
            Offline = users.Count(u => u.Status is UserStatus.Offline or UserStatus.Invisible),
            InVoice = guild.VoiceChannels.Sum(vc => vc.ConnectedUsers.Count)
        };
    }

    private async Task SweepAsync()
    {
        try
        {
            var cutoff = DateTime.UtcNow - RetentionPeriod;
            await using var db = await dbFactory.CreateConnectionAsync();

            var snapshots = await db.GuildSnapshots.Where(x => x.Timestamp < cutoff).DeleteAsync();
            var segments = await db.VoiceSegments.Where(x => x.EndedAt < cutoff).DeleteAsync();
            var activities = await db.ActivitySegments.Where(x => x.EndedAt < cutoff).DeleteAsync();

            if (snapshots + segments + activities > 0)
                logger.LogInformation(
                    "Stats retention sweep removed {Snapshots} snapshots, {Segments} voice segments and {Activities} activity segments",
                    snapshots, segments, activities);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Stats retention sweep failed");
        }
    }
}