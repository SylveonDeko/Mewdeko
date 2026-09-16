using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Data;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Services.Analytics;

namespace Mewdeko.Modules.ServerStats.Services;

/// <summary>
///     Records how long members spend in presence activities: games, streams, Spotify, watching and competing.
///     Presence events are rate limited and batched upstream, so a periodic sweep of every tracked guild's members
///     is the source of truth and the events only tighten segment boundaries between sweeps.
/// </summary>
public class ActivityTrackingService : INService, IReadyExecutor, IDisposable
{
    /// <summary>
    ///     Longest activity name stored, so a spoofed presence cannot bloat the table.
    /// </summary>
    private const int MaxNameLength = 128;

    /// <summary>
    ///     How often open sessions are checkpointed and member presences re-scanned.
    /// </summary>
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);

    private readonly DiscordShardedClient client;
    private readonly IAnalyticsCollector collector;
    private readonly IDataConnectionFactory dbFactory;
    private readonly EventHandler eventHandler;
    private readonly ILogger<ActivityTrackingService> logger;

    private readonly ConcurrentDictionary<(ulong GuildId, ulong UserId), Dictionary<string, ActivitySession>> sessions =
        new();

    private readonly ServerStatsSettingsService settings;
    private readonly SemaphoreSlim sweepLock = new(1, 1);
    private Timer? sweepTimer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ActivityTrackingService" /> class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="settings">The stats settings service.</param>
    /// <param name="eventHandler">The event handler.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    /// <param name="collector">The analytics collector.</param>
    public ActivityTrackingService(DiscordShardedClient client, IDataConnectionFactory dbFactory,
        ServerStatsSettingsService settings, EventHandler eventHandler, ILogger<ActivityTrackingService> logger,
        IAnalyticsCollector collector)
    {
        this.client = client;
        this.dbFactory = dbFactory;
        this.settings = settings;
        this.eventHandler = eventHandler;
        this.logger = logger;
        this.collector = collector;

        eventHandler.Subscribe("PresenceUpdated", "ActivityTrackingService", OnPresenceUpdated);
        eventHandler.Subscribe("LeftGuild", "ActivityTrackingService", OnLeftGuild);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        eventHandler.Unsubscribe("PresenceUpdated", "ActivityTrackingService", OnPresenceUpdated);
        eventHandler.Unsubscribe("LeftGuild", "ActivityTrackingService", OnLeftGuild);
        sweepTimer?.Dispose();
        sweepLock.Dispose();
    }

    /// <inheritdoc />
    public Task OnReadyAsync()
    {
        sweepTimer = new Timer(_ => _ = SweepAsync(), null, TimeSpan.FromSeconds(45), SweepInterval);
        return Task.CompletedTask;
    }

    private Task OnLeftGuild(SocketGuild guild)
    {
        foreach (var key in sessions.Keys.Where(k => k.GuildId == guild.Id))
            sessions.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    private async Task OnPresenceUpdated(SocketUser user, SocketPresence before, SocketPresence after)
    {
        if (user.IsBot)
            return;

        try
        {
            var closed = new List<ActivitySegment>();
            var now = DateTime.UtcNow;
            foreach (var guild in user.MutualGuilds)
            {
                if (!settings.GetCachedSettings(guild.Id).TrackActivities)
                    continue;

                var member = guild.GetUser(user.Id);
                if (member == null || settings.IsExcluded(guild.Id, 0, member))
                    continue;

                Reconcile(guild.Id, user.Id, after.Activities, now, closed);
            }

            if (closed.Count > 0)
                await PersistAsync(closed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error tracking presence for {UserId}", user.Id);
        }
    }

    /// <summary>
    ///     Brings one member's open sessions in line with their current activities, closing what stopped and opening
    ///     what started.
    /// </summary>
    private void Reconcile(ulong guildId, ulong userId, IReadOnlyCollection<IActivity> activities, DateTime now,
        List<ActivitySegment> closed)
    {
        var current = new Dictionary<string, IActivity>();
        foreach (var activity in activities)
        {
            if (!IsTrackable(guildId, activity))
                continue;
            current[Key(activity)] = activity;
        }

        var key = (guildId, userId);
        var open = sessions.GetOrAdd(key, _ => new Dictionary<string, ActivitySession>());
        lock (open)
        {
            foreach (var (sessionKey, session) in open.ToList())
            {
                if (current.ContainsKey(sessionKey))
                    continue;

                closed.Add(session.Close(guildId, userId, now));
                open.Remove(sessionKey);
            }

            foreach (var (activityKey, activity) in current)
            {
                if (open.ContainsKey(activityKey))
                    continue;

                open[activityKey] = new ActivitySession(Name(activity), ApplicationIdOf(activity), activity.Type, now);
            }

            if (open.Count == 0)
                sessions.TryRemove(key, out _);
        }
    }

    private bool IsTrackable(ulong guildId, IActivity activity)
    {
        if (activity.Type == ActivityType.CustomStatus || string.IsNullOrWhiteSpace(activity.Name))
            return false;

        var s = settings.GetCachedSettings(guildId);
        if (s.VerifyActivities && !IsVerified(activity))
            return false;

        return settings.IsActivityAllowed(guildId, Name(activity));
    }

    /// <summary>
    ///     Whether an activity is backed by something Discord itself attests to: a registered application, Spotify
    ///     or a stream. Plain named games from modified clients fail this.
    /// </summary>
    private static bool IsVerified(IActivity activity)
    {
        return activity switch
        {
            SpotifyGame => true,
            StreamingGame => true,
            RichGame rich => rich.ApplicationId != 0,
            _ => false
        };
    }

    private static string Name(IActivity activity)
    {
        var name = activity.Name.Trim();
        return name.Length > MaxNameLength ? name[..MaxNameLength] : name;
    }

    private static ulong? ApplicationIdOf(IActivity activity)
    {
        return activity is RichGame { ApplicationId: not 0 } rich ? rich.ApplicationId : null;
    }

    private static string Key(IActivity activity)
    {
        return $"{(int)activity.Type}|{Name(activity).ToLowerInvariant()}";
    }

    /// <summary>
    ///     Re-scans every tracked guild and checkpoints long running sessions so stats stay current.
    /// </summary>
    private async Task SweepAsync()
    {
        if (!await sweepLock.WaitAsync(0))
            return;

        try
        {
            var now = DateTime.UtcNow;
            var closed = new List<ActivitySegment>();
            var seen = new HashSet<(ulong, ulong)>();

            foreach (var guild in client.Guilds)
            {
                if (!settings.GetCachedSettings(guild.Id).TrackActivities)
                    continue;

                foreach (var member in guild.Users)
                {
                    if (member.IsBot || member.Activities.Count == 0 && !sessions.ContainsKey((guild.Id, member.Id)))
                        continue;
                    if (settings.IsExcluded(guild.Id, 0, member))
                        continue;

                    seen.Add((guild.Id, member.Id));
                    Reconcile(guild.Id, member.Id, member.Activities, now, closed);
                }
            }

            // Sessions for guilds that stopped tracking, or members no longer visible, are closed out.
            foreach (var (key, open) in sessions.ToArray())
            {
                if (seen.Contains(key))
                {
                    lock (open)
                    {
                        foreach (var (sessionKey, session) in open.ToList())
                        {
                            if (now - session.StartedAt < TimeSpan.FromMinutes(1))
                                continue;
                            closed.Add(session.Close(key.GuildId, key.UserId, now));
                            open[sessionKey] = new ActivitySession(session.Name, session.ApplicationId, session.Type,
                                now);
                        }
                    }

                    continue;
                }

                if (!sessions.TryRemove(key, out var stale))
                    continue;
                lock (stale)
                {
                    foreach (var session in stale.Values)
                        closed.Add(session.Close(key.GuildId, key.UserId, now));
                }
            }

            if (closed.Count > 0)
                await PersistAsync(closed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Activity tracking sweep failed");
        }
        finally
        {
            sweepLock.Release();
        }
    }

    private async Task PersistAsync(List<ActivitySegment> segments)
    {
        segments = segments.Where(s => s.Seconds > 0).ToList();
        if (segments.Count == 0)
            return;

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.BulkCopyAsync(segments);

        foreach (var group in segments.GroupBy(s => (s.GuildId, s.UserId, s.Name, s.Type)))
        {
            var seconds = group.Sum(s => (long)s.Seconds);
            var appId = group.Select(s => s.ApplicationId).FirstOrDefault(a => a.HasValue);
            var updated = await db.ActivityTotals
                .Where(x => x.GuildId == group.Key.GuildId && x.UserId == group.Key.UserId &&
                            x.Name == group.Key.Name && x.Type == group.Key.Type)
                .Set(x => x.Seconds, x => x.Seconds + seconds)
                .UpdateAsync();

            if (updated == 0)
            {
                await db.InsertAsync(new ActivityTotal
                {
                    GuildId = group.Key.GuildId,
                    UserId = group.Key.UserId,
                    ApplicationId = appId,
                    Name = group.Key.Name,
                    Type = group.Key.Type,
                    Seconds = seconds,
                    DateAdded = DateTime.UtcNow
                });
            }

            collector.Feature("activity_stats", group.Key.GuildId);
        }
    }

    /// <summary>
    ///     Gets the seconds a member has accrued in their open sessions, keyed by activity name, so live numbers
    ///     include the current session.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <returns>Open seconds per activity name.</returns>
    public Dictionary<string, int> GetOpenSessionSeconds(ulong guildId, ulong userId)
    {
        if (!sessions.TryGetValue((guildId, userId), out var open))
            return new Dictionary<string, int>();

        lock (open)
        {
            return open.Values.ToDictionary(s => s.Name, s => (int)(DateTime.UtcNow - s.StartedAt).TotalSeconds,
                StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    ///     Counts members currently in a tracked activity, optionally one by name.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="name">An activity name, or null for any.</param>
    /// <returns>The member count.</returns>
    public int CountActiveNow(ulong guildId, string? name = null)
    {
        var count = 0;
        foreach (var (key, open) in sessions)
        {
            if (key.GuildId != guildId)
                continue;
            lock (open)
            {
                if (name == null
                        ? open.Count > 0
                        : open.Values.Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    count++;
            }
        }

        return count;
    }

    /// <summary>
    ///     An open stretch of one activity for one member.
    /// </summary>
    private sealed class ActivitySession(string name, ulong? applicationId, ActivityType type, DateTime startedAt)
    {
        /// <summary>The activity name.</summary>
        public string Name { get; } = name;

        /// <summary>The Discord application, when known.</summary>
        public ulong? ApplicationId { get; } = applicationId;

        /// <summary>The activity type.</summary>
        public ActivityType Type { get; } = type;

        /// <summary>When the stretch started.</summary>
        public DateTime StartedAt { get; } = startedAt;

        /// <summary>Closes the stretch into a persisted segment.</summary>
        public ActivitySegment Close(ulong guildId, ulong userId, DateTime endedAt)
        {
            return new ActivitySegment
            {
                GuildId = guildId,
                UserId = userId,
                ApplicationId = ApplicationId,
                Name = Name,
                Type = (int)Type,
                StartedAt = StartedAt,
                EndedAt = endedAt,
                Seconds = (int)Math.Max(0, (endedAt - StartedAt).TotalSeconds)
            };
        }
    }
}