using System.Text;
using DataModel;
using LinqToDB.Async;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.ServerStats.Common;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.ServerStats.Services;

/// <summary>
///     Answers every "how active is X over the last N days" question from message timestamps, voice segments, guild
///     snapshots and join/leave logs. Windows up to 90 days read the time bounded tables; a window of 0 means all
///     time and reads the lifetime totals instead.
/// </summary>
public class ServerStatsService : INService
{
    /// <summary>
    ///     The widest window the time bounded tables can answer.
    /// </summary>
    public const int MaxLookbackDays = 90;

    private readonly ActivityTrackingService activities;
    private readonly IDataConnectionFactory dbFactory;
    private readonly JoinLeaveLoggerService joinLeave;
    private readonly ServerStatsSettingsService settings;
    private readonly VoiceActivityService voice;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ServerStatsService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="settings">The stats settings service.</param>
    /// <param name="voice">The voice activity tracker.</param>
    /// <param name="joinLeave">The join and leave logger.</param>
    /// <param name="activities">The presence activity tracker.</param>
    public ServerStatsService(IDataConnectionFactory dbFactory, ServerStatsSettingsService settings,
        VoiceActivityService voice, JoinLeaveLoggerService joinLeave, ActivityTrackingService activities)
    {
        this.dbFactory = dbFactory;
        this.settings = settings;
        this.voice = voice;
        this.joinLeave = joinLeave;
        this.activities = activities;
    }

    /// <summary>
    ///     Resolves the window to use for a command: the requested one clamped to the supported range, or the guild's
    ///     default when none was given.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="requested">The requested days, 0 for all time, or null for the default.</param>
    /// <returns>The window in days.</returns>
    public int ResolveLookback(ulong guildId, int? requested)
    {
        var days = requested ?? settings.GetCachedSettings(guildId).DefaultLookbackDays;
        return Math.Clamp(days, 0, MaxLookbackDays);
    }

    private static DateTime Since(int lookbackDays)
    {
        return DateTime.UtcNow.AddDays(-lookbackDays);
    }

    private (List<ulong> Channels, List<ulong> Users) Exclusions(ulong guildId)
    {
        return (settings.GetExcludedChannels(guildId).ToList(), settings.GetExcludedUsers(guildId).ToList());
    }

    private static async Task<TopEntry?> TopAsync(IQueryable<Ranked> ranked)
    {
        return (await ranked.OrderByDescending(x => x.Value).FirstOrDefaultAsync())?.ToEntry();
    }

    private static async Task<List<TopEntry>> TopListAsync(IQueryable<Ranked> ranked, int limit)
    {
        return (await ranked.OrderByDescending(x => x.Value).Take(limit).ToListAsync())
            .Select(x => x.ToEntry()).ToList();
    }

    private IQueryable<MessageTimestamp> Timestamps(MewdekoDb db, ulong guildId,
        int lookbackDays)
    {
        var since = Since(lookbackDays);
        var (channels, users) = Exclusions(guildId);
        var query = db.MessageTimestamps.Where(x => x.GuildId == guildId && x.Timestamp >= since);
        if (channels.Count > 0) query = query.Where(x => !channels.Contains(x.ChannelId));
        if (users.Count > 0) query = query.Where(x => !users.Contains(x.UserId));
        return query;
    }

    private IQueryable<MessageCount> Counts(MewdekoDb db, ulong guildId)
    {
        var (channels, users) = Exclusions(guildId);
        var query = db.MessageCounts.Where(x => x.GuildId == guildId);
        if (channels.Count > 0) query = query.Where(x => !channels.Contains(x.ChannelId));
        if (users.Count > 0) query = query.Where(x => !users.Contains(x.UserId));
        return query;
    }

    private IQueryable<VoiceSegment> Segments(MewdekoDb db, ulong guildId,
        int lookbackDays)
    {
        var since = Since(lookbackDays);
        var mask = settings.GetCachedSettings(guildId).VoiceStates;
        var (channels, users) = Exclusions(guildId);
        var query = db.VoiceSegments.Where(x => x.GuildId == guildId && x.EndedAt >= since);
        if (mask != 0) query = query.Where(x => (x.State & mask) == 0);
        if (channels.Count > 0) query = query.Where(x => !channels.Contains(x.ChannelId));
        if (users.Count > 0) query = query.Where(x => !users.Contains(x.UserId));
        return query;
    }

    private IQueryable<VoiceTotal> Totals(MewdekoDb db, ulong guildId)
    {
        var (channels, users) = Exclusions(guildId);
        var query = db.VoiceTotals.Where(x => x.GuildId == guildId);
        if (channels.Count > 0) query = query.Where(x => !channels.Contains(x.ChannelId));
        if (users.Count > 0) query = query.Where(x => !users.Contains(x.UserId));
        return query;
    }

    private IQueryable<ActivitySegment> ActivitySegments(MewdekoDb db, ulong guildId,
        int lookbackDays, string? name = null)
    {
        var since = Since(lookbackDays);
        var users = settings.GetExcludedUsers(guildId).ToList();
        var query = db.ActivitySegments.Where(x => x.GuildId == guildId && x.EndedAt >= since);
        if (users.Count > 0) query = query.Where(x => !users.Contains(x.UserId));
        if (name != null)
        {
            var lowered = name.ToLowerInvariant();
            query = query.Where(x => x.Name.ToLower() == lowered);
        }

        return query;
    }

    private IQueryable<ActivityTotal> ActivityTotals(MewdekoDb db, ulong guildId,
        string? name = null)
    {
        var users = settings.GetExcludedUsers(guildId).ToList();
        var query = db.ActivityTotals.Where(x => x.GuildId == guildId);
        if (users.Count > 0) query = query.Where(x => !users.Contains(x.UserId));
        if (name != null)
        {
            var lowered = name.ToLowerInvariant();
            query = query.Where(x => x.Name.ToLower() == lowered);
        }

        return query;
    }

    #region Export

    /// <summary>
    ///     Renders a member ranking as CSV.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="kind">Messages or voice.</param>
    /// <param name="lookbackDays">The window in days, or 0 for all time.</param>
    /// <returns>CSV text.</returns>
    public async Task<string> ExportCsvAsync(IGuild guild, StatKind kind, int lookbackDays)
    {
        var totals = await GetPerUserTotalsAsync(guild.Id, kind, lookbackDays);
        var sb = new StringBuilder();
        sb.AppendLine(kind switch
        {
            StatKind.Messages => "rank,user_id,username,messages",
            StatKind.Voice => "rank,user_id,username,voice_seconds,voice_hours",
            _ => "rank,user_id,username,activity_seconds,activity_hours"
        });

        var rank = 1;
        foreach (var (userId, value) in totals.OrderByDescending(x => x.Value))
        {
            var user = await guild.GetUserAsync(userId);
            var name = user?.Username ?? "";
            if (name.Contains(',') || name.Contains('"'))
                name = $"\"{name.Replace("\"", "\"\"")}\"";

            sb.Append(rank++).Append(',').Append(userId).Append(',').Append(name).Append(',').Append(value);
            if (kind != StatKind.Messages)
                sb.Append(',').Append((value / 3600.0).ToString("F2"));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    #endregion

    /// <summary>
    ///     Formats seconds as a compact duration such as 3d 4h, 5h 12m or 42m.
    /// </summary>
    /// <param name="seconds">The duration in seconds.</param>
    /// <returns>The formatted text.</returns>
    public static string FormatDuration(long seconds)
    {
        var span = TimeSpan.FromSeconds(seconds);
        if (span.TotalDays >= 1)
            return $"{(int)span.TotalDays}d {span.Hours}h";
        if (span.TotalHours >= 1)
            return $"{(int)span.TotalHours}h {span.Minutes}m";
        return $"{Math.Max(0, (int)span.TotalMinutes)}m";
    }

    /// <summary>
    ///     A grouped query row. Member initialised so LinqToDB can order by <see cref="Value" /> before materialising.
    /// </summary>
    private sealed class Ranked
    {
        public ulong Key { get; init; }
        public long Value { get; init; }

        public TopEntry ToEntry()
        {
            return new TopEntry(Key, Value);
        }
    }

    #region Summaries

    /// <summary>
    ///     Builds the guild wide summary for a window.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="lookbackDays">The window in days, or 0 for all time.</param>
    /// <returns>The overview.</returns>
    public async Task<ServerOverview> GetOverviewAsync(IGuild guild, int lookbackDays)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        long messages;
        int messageContributors;
        TopEntry? topMessageUser;
        TopEntry? topMessageChannel;
        long voiceSeconds;
        int voiceContributors;
        TopEntry? topVoiceUser;
        TopEntry? topVoiceChannel;

        if (lookbackDays > 0)
        {
            var ts = Timestamps(db, guild.Id, lookbackDays);
            messages = await ts.CountAsync();
            messageContributors = await ts.Select(x => x.UserId).Distinct().CountAsync();
            topMessageUser = await TopAsync(ts.GroupBy(x => x.UserId)
                .Select(g => new Ranked
                {
                    Key = g.Key, Value = g.Count()
                }));
            topMessageChannel = await TopAsync(ts.GroupBy(x => x.ChannelId)
                .Select(g => new Ranked
                {
                    Key = g.Key, Value = g.Count()
                }));

            var seg = Segments(db, guild.Id, lookbackDays);
            voiceSeconds = await seg.SumAsync(x => (long?)x.Seconds) ?? 0;
            voiceContributors = await seg.Select(x => x.UserId).Distinct().CountAsync();
            topVoiceUser = await TopAsync(seg.GroupBy(x => x.UserId)
                .Select(g => new Ranked
                {
                    Key = g.Key, Value = g.Sum(x => (long)x.Seconds)
                }));
            topVoiceChannel = await TopAsync(seg.GroupBy(x => x.ChannelId)
                .Select(g => new Ranked
                {
                    Key = g.Key, Value = g.Sum(x => (long)x.Seconds)
                }));
        }
        else
        {
            var counts = Counts(db, guild.Id);
            messages = (long)(await counts.SumAsync(x => (decimal?)x.Count) ?? 0);
            messageContributors = await counts.Select(x => x.UserId).Distinct().CountAsync();
            topMessageUser = await TopAsync(counts.GroupBy(x => x.UserId)
                .Select(g => new Ranked
                {
                    Key = g.Key, Value = (long)g.Sum(x => (decimal)x.Count)
                }));
            topMessageChannel = await TopAsync(counts.GroupBy(x => x.ChannelId)
                .Select(g => new Ranked
                {
                    Key = g.Key, Value = (long)g.Sum(x => (decimal)x.Count)
                }));

            var totals = Totals(db, guild.Id);
            voiceSeconds = await totals.SumAsync(x => (long?)x.Seconds) ?? 0;
            voiceContributors = await totals.Select(x => x.UserId).Distinct().CountAsync();
            topVoiceUser = await TopAsync(totals.GroupBy(x => x.UserId)
                .Select(g => new Ranked
                {
                    Key = g.Key, Value = g.Sum(x => x.Seconds)
                }));
            topVoiceChannel = await TopAsync(totals.GroupBy(x => x.ChannelId)
                .Select(g => new Ranked
                {
                    Key = g.Key, Value = g.Sum(x => x.Seconds)
                }));
        }

        var (joins, leaves) = await joinLeave.CountSinceAsync(guild.Id,
            lookbackDays > 0 ? Since(lookbackDays) : DateTime.MinValue);

        return new ServerOverview(lookbackDays, messages, voiceSeconds, messageContributors, voiceContributors,
            joins, leaves, topMessageUser, topVoiceUser, topMessageChannel, topVoiceChannel);
    }

    /// <summary>
    ///     Builds one member's activity summary for a window.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="userId">The member.</param>
    /// <param name="lookbackDays">The window in days, or 0 for all time.</param>
    /// <returns>The summary.</returns>
    public async Task<UserActivity> GetUserActivityAsync(IGuild guild, ulong userId, int lookbackDays)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var allTimeMessages = (long)(await Counts(db, guild.Id).Where(x => x.UserId == userId)
            .SumAsync(x => (decimal?)x.Count) ?? 0);
        var allTimeVoice = await Totals(db, guild.Id).Where(x => x.UserId == userId)
            .SumAsync(x => (long?)x.Seconds) ?? 0;

        long messages;
        long voiceSeconds;
        List<TopEntry> topMessageChannels;
        List<TopEntry> topVoiceChannels;

        if (lookbackDays > 0)
        {
            var ts = Timestamps(db, guild.Id, lookbackDays).Where(x => x.UserId == userId);
            messages = await ts.CountAsync();
            topMessageChannels = await TopListAsync(ts.GroupBy(x => x.ChannelId)
                .Select(g => new Ranked
                {
                    Key = g.Key, Value = g.Count()
                }), 5);

            var seg = Segments(db, guild.Id, lookbackDays).Where(x => x.UserId == userId);
            voiceSeconds = await seg.SumAsync(x => (long?)x.Seconds) ?? 0;
            topVoiceChannels = await TopListAsync(seg.GroupBy(x => x.ChannelId)
                .Select(g => new Ranked
                {
                    Key = g.Key, Value = g.Sum(x => (long)x.Seconds)
                }), 5);
        }
        else
        {
            messages = allTimeMessages;
            voiceSeconds = allTimeVoice;
            topMessageChannels = await Counts(db, guild.Id).Where(x => x.UserId == userId)
                .OrderByDescending(x => x.Count).Take(5)
                .Select(x => new TopEntry(x.ChannelId, (long)x.Count)).ToListAsync();
            topVoiceChannels = await Totals(db, guild.Id).Where(x => x.UserId == userId)
                .OrderByDescending(x => x.Seconds).Take(5)
                .Select(x => new TopEntry(x.ChannelId, x.Seconds)).ToListAsync();
        }

        voiceSeconds += voice.GetOpenSessionSeconds(guild.Id, userId);

        var messageRank = messages > 0 ? await RankAsync(guild.Id, StatKind.Messages, lookbackDays, messages) : null;
        var voiceRank = voiceSeconds > 0 ? await RankAsync(guild.Id, StatKind.Voice, lookbackDays, voiceSeconds) : null;

        return new UserActivity(userId, lookbackDays, messages, voiceSeconds, messageRank, voiceRank,
            topMessageChannels, topVoiceChannels, allTimeMessages, allTimeVoice);
    }

    private async Task<int?> RankAsync(ulong guildId, StatKind kind, int lookbackDays, long value)
    {
        var totals = await GetPerUserTotalsAsync(guildId, kind, lookbackDays);
        return totals.Values.Count(v => v > value) + 1;
    }

    /// <summary>
    ///     Builds one channel's activity summary for a window.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="channelId">The channel.</param>
    /// <param name="lookbackDays">The window in days, or 0 for all time.</param>
    /// <returns>The summary.</returns>
    public async Task<ChannelActivity> GetChannelActivityAsync(IGuild guild, ulong channelId, int lookbackDays)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        long messages;
        long voiceSeconds;
        int contributors;
        List<TopEntry> topMessageUsers;
        List<TopEntry> topVoiceUsers;

        if (lookbackDays > 0)
        {
            var ts = Timestamps(db, guild.Id, lookbackDays).Where(x => x.ChannelId == channelId);
            messages = await ts.CountAsync();
            var messageUsers = await ts.Select(x => x.UserId).Distinct().ToListAsync();
            topMessageUsers = await TopListAsync(ts.GroupBy(x => x.UserId)
                .Select(g => new Ranked
                {
                    Key = g.Key, Value = g.Count()
                }), 5);

            var seg = Segments(db, guild.Id, lookbackDays).Where(x => x.ChannelId == channelId);
            voiceSeconds = await seg.SumAsync(x => (long?)x.Seconds) ?? 0;
            var voiceUsers = await seg.Select(x => x.UserId).Distinct().ToListAsync();
            topVoiceUsers = await TopListAsync(seg.GroupBy(x => x.UserId)
                .Select(g => new Ranked
                {
                    Key = g.Key, Value = g.Sum(x => (long)x.Seconds)
                }), 5);

            contributors = messageUsers.Union(voiceUsers).Count();
        }
        else
        {
            var counts = Counts(db, guild.Id).Where(x => x.ChannelId == channelId);
            messages = (long)(await counts.SumAsync(x => (decimal?)x.Count) ?? 0);
            var messageUsers = await counts.Select(x => x.UserId).Distinct().ToListAsync();
            topMessageUsers = await counts.OrderByDescending(x => x.Count).Take(5)
                .Select(x => new TopEntry(x.UserId, (long)x.Count)).ToListAsync();

            var totals = Totals(db, guild.Id).Where(x => x.ChannelId == channelId);
            voiceSeconds = await totals.SumAsync(x => (long?)x.Seconds) ?? 0;
            var voiceUsers = await totals.Select(x => x.UserId).Distinct().ToListAsync();
            topVoiceUsers = await totals.OrderByDescending(x => x.Seconds).Take(5)
                .Select(x => new TopEntry(x.UserId, x.Seconds)).ToListAsync();

            contributors = messageUsers.Union(voiceUsers).Count();
        }

        return new ChannelActivity(channelId, lookbackDays, messages, voiceSeconds, contributors, topMessageUsers,
            topVoiceUsers);
    }

    #endregion

    #region Rankings

    /// <summary>
    ///     Gets every member's total for a window, keyed by user.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="kind">Messages or voice seconds.</param>
    /// <param name="lookbackDays">The window in days, or 0 for all time.</param>
    /// <param name="channelFilter">Only count activity in these channels, or null for all.</param>
    /// <returns>Totals by user.</returns>
    public async Task<Dictionary<ulong, long>> GetPerUserTotalsAsync(ulong guildId, StatKind kind, int lookbackDays,
        IReadOnlyCollection<ulong>? channelFilter = null)
    {
        if (kind == StatKind.Activity)
            return await GetPerUserActivitySecondsAsync(guildId, null, lookbackDays);

        await using var db = await dbFactory.CreateConnectionAsync();
        var channels = channelFilter?.ToList();

        if (kind == StatKind.Messages)
        {
            if (lookbackDays > 0)
            {
                var ts = Timestamps(db, guildId, lookbackDays);
                if (channels is { Count: > 0 })
                    ts = ts.Where(x => channels.Contains(x.ChannelId));
                return (await ts.GroupBy(x => x.UserId)
                        .Select(g => new
                        {
                            g.Key, Value = (long)g.Count()
                        })
                        .ToListAsync())
                    .ToDictionary(x => x.Key, x => x.Value);
            }

            var counts = Counts(db, guildId);
            if (channels is { Count: > 0 })
                counts = counts.Where(x => channels.Contains(x.ChannelId));
            return (await counts.GroupBy(x => x.UserId)
                    .Select(g => new
                    {
                        g.Key, Value = g.Sum(x => (decimal)x.Count)
                    })
                    .ToListAsync())
                .ToDictionary(x => x.Key, x => (long)x.Value);
        }

        if (lookbackDays > 0)
        {
            var seg = Segments(db, guildId, lookbackDays);
            if (channels is { Count: > 0 })
                seg = seg.Where(x => channels.Contains(x.ChannelId));
            return (await seg.GroupBy(x => x.UserId)
                    .Select(g => new
                    {
                        g.Key, Value = g.Sum(x => (long)x.Seconds)
                    })
                    .ToListAsync())
                .ToDictionary(x => x.Key, x => x.Value);
        }

        var totals = Totals(db, guildId);
        if (channels is { Count: > 0 })
            totals = totals.Where(x => channels.Contains(x.ChannelId));
        return (await totals.GroupBy(x => x.UserId)
                .Select(g => new
                {
                    g.Key, Value = g.Sum(x => x.Seconds)
                })
                .ToListAsync())
            .ToDictionary(x => x.Key, x => x.Value);
    }

    /// <summary>
    ///     Gets each member's per day totals inside a window, for streak style conditions.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="kind">Messages or voice seconds.</param>
    /// <param name="lookbackDays">The window in days; all time is not supported here.</param>
    /// <param name="channelFilter">Only count activity in these channels, or null for all.</param>
    /// <returns>For each user, the total per UTC day.</returns>
    public async Task<Dictionary<ulong, Dictionary<DateTime, long>>> GetPerUserDailyTotalsAsync(ulong guildId,
        StatKind kind, int lookbackDays, IReadOnlyCollection<ulong>? channelFilter = null)
    {
        lookbackDays = Math.Clamp(lookbackDays, 1, MaxLookbackDays);
        await using var db = await dbFactory.CreateConnectionAsync();
        var channels = channelFilter?.ToList();

        List<(ulong UserId, DateTime Day, long Value)> rows;
        if (kind == StatKind.Messages)
        {
            var ts = Timestamps(db, guildId, lookbackDays);
            if (channels is { Count: > 0 })
                ts = ts.Where(x => channels.Contains(x.ChannelId));
            rows = (await ts.GroupBy(x => new
                    {
                        x.UserId, x.Timestamp.Date
                    })
                    .Select(g => new
                    {
                        g.Key.UserId, g.Key.Date, Value = (long)g.Count()
                    })
                    .ToListAsync())
                .Select(x => (x.UserId, x.Date, x.Value)).ToList();
        }
        else
        {
            var seg = Segments(db, guildId, lookbackDays);
            if (channels is { Count: > 0 })
                seg = seg.Where(x => channels.Contains(x.ChannelId));
            rows = (await seg.GroupBy(x => new
                    {
                        x.UserId, x.EndedAt.Date
                    })
                    .Select(g => new
                    {
                        g.Key.UserId, g.Key.Date, Value = g.Sum(x => (long)x.Seconds)
                    })
                    .ToListAsync())
                .Select(x => (x.UserId, x.Date, x.Value)).ToList();
        }

        return rows.GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.Day, x => x.Value));
    }

    /// <summary>
    ///     Ranks members for a window.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="kind">Messages or voice seconds.</param>
    /// <param name="lookbackDays">The window in days, or 0 for all time.</param>
    /// <param name="limit">Maximum entries.</param>
    /// <param name="channelId">Only count activity in this channel, or null for all.</param>
    /// <returns>The ranking, best first.</returns>
    public async Task<List<TopEntry>> GetTopUsersAsync(ulong guildId, StatKind kind, int lookbackDays, int limit,
        ulong? channelId = null)
    {
        var totals = await GetPerUserTotalsAsync(guildId, kind, lookbackDays,
            channelId.HasValue ? [channelId.Value] : null);
        return totals.OrderByDescending(x => x.Value).Take(limit)
            .Select(x => new TopEntry(x.Key, x.Value)).ToList();
    }

    /// <summary>
    ///     Ranks channels for a window.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="kind">Messages or voice seconds.</param>
    /// <param name="lookbackDays">The window in days, or 0 for all time.</param>
    /// <param name="limit">Maximum entries.</param>
    /// <returns>The ranking, best first.</returns>
    public async Task<List<TopEntry>> GetTopChannelsAsync(ulong guildId, StatKind kind, int lookbackDays, int limit)
    {
        if (kind == StatKind.Activity)
            return [];

        await using var db = await dbFactory.CreateConnectionAsync();

        if (kind == StatKind.Messages)
        {
            if (lookbackDays > 0)
            {
                return await TopListAsync(Timestamps(db, guildId, lookbackDays).GroupBy(x => x.ChannelId)
                    .Select(g => new Ranked
                    {
                        Key = g.Key, Value = g.Count()
                    }), limit);
            }

            return await TopListAsync(Counts(db, guildId).GroupBy(x => x.ChannelId)
                .Select(g => new Ranked
                {
                    Key = g.Key, Value = (long)g.Sum(x => (decimal)x.Count)
                }), limit);
        }

        if (lookbackDays > 0)
        {
            return await TopListAsync(Segments(db, guildId, lookbackDays).GroupBy(x => x.ChannelId)
                .Select(g => new Ranked
                {
                    Key = g.Key, Value = g.Sum(x => (long)x.Seconds)
                }), limit);
        }

        return await TopListAsync(Totals(db, guildId).GroupBy(x => x.ChannelId)
            .Select(g => new Ranked
            {
                Key = g.Key, Value = g.Sum(x => x.Seconds)
            }), limit);
    }

    #endregion

    #region Activities

    /// <summary>
    ///     Ranks games and apps by time spent across members.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="lookbackDays">The window in days, or 0 for all time.</param>
    /// <param name="limit">Maximum entries.</param>
    /// <param name="userId">Only this member's activities, or null for everyone.</param>
    /// <returns>The ranking, most played first.</returns>
    public async Task<List<ActivitySummary>> GetTopActivitiesAsync(ulong guildId, int lookbackDays, int limit,
        ulong? userId = null)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        List<(string Name, int Type, long Seconds, int Players, ulong? AppId)> rows;
        if (lookbackDays > 0)
        {
            var seg = ActivitySegments(db, guildId, lookbackDays);
            if (userId.HasValue) seg = seg.Where(x => x.UserId == userId.Value);
            rows = (await seg.GroupBy(x => new
                    {
                        x.Name, x.Type
                    })
                    .Select(g => new
                    {
                        g.Key.Name,
                        g.Key.Type,
                        Seconds = g.Sum(x => (long)x.Seconds),
                        Players = g.Select(x => x.UserId).Distinct().Count(),
                        AppId = g.Max(x => x.ApplicationId)
                    })
                    .OrderByDescending(x => x.Seconds)
                    .Take(limit)
                    .ToListAsync())
                .Select(x => (x.Name, x.Type, x.Seconds, x.Players, x.AppId)).ToList();
        }
        else
        {
            var totals = ActivityTotals(db, guildId);
            if (userId.HasValue) totals = totals.Where(x => x.UserId == userId.Value);
            rows = (await totals.GroupBy(x => new
                    {
                        x.Name, x.Type
                    })
                    .Select(g => new
                    {
                        g.Key.Name,
                        g.Key.Type,
                        Seconds = g.Sum(x => x.Seconds),
                        Players = g.Select(x => x.UserId).Distinct().Count(),
                        AppId = g.Max(x => x.ApplicationId)
                    })
                    .OrderByDescending(x => x.Seconds)
                    .Take(limit)
                    .ToListAsync())
                .Select(x => (x.Name, x.Type, x.Seconds, x.Players, x.AppId)).ToList();
        }

        if (userId.HasValue)
        {
            var open = activities.GetOpenSessionSeconds(guildId, userId.Value);
            rows = rows.Select(r => open.TryGetValue(r.Name, out var extra)
                ? (r.Name, r.Type, r.Seconds + extra, r.Players, r.AppId)
                : r).ToList();
        }

        return rows.Select(r => new ActivitySummary(r.Name, r.AppId, (ActivityType)r.Type, r.Seconds, r.Players))
            .ToList();
    }

    /// <summary>
    ///     Gets each member's seconds in one activity, or in any activity, for a window.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="name">The activity name, or null for any activity.</param>
    /// <param name="lookbackDays">The window in days, or 0 for all time.</param>
    /// <returns>Seconds by user.</returns>
    public async Task<Dictionary<ulong, long>> GetPerUserActivitySecondsAsync(ulong guildId, string? name,
        int lookbackDays)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        if (lookbackDays > 0)
        {
            return (await ActivitySegments(db, guildId, lookbackDays, name)
                    .GroupBy(x => x.UserId)
                    .Select(g => new
                    {
                        g.Key, Value = g.Sum(x => (long)x.Seconds)
                    })
                    .ToListAsync())
                .ToDictionary(x => x.Key, x => x.Value);
        }

        return (await ActivityTotals(db, guildId, name)
                .GroupBy(x => x.UserId)
                .Select(g => new
                {
                    g.Key, Value = g.Sum(x => x.Seconds)
                })
                .ToListAsync())
            .ToDictionary(x => x.Key, x => x.Value);
    }

    /// <summary>
    ///     Gets each member's per day seconds in one activity, or in any activity, for streak conditions.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="name">The activity name, or null for any activity.</param>
    /// <param name="lookbackDays">The window in days, 1 to 90.</param>
    /// <returns>For each user, seconds per UTC day.</returns>
    public async Task<Dictionary<ulong, Dictionary<DateTime, long>>> GetPerUserDailyActivitySecondsAsync(
        ulong guildId, string? name, int lookbackDays)
    {
        lookbackDays = Math.Clamp(lookbackDays, 1, MaxLookbackDays);
        await using var db = await dbFactory.CreateConnectionAsync();
        var rows = await ActivitySegments(db, guildId, lookbackDays, name)
            .GroupBy(x => new
            {
                x.UserId, x.EndedAt.Date
            })
            .Select(g => new
            {
                g.Key.UserId, g.Key.Date, Value = g.Sum(x => (long)x.Seconds)
            })
            .ToListAsync();

        return rows.GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.Date, x => x.Value));
    }

    /// <summary>
    ///     Sums activity seconds since a point in time, for counters.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="since">The start of the window.</param>
    /// <returns>The seconds.</returns>
    public async Task<long> CountActivitySecondsSinceAsync(ulong guildId, DateTime since)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var users = settings.GetExcludedUsers(guildId).ToList();
        var query = db.ActivitySegments.Where(x => x.GuildId == guildId && x.EndedAt >= since);
        if (users.Count > 0) query = query.Where(x => !users.Contains(x.UserId));
        return await query.SumAsync(x => (long?)x.Seconds) ?? 0;
    }

    /// <summary>
    ///     Counts members currently in a tracked activity.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="name">An activity name, or null for any.</param>
    /// <returns>The member count.</returns>
    public int CountActiveNow(ulong guildId, string? name = null)
    {
        return activities.CountActiveNow(guildId, name);
    }

    #endregion

    #region Series

    /// <summary>
    ///     Messages per bucket over a window. Windows of two days or less use hourly buckets, otherwise daily.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="lookbackDays">The window in days, 1 to 90.</param>
    /// <param name="userId">Only this member, or null.</param>
    /// <param name="channelId">Only this channel, or null.</param>
    /// <returns>The series, oldest first, with empty buckets filled in.</returns>
    public async Task<List<SeriesPoint>> GetMessageSeriesAsync(ulong guildId, int lookbackDays, ulong? userId = null,
        ulong? channelId = null)
    {
        lookbackDays = Math.Clamp(lookbackDays, 1, MaxLookbackDays);
        await using var db = await dbFactory.CreateConnectionAsync();
        var ts = Timestamps(db, guildId, lookbackDays);
        if (userId.HasValue) ts = ts.Where(x => x.UserId == userId.Value);
        if (channelId.HasValue) ts = ts.Where(x => x.ChannelId == channelId.Value);

        if (lookbackDays <= 2)
        {
            var hourly = await ts.GroupBy(x => new
                {
                    x.Timestamp.Date, x.Timestamp.Hour
                })
                .Select(g => new
                {
                    g.Key.Date, g.Key.Hour, Value = (double)g.Count()
                })
                .ToListAsync();
            return FillHourly(lookbackDays, hourly.ToDictionary(x => x.Date.AddHours(x.Hour), x => x.Value));
        }

        var daily = await ts.GroupBy(x => x.Timestamp.Date)
            .Select(g => new
            {
                Day = g.Key, Value = (double)g.Count()
            })
            .ToListAsync();
        return FillDaily(lookbackDays, daily.ToDictionary(x => x.Day, x => x.Value));
    }

    /// <summary>
    ///     Voice hours per bucket over a window.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="lookbackDays">The window in days, 1 to 90.</param>
    /// <param name="userId">Only this member, or null.</param>
    /// <param name="channelId">Only this channel, or null.</param>
    /// <returns>The series in hours, oldest first, with empty buckets filled in.</returns>
    public async Task<List<SeriesPoint>> GetVoiceSeriesAsync(ulong guildId, int lookbackDays, ulong? userId = null,
        ulong? channelId = null)
    {
        lookbackDays = Math.Clamp(lookbackDays, 1, MaxLookbackDays);
        await using var db = await dbFactory.CreateConnectionAsync();
        var seg = Segments(db, guildId, lookbackDays);
        if (userId.HasValue) seg = seg.Where(x => x.UserId == userId.Value);
        if (channelId.HasValue) seg = seg.Where(x => x.ChannelId == channelId.Value);

        if (lookbackDays <= 2)
        {
            var hourly = await seg.GroupBy(x => new
                {
                    x.EndedAt.Date, x.EndedAt.Hour
                })
                .Select(g => new
                {
                    g.Key.Date, g.Key.Hour, Value = g.Sum(x => (long)x.Seconds) / 3600.0
                })
                .ToListAsync();
            return FillHourly(lookbackDays, hourly.ToDictionary(x => x.Date.AddHours(x.Hour), x => x.Value));
        }

        var daily = await seg.GroupBy(x => x.EndedAt.Date)
            .Select(g => new
            {
                Day = g.Key, Value = g.Sum(x => (long)x.Seconds) / 3600.0
            })
            .ToListAsync();
        return FillDaily(lookbackDays, daily.ToDictionary(x => x.Day, x => x.Value));
    }

    /// <summary>
    ///     Gets guild snapshots for a window, thinned to one per day for windows over two weeks.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="lookbackDays">The window in days, 1 to 90.</param>
    /// <returns>Snapshots, oldest first.</returns>
    public async Task<List<GuildSnapshot>> GetSnapshotSeriesAsync(ulong guildId, int lookbackDays)
    {
        lookbackDays = Math.Clamp(lookbackDays, 1, MaxLookbackDays);
        await using var db = await dbFactory.CreateConnectionAsync();
        var since = Since(lookbackDays);
        var rows = await db.GuildSnapshots
            .Where(x => x.GuildId == guildId && x.Timestamp >= since)
            .OrderBy(x => x.Timestamp)
            .ToListAsync();

        if (lookbackDays <= 14)
            return rows;

        return rows.GroupBy(x => x.Timestamp.Date)
            .Select(g => g.OrderBy(x => Math.Abs(x.Timestamp.Hour - 12)).First())
            .OrderBy(x => x.Timestamp)
            .ToList();
    }

    /// <summary>
    ///     Joins and leaves per day over a window.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="lookbackDays">The window in days, 1 to 90.</param>
    /// <returns>Join and leave series, oldest first.</returns>
    public async Task<(List<SeriesPoint> Joins, List<SeriesPoint> Leaves)> GetJoinLeaveSeriesAsync(ulong guildId,
        int lookbackDays)
    {
        lookbackDays = Math.Clamp(lookbackDays, 1, MaxLookbackDays);
        var joins = await joinLeave.GetGroupedJoinLeaveDataAsync(guildId, true, lookbackDays);
        var leaves = await joinLeave.GetGroupedJoinLeaveDataAsync(guildId, false, lookbackDays);
        return (joins.Select(x => new SeriesPoint(x.Date, x.Count)).ToList(),
            leaves.Select(x => new SeriesPoint(x.Date, x.Count)).ToList());
    }

    private static List<SeriesPoint> FillDaily(int days, Dictionary<DateTime, double> values)
    {
        var start = DateTime.UtcNow.Date.AddDays(-(days - 1));
        return Enumerable.Range(0, days)
            .Select(i => start.AddDays(i))
            .Select(day => new SeriesPoint(day, values.GetValueOrDefault(day)))
            .ToList();
    }

    private static List<SeriesPoint> FillHourly(int days, Dictionary<DateTime, double> values)
    {
        var now = DateTime.UtcNow;
        var end = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);
        var hours = days * 24;
        var start = end.AddHours(-(hours - 1));
        return Enumerable.Range(0, hours)
            .Select(i => start.AddHours(i))
            .Select(hour => new SeriesPoint(hour, values.GetValueOrDefault(hour)))
            .ToList();
    }

    #endregion

    #region Quick counts

    /// <summary>
    ///     Counts messages since a point in time, for counters.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="since">The start of the window.</param>
    /// <returns>The count.</returns>
    public async Task<long> CountMessagesSinceAsync(ulong guildId, DateTime since)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var (channels, users) = Exclusions(guildId);
        var query = db.MessageTimestamps.Where(x => x.GuildId == guildId && x.Timestamp >= since);
        if (channels.Count > 0) query = query.Where(x => !channels.Contains(x.ChannelId));
        if (users.Count > 0) query = query.Where(x => !users.Contains(x.UserId));
        return await query.CountAsync();
    }

    /// <summary>
    ///     Sums voice seconds since a point in time, for counters.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="since">The start of the window.</param>
    /// <returns>The seconds.</returns>
    public async Task<long> CountVoiceSecondsSinceAsync(ulong guildId, DateTime since)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var mask = settings.GetCachedSettings(guildId).VoiceStates;
        var (channels, users) = Exclusions(guildId);
        var query = db.VoiceSegments.Where(x => x.GuildId == guildId && x.EndedAt >= since);
        if (mask != 0) query = query.Where(x => (x.State & mask) == 0);
        if (channels.Count > 0) query = query.Where(x => !channels.Contains(x.ChannelId));
        if (users.Count > 0) query = query.Where(x => !users.Contains(x.UserId));
        return await query.SumAsync(x => (long?)x.Seconds) ?? 0;
    }

    #endregion
}