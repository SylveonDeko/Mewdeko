using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Data;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.ServerStats.Common;
using Mewdeko.Services.Analytics;

namespace Mewdeko.Modules.ServerStats.Services;

/// <summary>
///     Records how long members spend in voice channels. Every channel move or mute, deafen, stream or camera change
///     closes the current segment and opens a new one, and long sessions are checkpointed periodically so stats stay
///     current while people are still connected.
/// </summary>
public class VoiceActivityService : INService, IReadyExecutor, IDisposable
{
    /// <summary>
    ///     How often open sessions are checkpointed into segments.
    /// </summary>
    private static readonly TimeSpan CheckpointInterval = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim checkpointLock = new(1, 1);

    private readonly DiscordShardedClient client;
    private readonly IAnalyticsCollector collector;
    private readonly IDataConnectionFactory dbFactory;
    private readonly EventHandler eventHandler;
    private readonly ILogger<VoiceActivityService> logger;
    private readonly ConcurrentDictionary<(ulong GuildId, ulong UserId), VoiceSession> sessions = new();
    private readonly ServerStatsSettingsService settings;
    private Timer? checkpointTimer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="VoiceActivityService" /> class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="settings">The stats settings service.</param>
    /// <param name="eventHandler">The event handler.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    /// <param name="collector">The analytics collector.</param>
    public VoiceActivityService(DiscordShardedClient client, IDataConnectionFactory dbFactory,
        ServerStatsSettingsService settings, EventHandler eventHandler, ILogger<VoiceActivityService> logger,
        IAnalyticsCollector collector)
    {
        this.client = client;
        this.dbFactory = dbFactory;
        this.settings = settings;
        this.eventHandler = eventHandler;
        this.logger = logger;
        this.collector = collector;

        eventHandler.Subscribe("UserVoiceStateUpdated", "VoiceActivityService", OnVoiceStateUpdated);
        eventHandler.Subscribe("LeftGuild", "VoiceActivityService", OnLeftGuild);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        eventHandler.Unsubscribe("UserVoiceStateUpdated", "VoiceActivityService", OnVoiceStateUpdated);
        eventHandler.Unsubscribe("LeftGuild", "VoiceActivityService", OnLeftGuild);
        checkpointTimer?.Dispose();
        checkpointLock.Dispose();
    }

    /// <inheritdoc />
    public Task OnReadyAsync()
    {
        var now = DateTime.UtcNow;
        var started = 0;
        foreach (var guild in client.Guilds)
        {
            if (!settings.GetCachedSettings(guild.Id).TrackVoice)
                continue;

            foreach (var channel in guild.VoiceChannels)
            foreach (var user in channel.ConnectedUsers)
            {
                if (settings.IsExcluded(guild.Id, channel.Id, user))
                    continue;
                sessions[(guild.Id, user.Id)] = new VoiceSession(channel.Id, now, ComputeState(guild, channel, user));
                started++;
            }
        }

        checkpointTimer = new Timer(_ => _ = CheckpointAsync(), null, CheckpointInterval, CheckpointInterval);
        logger.LogInformation("Voice activity tracking ready with {Sessions} open sessions", started);
        return Task.CompletedTask;
    }

    private Task OnLeftGuild(SocketGuild guild)
    {
        foreach (var key in sessions.Keys.Where(k => k.GuildId == guild.Id))
            sessions.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    private async Task OnVoiceStateUpdated(SocketUser user, SocketVoiceState before, SocketVoiceState after)
    {
        if (user is not SocketGuildUser guildUser)
            return;

        var guild = guildUser.Guild;
        var now = DateTime.UtcNow;
        var key = (guild.Id, guildUser.Id);

        try
        {
            var closed = new List<VoiceSegment>();
            if (sessions.TryRemove(key, out var session))
                closed.Add(session.Close(guild.Id, guildUser.Id, now));

            var trackable = after.VoiceChannel != null
                            && settings.GetCachedSettings(guild.Id).TrackVoice
                            && !settings.IsExcluded(guild.Id, after.VoiceChannel.Id, guildUser);
            if (trackable)
            {
                sessions[key] = new VoiceSession(after.VoiceChannel!.Id, now,
                    ComputeState(guild, after.VoiceChannel, guildUser));
            }

            // Other members in the affected channels may have gained or lost company, which changes their state.
            RestartPeers(guild, before.VoiceChannel, guildUser.Id, now, closed);
            if (after.VoiceChannel != null && after.VoiceChannel.Id != before.VoiceChannel?.Id)
                RestartPeers(guild, after.VoiceChannel, guildUser.Id, now, closed);

            if (closed.Count > 0)
                await PersistAsync(closed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error tracking voice state for {UserId} in {GuildId}", user.Id, guild.Id);
        }
    }

    private void RestartPeers(SocketGuild guild, SocketVoiceChannel? channel, ulong changedUserId, DateTime now,
        List<VoiceSegment> closed)
    {
        if (channel == null)
            return;

        foreach (var peer in channel.ConnectedUsers)
        {
            if (peer.Id == changedUserId)
                continue;

            var key = (guild.Id, peer.Id);
            if (!sessions.TryGetValue(key, out var session))
                continue;

            var state = ComputeState(guild, channel, peer);
            if (state == session.State)
                continue;

            closed.Add(session.Close(guild.Id, peer.Id, now));
            sessions[key] = new VoiceSession(channel.Id, now, state);
        }
    }

    private static VoiceStateFlags ComputeState(SocketGuild guild, SocketVoiceChannel channel, SocketGuildUser user)
    {
        var state = VoiceStateFlags.Normal;
        if (user.IsSelfMuted) state |= VoiceStateFlags.SelfMuted;
        if (user.IsSelfDeafened) state |= VoiceStateFlags.SelfDeafened;
        if (user.IsMuted) state |= VoiceStateFlags.ServerMuted;
        if (user.IsDeafened) state |= VoiceStateFlags.ServerDeafened;
        if (user.IsStreaming) state |= VoiceStateFlags.Streaming;
        if (user.IsVideoing) state |= VoiceStateFlags.Video;
        if (guild.AFKChannel?.Id == channel.Id) state |= VoiceStateFlags.Afk;
        if (channel.ConnectedUsers.Count(u => !u.IsBot) <= 1) state |= VoiceStateFlags.Alone;
        return state;
    }

    private async Task CheckpointAsync()
    {
        if (!await checkpointLock.WaitAsync(0))
            return;

        try
        {
            var now = DateTime.UtcNow;
            var closed = new List<VoiceSegment>();
            foreach (var (key, session) in sessions.ToArray())
            {
                if (now - session.StartedAt < TimeSpan.FromMinutes(1))
                    continue;

                closed.Add(session.Close(key.GuildId, key.UserId, now));
                sessions[key] = new VoiceSession(session.ChannelId, now, session.State);
            }

            if (closed.Count > 0)
                await PersistAsync(closed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Voice activity checkpoint failed");
        }
        finally
        {
            checkpointLock.Release();
        }
    }

    private async Task PersistAsync(List<VoiceSegment> segments)
    {
        segments = segments.Where(s => s.Seconds > 0).ToList();
        if (segments.Count == 0)
            return;

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.BulkCopyAsync(segments);

        foreach (var group in segments.GroupBy(s => (s.GuildId, s.ChannelId, s.UserId)))
        {
            var mask = settings.GetCachedSettings(group.Key.GuildId).VoiceStates;
            var seconds = group.Where(s => (s.State & mask) == 0).Sum(s => (long)s.Seconds);
            if (seconds <= 0)
                continue;

            var updated = await db.VoiceTotals
                .Where(x => x.GuildId == group.Key.GuildId && x.ChannelId == group.Key.ChannelId &&
                            x.UserId == group.Key.UserId)
                .Set(x => x.Seconds, x => x.Seconds + seconds)
                .UpdateAsync();

            if (updated == 0)
            {
                await db.InsertAsync(new VoiceTotal
                {
                    GuildId = group.Key.GuildId,
                    ChannelId = group.Key.ChannelId,
                    UserId = group.Key.UserId,
                    Seconds = seconds,
                    DateAdded = DateTime.UtcNow
                });
            }

            collector.Feature("voice_stats", group.Key.GuildId);
        }
    }

    /// <summary>
    ///     Gets the seconds a member has accrued in their current open session, so live numbers include it.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <returns>Open session seconds, or 0.</returns>
    public int GetOpenSessionSeconds(ulong guildId, ulong userId)
    {
        return sessions.TryGetValue((guildId, userId), out var session)
            ? (int)(DateTime.UtcNow - session.StartedAt).TotalSeconds
            : 0;
    }

    /// <summary>
    ///     An open stretch of voice time for one member.
    /// </summary>
    private sealed class VoiceSession(ulong channelId, DateTime startedAt, VoiceStateFlags state)
    {
        /// <summary>
        ///     The channel the member is in.
        /// </summary>
        public ulong ChannelId { get; } = channelId;

        /// <summary>
        ///     When this stretch started.
        /// </summary>
        public DateTime StartedAt { get; } = startedAt;

        /// <summary>
        ///     The state in effect for this stretch.
        /// </summary>
        public VoiceStateFlags State { get; } = state;

        /// <summary>
        ///     Closes the stretch into a persisted segment.
        /// </summary>
        public VoiceSegment Close(ulong guildId, ulong userId, DateTime endedAt)
        {
            return new VoiceSegment
            {
                GuildId = guildId,
                ChannelId = ChannelId,
                UserId = userId,
                StartedAt = StartedAt,
                EndedAt = endedAt,
                Seconds = (int)Math.Max(0, (endedAt - StartedAt).TotalSeconds),
                State = (int)State
            };
        }
    }
}