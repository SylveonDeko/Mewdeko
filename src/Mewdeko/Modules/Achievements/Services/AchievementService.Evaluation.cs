using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Database.Enums;
using Mewdeko.Modules.Achievements.Common;
using Mewdeko.Modules.Currency.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Modules.Achievements.Services;

public sealed partial class AchievementService
{
    /// <summary>
    ///     How often changed members are checked.
    /// </summary>
    private static readonly TimeSpan EvaluateInterval = TimeSpan.FromSeconds(20);

    /// <summary>
    ///     How often the sweep timer looks for work.
    /// </summary>
    private static readonly TimeSpan SweepTick = TimeSpan.FromMinutes(1);

    /// <summary>
    ///     How long a server goes between full sweeps.
    /// </summary>
    private static readonly TimeSpan SweepEvery = TimeSpan.FromHours(6);

    /// <summary>
    ///     Longest one sweep tick keeps working.
    /// </summary>
    private static readonly TimeSpan SweepBudget = TimeSpan.FromSeconds(50);

    /// <summary>
    ///     How long cached message counts are trusted before being read again.
    /// </summary>
    private static readonly TimeSpan TallyFresh = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     Members checked per batch.
    /// </summary>
    private const int BatchSize = 250;

    private readonly ConcurrentDictionary<(ulong GuildId, ulong UserId), AchievementDirty> dirty = new();
    private readonly ConcurrentDictionary<(ulong GuildId, ulong UserId), ulong> lastChannels = new();
    private readonly ConcurrentDictionary<(ulong GuildId, ulong UserId), MessageTally> tallies = new();
    private readonly ConcurrentDictionary<ulong, DateTime> lastSweeps = new();
    private readonly ConcurrentDictionary<ulong, bool> pendingSweeps = new();
    private readonly SemaphoreSlim evaluateLock = new(1, 1);
    private readonly SemaphoreSlim sweepLock = new(1, 1);
    private Timer? evaluateTimer;
    private Timer? sweepTimer;
    private Timer? trimTimer;

    private void StartTimers()
    {
        evaluateTimer = new Timer(_ => _ = EvaluateTickAsync(), null, EvaluateInterval, EvaluateInterval);
        sweepTimer = new Timer(_ => _ = SweepTickAsync(), null, SweepTick, SweepTick);
        trimTimer = new Timer(_ => Trim(), null, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
    }

    private void StopTimers()
    {
        evaluateTimer?.Dispose();
        sweepTimer?.Dispose();
        trimTimer?.Dispose();
    }

    /// <summary>
    ///     Asks for every member of a server to be checked soon.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="silent">Whether unlocks found by this sweep skip announcements.</param>
    public void QueueSweep(ulong guildId, bool silent = true)
    {
        pendingSweeps.AddOrUpdate(guildId, silent, (_, existing) => existing && silent);
    }

    #region Unlock cache

    private SemaphoreSlim MemberLock(ulong guildId, ulong userId)
    {
        var hash = HashCode.Combine(guildId, userId) & int.MaxValue;
        return memberLocks[hash % MemberLockStripes];
    }

    private async Task<UnlockState> GetUnlockStateAsync(ulong guildId, ulong userId)
    {
        if (unlockCache.TryGetValue((guildId, userId), out var cached))
        {
            cached.LastUsed = DateTime.UtcNow;
            return cached;
        }

        await using var db = await dbFactory.CreateConnectionAsync();
        var rows = await db.UserAchievements
            .Where(x => x.GuildId == guildId && x.UserId == userId)
            .Select(x => new
            {
                x.AchievementKey, x.UnlockedAt
            })
            .ToListAsync();
        var points = await db.AchievementMembers
            .Where(x => x.GuildId == guildId && x.UserId == userId)
            .Select(x => (int?)x.Points)
            .FirstOrDefaultAsync() ?? 0;

        var state = new UnlockState(rows.ToDictionary(r => r.AchievementKey, r => r.UnlockedAt, StringComparer.Ordinal),
            points);
        return unlockCache.GetOrAdd((guildId, userId), state);
    }

    private async Task PreloadUnlockStatesAsync(ulong guildId, IReadOnlyCollection<ulong> userIds)
    {
        var missing = userIds.Where(u => !unlockCache.ContainsKey((guildId, u))).ToList();
        if (missing.Count == 0)
            return;

        await using var db = await dbFactory.CreateConnectionAsync();
        var rows = await db.UserAchievements
            .Where(x => x.GuildId == guildId && missing.Contains(x.UserId))
            .Select(x => new
            {
                x.UserId, x.AchievementKey, x.UnlockedAt
            })
            .ToListAsync();
        var points = await db.AchievementMembers
            .Where(x => x.GuildId == guildId && missing.Contains(x.UserId))
            .Select(x => new
            {
                x.UserId, x.Points
            })
            .ToDictionaryAsync(x => x.UserId, x => x.Points);

        var byUser = rows.GroupBy(r => r.UserId).ToDictionary(g => g.Key);
        foreach (var userId in missing)
        {
            var keys = byUser.TryGetValue(userId, out var group)
                ? group.ToDictionary(r => r.AchievementKey, r => r.UnlockedAt, StringComparer.Ordinal)
                : new Dictionary<string, DateTime>(StringComparer.Ordinal);
            unlockCache.TryAdd((guildId, userId), new UnlockState(keys, points.GetValueOrDefault(userId)));
        }
    }

    private void DropCachedKey(ulong guildId, string key)
    {
        foreach (var (cacheKey, state) in unlockCache)
        {
            if (cacheKey.GuildId == guildId)
                state.Remove(key);
        }
    }

    private void DropMember(ulong guildId, ulong userId)
    {
        unlockCache.TryRemove((guildId, userId), out _);
    }

    private void Trim()
    {
        try
        {
            var now = DateTime.UtcNow;
            foreach (var (key, state) in unlockCache)
            {
                if (now - state.LastUsed > UnlockCacheIdle)
                    unlockCache.TryRemove(key, out _);
            }

            foreach (var (key, tally) in tallies)
            {
                if (now - tally.QueriedAt > UnlockCacheIdle)
                    tallies.TryRemove(key, out _);
            }

            foreach (var (key, recent) in recentMessages)
            {
                if (now - recent.SentAt > TimeSpan.FromMinutes(1))
                    recentMessages.TryRemove(key, out _);
            }

            foreach (var (key, queue) in messageBursts)
            {
                lock (queue)
                {
                    if (queue.Count == 0 || now - queue.Peek() > TimeSpan.FromMinutes(1))
                        messageBursts.TryRemove(key, out _);
                }
            }

            foreach (var key in lastChannels.Keys.Where(k => !dirty.ContainsKey(k)))
                lastChannels.TryRemove(key, out _);

            if (userTimezones.Count > 50000)
                userTimezones.Clear();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Achievement cache trim failed");
        }
    }

    #endregion

    #region Evaluation

    private async Task EvaluateTickAsync()
    {
        if (!await evaluateLock.WaitAsync(0))
            return;

        try
        {
            await FlushCountersAsync();

            var batch = new List<(ulong GuildId, ulong UserId, AchievementDirty Flags)>();
            foreach (var key in dirty.Keys)
            {
                if (dirty.TryRemove(key, out var flags))
                    batch.Add((key.GuildId, key.UserId, flags));
            }

            foreach (var group in batch.GroupBy(x => x.GuildId))
            {
                var guild = client.GetGuild(group.Key);
                if (guild is null || !IsEnabled(guild.Id))
                    continue;

                foreach (var chunk in group.Chunk(BatchSize))
                {
                    try
                    {
                        await EvaluateMembersAsync(guild, chunk.Select(x => (x.UserId, x.Flags)).ToList(), false);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Achievement evaluation failed for {GuildId}", guild.Id);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Achievement evaluation tick failed");
        }
        finally
        {
            evaluateLock.Release();
        }
    }

    private async Task SweepTickAsync()
    {
        if (!await sweepLock.WaitAsync(0))
            return;

        try
        {
            var started = DateTime.UtcNow;
            while (DateTime.UtcNow - started < SweepBudget)
            {
                ulong guildId;
                bool silent;
                var pending = pendingSweeps.Keys.FirstOrDefault();
                if (pending != 0 && pendingSweeps.TryRemove(pending, out var pendingSilent))
                {
                    guildId = pending;
                    silent = pendingSilent;
                }
                else
                {
                    var due = settingsCache.Values
                        .Where(s => s.Enabled && client.GetGuild(s.Row.GuildId) is not null)
                        .Select(s => s.Row.GuildId)
                        .FirstOrDefault(id => !lastSweeps.TryGetValue(id, out var last) || DateTime.UtcNow - last > SweepEvery);
                    if (due == 0)
                        break;
                    guildId = due;
                    silent = false;
                }

                await SweepGuildAsync(guildId, silent);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Achievement sweep tick failed");
        }
        finally
        {
            sweepLock.Release();
        }
    }

    private async Task SweepGuildAsync(ulong guildId, bool silent)
    {
        lastSweeps[guildId] = DateTime.UtcNow;
        var guild = client.GetGuild(guildId);
        var settings = GetSettings(guildId);
        if (guild is null || !settings.Enabled)
            return;

        var backfill = settings.Row.BackfilledAt is null;
        var quiet = silent || backfill;
        var members = guild.Users.Where(u => !u.IsBot).Select(u => u.Id).ToList();

        await FlushCountersAsync();
        foreach (var chunk in members.Chunk(BatchSize))
        {
            try
            {
                await EvaluateMembersAsync(guild, chunk.Select(u => (u, AchievementDirty.All)).ToList(), quiet);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Achievement sweep failed for a batch in {GuildId}", guildId);
            }
        }

        if (backfill)
            await UpdateSettingsAsync(guildId, row => row.BackfilledAt = DateTime.UtcNow);

        logger.LogInformation("Achievement sweep finished for {GuildId} with {Count} members", guildId, members.Count);
    }

    /// <summary>
    ///     One time feats that a count already proves: anyone with at least one counted message, voice join, or
    ///     reaction did each for a first time, even when it happened before the bot was watching. Sweeps and
    ///     backfills award these from the count.
    /// </summary>
    private static readonly IReadOnlyDictionary<AchievementFeat, AchievementMetric> ImpliedFeats =
        new Dictionary<AchievementFeat, AchievementMetric>
        {
            [AchievementFeat.FirstMessage] = AchievementMetric.MessagesTotal,
            [AchievementFeat.FirstVoice] = AchievementMetric.VoiceJoins,
            [AchievementFeat.FirstReaction] = AchievementMetric.Reactions
        };

    private async Task EvaluateMembersAsync(SocketGuild guild,
        IReadOnlyList<(ulong UserId, AchievementDirty Flags)> batch, bool silent)
    {
        var catalog = await GetCatalogAsync(guild.Id);
        var impliedDefs = catalog.Earnable
            .Where(d => d.Feat is { } feat && (ImpliedFeats.ContainsKey(feat) || feat == AchievementFeat.Boosted))
            .ToList();
        var metricDefs = catalog.Earnable.Where(d => d.IsMetric).Concat(impliedDefs).ToList();
        if (metricDefs.Count == 0)
            return;

        var members = batch
            .Select(x => (Member: guild.GetUser(x.UserId), x.Flags))
            .Where(x => x.Member is not null && CanEarn(x.Member))
            .Select(x => (Member: x.Member!, x.Flags))
            .ToList();
        if (members.Count == 0)
            return;

        var userIds = members.Select(m => m.Member.Id).ToList();
        await PreloadUnlockStatesAsync(guild.Id, userIds);

        var needed = new HashSet<AchievementMetric>();
        var lockedByUser = new Dictionary<ulong, List<AchievementDefinition>>();
        foreach (var (member, flags) in members)
        {
            var state = await GetUnlockStateAsync(guild.Id, member.Id);
            var locked = metricDefs
                .Where(d => !state.Has(d.Key) &&
                            (flags.HasFlag(AchievementDirty.All) ||
                             (AchievementCatalog.GetMetric(MetricOf(d)).Dirty & flags) != 0 ||
                             AchievementCatalog.GetMetric(MetricOf(d)).Dirty == AchievementDirty.None))
                .ToList();
            if (locked.Count == 0)
                continue;
            lockedByUser[member.Id] = locked;
            foreach (var def in locked.Where(d => MetricOf(d) != AchievementMetric.None))
                needed.Add(MetricOf(def));
        }

        if (lockedByUser.Count == 0)
            return;

        var values = await ComputeMetricsAsync(guild, lockedByUser.Keys.ToList(), needed);
        foreach (var (member, _) in members)
        {
            if (!lockedByUser.TryGetValue(member.Id, out var locked) || !values.TryGetValue(member.Id, out var metrics))
                continue;

            var reached = locked
                .Where(d => d.Feat switch
                {
                    AchievementFeat.Boosted => member.PremiumSince is not null,
                    { } feat when ImpliedFeats.TryGetValue(feat, out var metric) =>
                        metrics.TryGetValue(metric, out var count) && count >= 1,
                    _ => metrics.TryGetValue(d.Metric, out var value) && value >= d.Threshold
                })
                .ToList();
            if (reached.Count == 0)
                continue;

            lastChannels.TryGetValue((guild.Id, member.Id), out var channelId);
            await UnlockForMemberAsync(member, reached, silent ? 0 : channelId, silent);
        }
    }

    /// <summary>
    ///     The count an achievement is checked against: its own metric, or the count that proves an implied feat.
    /// </summary>
    private static AchievementMetric MetricOf(AchievementDefinition def)
    {
        return def.Feat is { } feat && ImpliedFeats.TryGetValue(feat, out var metric) ? metric : def.Metric;
    }

    /// <summary>
    ///     Reads metric values for members.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="userIds">The members.</param>
    /// <param name="metrics">Which metrics to read.</param>
    /// <returns>Values by member and metric. Metrics without data are left out.</returns>
    public async Task<Dictionary<ulong, Dictionary<AchievementMetric, long>>> ComputeMetricsAsync(SocketGuild guild,
        IReadOnlyList<ulong> userIds, IReadOnlySet<AchievementMetric> metrics)
    {
        var result = userIds.ToDictionary(u => u, _ => new Dictionary<AchievementMetric, long>());
        if (userIds.Count == 0 || metrics.Count == 0)
            return result;

        var settings = GetSettings(guild.Id);
        var excludedChannels = settings.ExcludedChannels.ToList();
        bool Needs(params AchievementMetric[] any)
        {
            return any.Any(metrics.Contains);
        }

        await using var db = await dbFactory.CreateConnectionAsync();

        if (Needs(AchievementMetric.MessagesTotal, AchievementMetric.MessagesToday, AchievementMetric.MessagesThisWeek,
                AchievementMetric.MessagesThisMonth, AchievementMetric.TextChannels, AchievementMetric.EveryTextChannel) &&
            messageCounts.IsCounting(guild.Id))
        {
            await FillMessageTalliesAsync(db, guild.Id, userIds, excludedChannels);
            foreach (var userId in userIds)
            {
                if (!tallies.TryGetValue((guild.Id, userId), out var tally))
                    continue;
                var values = result[userId];
                lock (tally)
                {
                    values[AchievementMetric.MessagesTotal] = tally.Total;
                    values[AchievementMetric.MessagesToday] = tally.Day;
                    values[AchievementMetric.MessagesThisWeek] = tally.Week;
                    values[AchievementMetric.MessagesThisMonth] = tally.Month;
                    values[AchievementMetric.TextChannels] = tally.Channels.Count;
                    if (metrics.Contains(AchievementMetric.EveryTextChannel) && guild.GetUser(userId) is { } member)
                    {
                        var postable = PostableChannels(guild, member, settings);
                        values[AchievementMetric.EveryTextChannel] =
                            postable.Count > 0 && postable.All(tally.Channels.Contains) ? 1 : 0;
                    }
                }
            }
        }

        if (Needs(AchievementMetric.VoiceHours, AchievementMetric.VoiceChannels, AchievementMetric.EveryVoiceChannel))
        {
            var rows = await db.VoiceTotals
                .Where(x => x.GuildId == guild.Id && userIds.Contains(x.UserId) && !excludedChannels.Contains(x.ChannelId))
                .Select(x => new
                {
                    x.UserId, x.ChannelId, x.Seconds
                })
                .ToListAsync();
            foreach (var group in rows.GroupBy(r => r.UserId))
            {
                var values = result[group.Key];
                values[AchievementMetric.VoiceHours] = group.Sum(r => r.Seconds) / 3600;
                var visited = group.Where(r => r.Seconds > 0).Select(r => r.ChannelId).ToHashSet();
                values[AchievementMetric.VoiceChannels] = visited.Count;
                if (metrics.Contains(AchievementMetric.EveryVoiceChannel) && guild.GetUser(group.Key) is { } member)
                {
                    var joinable = JoinableVoiceChannels(guild, member, settings);
                    values[AchievementMetric.EveryVoiceChannel] =
                        joinable.Count > 0 && joinable.All(visited.Contains) ? 1 : 0;
                }
            }
        }

        if (Needs(AchievementMetric.VoiceJoins, AchievementMetric.MutedHours, AchievementMetric.Unlocked,
                AchievementMetric.Points))
        {
            var rows = await db.AchievementMembers
                .Where(x => x.GuildId == guild.Id && userIds.Contains(x.UserId))
                .ToDictionaryAsync(x => x.UserId);
            var now = DateTime.UtcNow;
            foreach (var userId in userIds)
            {
                rows.TryGetValue(userId, out var row);
                var pending = counterDeltas.TryGetValue((guild.Id, userId), out var delta) ? delta.Peek() : (0, 0);
                var liveMuted = voiceTracks.TryGetValue((guild.Id, userId), out var track) && track.MutedSince is { } since
                    ? (long)(now - since).TotalSeconds
                    : 0;
                var values = result[userId];
                values[AchievementMetric.VoiceJoins] = (row?.VoiceJoins ?? 0) + pending.Item1;
                values[AchievementMetric.MutedHours] = ((row?.MutedSeconds ?? 0) + pending.Item2 + liveMuted) / 3600;
                values[AchievementMetric.Unlocked] = row?.UnlockedCount ?? 0;
                values[AchievementMetric.Points] = row?.Points ?? 0;
            }
        }

        if (Needs(AchievementMetric.Reactions, AchievementMetric.UniqueEmojis, AchievementMetric.SameEmoji))
        {
            var rows = await db.AchievementEmojiUses
                .Where(x => x.GuildId == guild.Id && userIds.Contains(x.UserId))
                .GroupBy(x => x.UserId)
                .Select(g => new EmojiAggregate
                {
                    UserId = g.Key,
                    Total = g.Sum(x => (long)x.Count),
                    Unique = g.Count(),
                    Max = g.Max(x => x.Count)
                })
                .ToListAsync();
            foreach (var row in rows)
            {
                var values = result[row.UserId];
                values[AchievementMetric.Reactions] = row.Total;
                values[AchievementMetric.UniqueEmojis] = row.Unique;
                values[AchievementMetric.SameEmoji] = row.Max;
            }
        }

        if (Needs(AchievementMetric.Invites))
        {
            var rows = await db.InviteCounts
                .Where(x => x.GuildId == guild.Id && userIds.Contains(x.UserId))
                .Select(x => new
                {
                    x.UserId, x.Count
                })
                .ToListAsync();
            foreach (var row in rows)
                result[row.UserId][AchievementMetric.Invites] = row.Count;
        }

        if (Needs(AchievementMetric.Commands))
        {
            var rows = await db.CommandStats
                .Where(x => x.GuildId == guild.Id && userIds.Contains(x.UserId))
                .GroupBy(x => x.UserId)
                .Select(g => new CountAggregate
                {
                    UserId = g.Key,
                    Count = g.LongCount()
                })
                .ToListAsync();
            foreach (var row in rows)
                result[row.UserId][AchievementMetric.Commands] = row.Count;
        }

        if (Needs(AchievementMetric.TenureDays, AchievementMetric.BoostMonths))
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var userId in userIds)
            {
                if (guild.GetUser(userId) is not { } member)
                    continue;
                var values = result[userId];
                if (member.JoinedAt is { } joined)
                    values[AchievementMetric.TenureDays] = (long)(now - joined).TotalDays;
                values[AchievementMetric.BoostMonths] = member.PremiumSince is { } since ? FullMonths(since, now) : -1;
            }
        }

        if (Needs(AchievementMetric.XpLevel))
        {
            var levels = await xpService.GetUserLevelsAsync(guild.Id, userIds);
            foreach (var (userId, level) in levels)
            {
                if (result.TryGetValue(userId, out var values))
                    values[AchievementMetric.XpLevel] = level;
            }
        }

        if (Needs(AchievementMetric.Reputation))
        {
            var rows = await db.UserReputations
                .Where(x => x.GuildId == guild.Id && userIds.Contains(x.UserId))
                .Select(x => new
                {
                    x.UserId, x.TotalRep
                })
                .ToListAsync();
            foreach (var row in rows)
                result[row.UserId][AchievementMetric.Reputation] = row.TotalRep;
        }

        if (Needs(AchievementMetric.NetWorth))
        {
            var currency = services.GetRequiredService<ICurrencyService>();
            foreach (var userId in userIds)
            {
                var (wallet, bank) = await currency.GetBalancesAsync(userId, guild.Id);
                result[userId][AchievementMetric.NetWorth] = wallet + bank;
            }
        }

        return result;
    }

    private static long FullMonths(DateTimeOffset since, DateTimeOffset now)
    {
        var months = (now.Year - since.Year) * 12 + now.Month - since.Month;
        if (now.Day < since.Day)
            months--;
        return Math.Max(0, months);
    }

    /// <summary>
    ///     Text channels a member can post in that count for achievements.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="member">The member.</param>
    /// <param name="settings">The server's settings.</param>
    /// <returns>Channel IDs.</returns>
    private static List<ulong> PostableChannels(SocketGuild guild, SocketGuildUser member, AchievementGuildSettings settings)
    {
        return guild.TextChannels
            .Where(c => c is not SocketThreadChannel && !settings.ExcludedChannels.Contains(c.Id))
            .Where(c =>
            {
                var perms = member.GetPermissions(c);
                return perms.ViewChannel && perms.SendMessages;
            })
            .Select(c => c.Id)
            .ToList();
    }

    /// <summary>
    ///     Voice channels a member can join that count for achievements.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="member">The member.</param>
    /// <param name="settings">The server's settings.</param>
    /// <returns>Channel IDs.</returns>
    private static List<ulong> JoinableVoiceChannels(SocketGuild guild, SocketGuildUser member,
        AchievementGuildSettings settings)
    {
        return guild.VoiceChannels
            .Where(c => c is not SocketStageChannel && c.Id != guild.AFKChannel?.Id &&
                        !settings.ExcludedChannels.Contains(c.Id))
            .Where(c =>
            {
                var perms = member.GetPermissions(c);
                return perms.ViewChannel && perms.Connect;
            })
            .Select(c => c.Id)
            .ToList();
    }

    private void BumpTally(ulong guildId, ulong userId, ulong channelId, DateTime at)
    {
        if (!tallies.TryGetValue((guildId, userId), out var tally))
            return;

        lock (tally)
        {
            var (day, week, month) = PeriodStarts(at);
            if (day != tally.DayStart || week != tally.WeekStart || month != tally.MonthStart)
            {
                tally.QueriedAt = DateTime.MinValue;
                return;
            }

            tally.Total++;
            tally.Day++;
            tally.Week++;
            tally.Month++;
            tally.Channels.Add(channelId);
        }
    }

    private static (DateTime Day, DateTime Week, DateTime Month) PeriodStarts(DateTime at)
    {
        var day = at.Date;
        var week = day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
        var month = new DateTime(day.Year, day.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return (day, week, month);
    }

    private async Task FillMessageTalliesAsync(MewdekoDb db, ulong guildId, IReadOnlyList<ulong> userIds,
        List<ulong> excludedChannels)
    {
        var now = DateTime.UtcNow;
        var (dayStart, weekStart, monthStart) = PeriodStarts(now);
        var stale = userIds
            .Where(u => !tallies.TryGetValue((guildId, u), out var t) ||
                        now - t.QueriedAt > TallyFresh || t.DayStart != dayStart)
            .ToList();
        if (stale.Count == 0)
            return;

        var countRows = await db.MessageCounts
            .Where(x => x.GuildId == guildId && stale.Contains(x.UserId) && !excludedChannels.Contains(x.ChannelId))
            .Select(x => new
            {
                x.UserId, x.ChannelId, x.Count
            })
            .ToListAsync();

        var windowStart = weekStart < monthStart ? weekStart : monthStart;
        var windows = await (
                from t in db.MessageTimestamps
                join c in db.MessageCounts on t.MessageCountId equals c.Id
                where c.GuildId == guildId && stale.Contains(c.UserId) && !excludedChannels.Contains(c.ChannelId) &&
                      t.Timestamp >= windowStart
                group t by c.UserId
                into g
                select new WindowAggregate
                {
                    UserId = g.Key,
                    Day = g.Sum(x => x.Timestamp >= dayStart ? 1L : 0L),
                    Week = g.Sum(x => x.Timestamp >= weekStart ? 1L : 0L),
                    Month = g.Sum(x => x.Timestamp >= monthStart ? 1L : 0L)
                })
            .ToDictionaryAsync(x => x.UserId);

        var byUser = countRows.GroupBy(r => r.UserId).ToDictionary(g => g.Key);
        foreach (var userId in stale)
        {
            var tally = new MessageTally
            {
                DayStart = dayStart,
                WeekStart = weekStart,
                MonthStart = monthStart,
                QueriedAt = now
            };
            if (byUser.TryGetValue(userId, out var rows))
            {
                tally.Total = rows.Sum(r => (long)r.Count);
                foreach (var row in rows.Where(r => r.Count > 0))
                    tally.Channels.Add(row.ChannelId);
            }

            if (windows.TryGetValue(userId, out var window))
            {
                tally.Day = window.Day;
                tally.Week = window.Week;
                tally.Month = window.Month;
            }

            tallies[(guildId, userId)] = tally;
        }
    }

    private async Task FlushCountersAsync()
    {
        try
        {
            var emoji = new List<(ulong GuildId, ulong UserId, string Emoji, int Count)>();
            foreach (var key in emojiDeltas.Keys)
            {
                if (emojiDeltas.TryRemove(key, out var count) && count > 0)
                    emoji.Add((key.GuildId, key.UserId, key.Emoji, count));
            }

            var counters = new List<(ulong GuildId, ulong UserId, long Joins, long Muted)>();
            foreach (var (key, delta) in counterDeltas)
            {
                var (joins, muted) = delta.Take();
                if (joins > 0 || muted > 0)
                    counters.Add((key.GuildId, key.UserId, joins, muted));
                else
                    counterDeltas.TryRemove(key, out _);
            }

            if (emoji.Count == 0 && counters.Count == 0)
                return;

            await using var db = await dbFactory.CreateConnectionAsync();
            foreach (var (guildId, userId, key, count) in emoji)
            {
                await db.AchievementEmojiUses.InsertOrUpdateAsync(
                    () => new AchievementEmojiUse
                    {
                        GuildId = guildId,
                        UserId = userId,
                        Emoji = key,
                        Count = count
                    },
                    x => new AchievementEmojiUse
                    {
                        Count = x.Count + count
                    });
            }

            var now = DateTime.UtcNow;
            foreach (var (guildId, userId, joins, muted) in counters)
            {
                await db.AchievementMembers.InsertOrUpdateAsync(
                    () => new AchievementMember
                    {
                        GuildId = guildId,
                        UserId = userId,
                        VoiceJoins = joins,
                        MutedSeconds = muted,
                        DateAdded = now
                    },
                    x => new AchievementMember
                    {
                        VoiceJoins = x.VoiceJoins + joins,
                        MutedSeconds = x.MutedSeconds + muted
                    });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save achievement counters");
        }
    }

    #endregion

    #region Unlocking

    /// <summary>
    ///     Unlocks achievements for a member, then anything their completion opens up, then global achievements,
    ///     and announces the lot.
    /// </summary>
    /// <param name="user">The member.</param>
    /// <param name="defs">The achievements to unlock. Ones already unlocked are skipped.</param>
    /// <param name="channelId">Where it happened, or 0.</param>
    /// <param name="silent">Whether to skip announcements.</param>
    /// <param name="grantedBy">Who handed them out, for manual grants.</param>
    /// <returns>Everything newly unlocked, including completions and global achievements.</returns>
    public async Task<List<AchievementDefinition>> UnlockForMemberAsync(SocketGuildUser user,
        IReadOnlyList<AchievementDefinition> defs, ulong channelId, bool silent = false, ulong? grantedBy = null)
    {
        var guild = user.Guild;
        var unlocked = new List<AchievementDefinition>();
        var globalUnlocked = new List<AchievementDefinition>();
        var memberLock = MemberLock(guild.Id, user.Id);

        await memberLock.WaitAsync();
        try
        {
            var catalog = await GetCatalogAsync(guild.Id);
            var state = await GetUnlockStateAsync(guild.Id, user.Id);
            var pending = defs.Where(d => !d.IsGlobal && !state.Has(d.Key)).DistinctBy(d => d.Key).ToList();

            while (pending.Count > 0)
            {
                var stored = await StoreUnlocksAsync(guild.Id, user.Id, pending, state, grantedBy);
                unlocked.AddRange(stored);
                pending = FindFollowUps(catalog, state);
            }

            if (unlocked.Count > 0)
                globalUnlocked = await EvaluateGlobalAsync(user.Id);
        }
        finally
        {
            memberLock.Release();
        }

        if (unlocked.Count == 0)
            return unlocked;

        collector.Feature(FeatureKey, guild.Id);
        await GrantRewardsAsync(user, unlocked);

        var all = unlocked.Concat(globalUnlocked).ToList();
        if (!silent)
            await AnnounceAsync(user, all, channelId);
        return all;
    }

    private async Task<List<AchievementDefinition>> StoreUnlocksAsync(ulong guildId, ulong userId,
        IReadOnlyList<AchievementDefinition> defs, UnlockState state, ulong? grantedBy)
    {
        var stored = new List<AchievementDefinition>();
        var now = DateTime.UtcNow;
        await using var db = await dbFactory.CreateConnectionAsync();
        foreach (var def in defs)
        {
            try
            {
                await db.InsertAsync(new UserAchievement
                {
                    GuildId = guildId,
                    UserId = userId,
                    AchievementKey = def.Key,
                    Points = def.Points,
                    GrantedBy = grantedBy,
                    UnlockedAt = now
                });
                stored.Add(def);
                state.Add(def.Key, now, def.Points);
            }
            catch (Exception ex) when (IsDuplicate(ex))
            {
                state.Add(def.Key, now, 0);
            }
        }

        if (stored.Count == 0)
            return stored;

        var points = stored.Sum(d => d.Points);
        var count = stored.Count;
        await db.AchievementMembers.InsertOrUpdateAsync(
            () => new AchievementMember
            {
                GuildId = guildId,
                UserId = userId,
                Points = points,
                UnlockedCount = count,
                LastUnlockAt = now,
                DateAdded = now
            },
            x => new AchievementMember
            {
                Points = x.Points + points,
                UnlockedCount = x.UnlockedCount + count,
                LastUnlockAt = now
            });

        return stored;
    }

    private static bool IsDuplicate(Exception ex)
    {
        return ex.GetType().Name == "PostgresException" && ex.Message.Contains("23505");
    }

    /// <summary>
    ///     Achievements that a member's latest unlocks opened up: finished categories, and server made
    ///     achievements counting unlocks or points.
    /// </summary>
    /// <param name="catalog">The server's catalog.</param>
    /// <param name="state">The member's unlocks.</param>
    /// <returns>Achievements to unlock next.</returns>
    private static List<AchievementDefinition> FindFollowUps(AchievementGuildCatalog catalog, UnlockState state)
    {
        var next = new List<AchievementDefinition>();
        foreach (var def in catalog.Earnable)
        {
            if (state.Has(def.Key))
                continue;

            if (def.Trigger == AchievementTrigger.Completion)
            {
                var targets = def.Key == "prestige_all"
                    ? catalog.Earnable.Where(d => d.Key != def.Key).ToList()
                    : catalog.Earnable.Where(d => d.CategoryKey == def.Keyword &&
                                                  d.Trigger != AchievementTrigger.Completion).ToList();
                if (targets.Count > 0 && targets.All(t => state.Has(t.Key)))
                    next.Add(def);
            }
            else if (def is { Trigger: AchievementTrigger.Metric, Metric: AchievementMetric.Unlocked } &&
                     state.Count >= def.Threshold)
            {
                next.Add(def);
            }
            else if (def is { Trigger: AchievementTrigger.Metric, Metric: AchievementMetric.Points } &&
                     state.Points >= def.Threshold)
            {
                next.Add(def);
            }
        }

        return next;
    }

    private async Task<List<AchievementDefinition>> EvaluateGlobalAsync(ulong userId)
    {
        var unlocked = new List<AchievementDefinition>();
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var rows = await db.AchievementMembers
                .Where(x => x.UserId == userId && x.GuildId != AchievementCatalog.GlobalGuildId)
                .Select(x => new
                {
                    x.Points, x.UnlockedCount
                })
                .ToListAsync();
            var values = new Dictionary<AchievementMetric, long>
            {
                [AchievementMetric.GlobalPoints] = rows.Sum(r => (long)r.Points),
                [AchievementMetric.GlobalServers] = rows.Count(r => r.UnlockedCount > 0),
                [AchievementMetric.GlobalUnlocks] = rows.Sum(r => (long)r.UnlockedCount)
            };

            var state = await GetUnlockStateAsync(AchievementCatalog.GlobalGuildId, userId);
            var reached = AchievementCatalog.BuiltIns
                .Where(d => d.IsGlobal && !state.Has(d.Key) &&
                            values.TryGetValue(d.Metric, out var value) && value >= d.Threshold)
                .ToList();
            if (reached.Count > 0)
                unlocked = await StoreUnlocksAsync(AchievementCatalog.GlobalGuildId, userId, reached, state, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Global achievement evaluation failed for {UserId}", userId);
        }

        return unlocked;
    }

    private async Task GrantRewardsAsync(SocketGuildUser user, IReadOnlyList<AchievementDefinition> unlocked)
    {
        var guild = user.Guild;
        try
        {
            var roles = unlocked
                .Where(d => d.RoleRewardId is > 0 && user.Roles.All(r => r.Id != d.RoleRewardId) &&
                            CanAssign(guild, d.RoleRewardId!.Value))
                .Select(d => d.RoleRewardId!.Value)
                .Distinct()
                .ToList();
            if (roles.Count > 0)
            {
                await user.AddRolesAsync(roles, new RequestOptions
                {
                    AuditLogReason = strings.AchievementAuditReason(guild.Id)
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not give achievement roles in {GuildId}", guild.Id);
        }

        try
        {
            var currency = unlocked.Sum(d => d.CurrencyReward);
            if (currency > 0)
            {
                var service = services.GetRequiredService<ICurrencyService>();
                await service.CreditAsync(user.Id, currency, strings.AchievementCurrencyReason(guild.Id),
                    CurrencyCategory.Achievement, guild.Id, FeatureKey);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not give achievement currency in {GuildId}", guild.Id);
        }

        try
        {
            var settings = GetSettings(guild.Id);
            var xp = unlocked.Sum(d => (long)d.XpReward) + (long)settings.Row.XpPerPoint * unlocked.Sum(d => d.Points);
            if (xp > 0)
                await xpService.AddXpAsync(guild.Id, user.Id, (int)Math.Min(xp, int.MaxValue));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not give achievement XP in {GuildId}", guild.Id);
        }
    }

    /// <summary>
    ///     Hands an achievement to a member, announcing it like any other unlock.
    /// </summary>
    /// <param name="user">The member.</param>
    /// <param name="key">The achievement key.</param>
    /// <param name="grantedBy">Who handed it out.</param>
    /// <returns>The achievement when it was newly unlocked.</returns>
    public async Task<AchievementResult<AchievementDefinition>> GrantAsync(SocketGuildUser user, string key,
        ulong grantedBy)
    {
        var catalog = await GetCatalogAsync(user.Guild.Id);
        if (!catalog.ByKey.TryGetValue(key, out var def) || def.IsGlobal)
            return AchievementResult<AchievementDefinition>.Fail(AchievementError.NotFound);

        var state = await GetUnlockStateAsync(user.Guild.Id, user.Id);
        if (state.Has(key))
            return AchievementResult<AchievementDefinition>.Fail(AchievementError.AlreadyInState);

        await UnlockForMemberAsync(user, [def], 0, false, grantedBy);
        return AchievementResult<AchievementDefinition>.Ok(def);
    }

    /// <summary>
    ///     Takes an achievement away from a member.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <param name="key">The achievement key.</param>
    /// <returns>Whether it was unlocked.</returns>
    public async Task<AchievementResult<string>> RevokeAsync(ulong guildId, ulong userId, string key)
    {
        var memberLock = MemberLock(guildId, userId);
        await memberLock.WaitAsync();
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var row = await db.UserAchievements.FirstOrDefaultAsync(x =>
                x.GuildId == guildId && x.UserId == userId && x.AchievementKey == key);
            if (row is null)
                return AchievementResult<string>.Fail(AchievementError.AlreadyInState);

            await db.UserAchievements.Where(x => x.Id == row.Id).DeleteAsync();
            var points = row.Points;
            await db.AchievementMembers
                .Where(x => x.GuildId == guildId && x.UserId == userId)
                .Set(x => x.Points, x => x.Points - points < 0 ? 0 : x.Points - points)
                .Set(x => x.UnlockedCount, x => x.UnlockedCount - 1 < 0 ? 0 : x.UnlockedCount - 1)
                .UpdateAsync();
            DropMember(guildId, userId);
            return AchievementResult<string>.Ok(key);
        }
        finally
        {
            memberLock.Release();
        }
    }

    /// <summary>
    ///     Clears every achievement a member has in a server. Counters and badges stay, and the member is checked
    ///     again quietly so metric achievements they still qualify for come straight back.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <returns>How many were cleared.</returns>
    public async Task<int> ResetMemberAsync(ulong guildId, ulong userId)
    {
        var memberLock = MemberLock(guildId, userId);
        int cleared;
        await memberLock.WaitAsync();
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            cleared = await db.UserAchievements.Where(x => x.GuildId == guildId && x.UserId == userId).DeleteAsync();
            await db.AchievementMembers
                .Where(x => x.GuildId == guildId && x.UserId == userId)
                .Set(x => x.Points, 0)
                .Set(x => x.UnlockedCount, 0)
                .Set(x => x.LastUnlockAt, (DateTime?)null)
                .Set(x => x.Badge1, (string?)null)
                .Set(x => x.Badge2, (string?)null)
                .Set(x => x.Badge3, (string?)null)
                .Set(x => x.Badge4, (string?)null)
                .UpdateAsync();
            DropMember(guildId, userId);
        }
        finally
        {
            memberLock.Release();
        }

        if (client.GetGuild(guildId) is { } guild)
            _ = EvaluateMembersAsync(guild, [(userId, AchievementDirty.All)], true);
        return cleared;
    }

    /// <summary>
    ///     Clears every achievement in a server.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>How many unlocks were cleared.</returns>
    public async Task<int> ResetGuildAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var cleared = await db.UserAchievements.Where(x => x.GuildId == guildId).DeleteAsync();
        await db.AchievementMembers
            .Where(x => x.GuildId == guildId)
            .Set(x => x.Points, 0)
            .Set(x => x.UnlockedCount, 0)
            .Set(x => x.LastUnlockAt, (DateTime?)null)
            .Set(x => x.Badge1, (string?)null)
            .Set(x => x.Badge2, (string?)null)
            .Set(x => x.Badge3, (string?)null)
            .Set(x => x.Badge4, (string?)null)
            .UpdateAsync();
        foreach (var key in unlockCache.Keys.Where(k => k.GuildId == guildId))
            unlockCache.TryRemove(key, out _);
        await UpdateSettingsAsync(guildId, row => row.BackfilledAt = null);
        QueueSweep(guildId);
        return cleared;
    }

    #endregion

    /// <summary>
    ///     A member's unlocked achievements, cached.
    /// </summary>
    private sealed class UnlockState(Dictionary<string, DateTime> keys, int points)
    {
        /// <summary>
        ///     When the cache entry was last read.
        /// </summary>
        public DateTime LastUsed { get; set; } = DateTime.UtcNow;

        /// <summary>
        ///     Points earned.
        /// </summary>
        public int Points
        {
            get
            {
                lock (keys)
                    return points;
            }
        }

        /// <summary>
        ///     Achievements unlocked.
        /// </summary>
        public int Count
        {
            get
            {
                lock (keys)
                    return keys.Count;
            }
        }

        /// <summary>
        ///     Whether an achievement is unlocked.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>True when unlocked.</returns>
        public bool Has(string key)
        {
            lock (keys)
                return keys.ContainsKey(key);
        }

        /// <summary>
        ///     Records an unlock.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="at">When.</param>
        /// <param name="earned">Points earned.</param>
        public void Add(string key, DateTime at, int earned)
        {
            lock (keys)
            {
                if (keys.TryAdd(key, at))
                    points += earned;
            }
        }

        /// <summary>
        ///     Forgets an unlock.
        /// </summary>
        /// <param name="key">The key.</param>
        public void Remove(string key)
        {
            lock (keys)
                keys.Remove(key);
        }

        /// <summary>
        ///     A copy of the unlocks.
        /// </summary>
        /// <returns>Key to unlock time.</returns>
        public Dictionary<string, DateTime> Snapshot()
        {
            lock (keys)
                return new Dictionary<string, DateTime>(keys, StringComparer.Ordinal);
        }
    }

    /// <summary>
    ///     A member's message counts, cached between reads.
    /// </summary>
    private sealed class MessageTally
    {
        /// <summary>
        ///     Messages all time.
        /// </summary>
        public long Total { get; set; }

        /// <summary>
        ///     Messages today.
        /// </summary>
        public long Day { get; set; }

        /// <summary>
        ///     Messages this week.
        /// </summary>
        public long Week { get; set; }

        /// <summary>
        ///     Messages this month.
        /// </summary>
        public long Month { get; set; }

        /// <summary>
        ///     Channels posted in.
        /// </summary>
        public HashSet<ulong> Channels { get; } = [];

        /// <summary>
        ///     Start of the day the counts cover.
        /// </summary>
        public DateTime DayStart { get; init; }

        /// <summary>
        ///     Start of the week the counts cover.
        /// </summary>
        public DateTime WeekStart { get; init; }

        /// <summary>
        ///     Start of the month the counts cover.
        /// </summary>
        public DateTime MonthStart { get; init; }

        /// <summary>
        ///     When the counts were read from the database.
        /// </summary>
        public DateTime QueriedAt { get; set; }
    }

    /// <summary>
    ///     Reaction totals for one member.
    /// </summary>
    private sealed class EmojiAggregate
    {
        /// <summary>
        ///     The member.
        /// </summary>
        public ulong UserId { get; set; }

        /// <summary>
        ///     Reactions added.
        /// </summary>
        public long Total { get; set; }

        /// <summary>
        ///     Different emojis used.
        /// </summary>
        public long Unique { get; set; }

        /// <summary>
        ///     Most uses of one emoji.
        /// </summary>
        public long Max { get; set; }
    }

    /// <summary>
    ///     A row count for one member.
    /// </summary>
    private sealed class CountAggregate
    {
        /// <summary>
        ///     The member.
        /// </summary>
        public ulong UserId { get; set; }

        /// <summary>
        ///     Rows counted.
        /// </summary>
        public long Count { get; set; }
    }

    /// <summary>
    ///     Message counts inside the current day, week, and month for one member.
    /// </summary>
    private sealed class WindowAggregate
    {
        /// <summary>
        ///     The member.
        /// </summary>
        public ulong UserId { get; set; }

        /// <summary>
        ///     Messages today.
        /// </summary>
        public long Day { get; set; }

        /// <summary>
        ///     Messages this week.
        /// </summary>
        public long Week { get; set; }

        /// <summary>
        ///     Messages this month.
        /// </summary>
        public long Month { get; set; }
    }
}
