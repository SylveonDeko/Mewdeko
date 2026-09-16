using System.Text.Json;
using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.ServerStats.Common;
using Mewdeko.Modules.ServerStats.Services;
using Mewdeko.Modules.StatRoles.Common;
using Mewdeko.Services.Analytics;

namespace Mewdeko.Modules.StatRoles.Services;

/// <summary>
///     Grants and removes roles on a schedule based on member activity. Unlike level rewards, a stat role is
///     re-evaluated every run: members who stop meeting the condition lose the role unless it is permanent, so
///     "active member" style roles stay honest.
/// </summary>
public class StatRoleService : INService, IReadyExecutor, IDisposable
{
    /// <summary>
    ///     The shortest evaluation interval a stat role may be given.
    /// </summary>
    public const int MinimumIntervalMinutes = 10;

    /// <summary>
    ///     The default notification template.
    /// </summary>
    public const string DefaultNotifyMessage = "%user% %action% %role%";

    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(1);
    private static readonly JsonSerializerOptions JsonOptions = new();

    private readonly DiscordShardedClient client;
    private readonly IAnalyticsCollector collector;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> guildLocks = new();
    private readonly ILogger<StatRoleService> logger;
    private readonly ConcurrentDictionary<ulong, DateTime> manualRuns = new();
    private readonly ServerStatsService stats;
    private Timer? tickTimer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StatRoleService" /> class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="stats">The stats query service.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    /// <param name="collector">The analytics collector.</param>
    public StatRoleService(DiscordShardedClient client, IDataConnectionFactory dbFactory, ServerStatsService stats,
        ILogger<StatRoleService> logger, IAnalyticsCollector collector)
    {
        this.client = client;
        this.dbFactory = dbFactory;
        this.stats = stats;
        this.logger = logger;
        this.collector = collector;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        tickTimer?.Dispose();
    }

    /// <inheritdoc />
    public Task OnReadyAsync()
    {
        tickTimer = new Timer(_ => _ = RunDueAsync(), null, TimeSpan.FromMinutes(2), TickInterval);
        logger.LogInformation("Stat role scheduler ready");
        return Task.CompletedTask;
    }

    #region Notifications

    private async Task NotifyAsync(SocketGuild guild, StatRole role, SocketRole discordRole, SocketGuildUser user,
        StatRoleAction action, long value)
    {
        if (role.NotifyChannelId == null && !role.NotifyDm)
            return;

        var template = string.IsNullOrWhiteSpace(role.NotifyMessage) ? DefaultNotifyMessage : role.NotifyMessage;
        var stat = (StatRoleStat)role.StatType;
        var rep = new ReplacementBuilder()
            .WithServer(client, guild)
            .WithUser(user)
            .WithOverride("%role%", () => discordRole.Mention)
            .WithOverride("%role.name%", () => discordRole.Name)
            .WithOverride("%action%", () => action == StatRoleAction.Granted ? "earned" : "lost")
            .WithOverride("%value%", () => value.ToString("N0"))
            .WithOverride("%stat%", () => stat.ToString())
            .WithOverride("%statrole.name%", () => role.Name)
            .Build();

        var text = rep.Replace(template);

        if (role.NotifyChannelId is { } channelId && guild.GetTextChannel(channelId) is { } channel)
        {
            try
            {
                if (SmartEmbed.TryParse(text, guild.Id, out var embeds, out var plain, out var components))
                    await channel.SendMessageAsync(plain, embeds: embeds, components: components?.Build());
                else
                    await channel.SendMessageAsync(text, allowedMentions: AllowedMentions.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Stat role notification failed in {GuildId}", guild.Id);
            }
        }

        if (role.NotifyDm)
        {
            try
            {
                var dm = await user.CreateDMChannelAsync();
                await dm.SendMessageAsync(text);
            }
            catch
            {
                // Closed DMs are expected; nothing to do.
            }
        }
    }

    #endregion

    #region CRUD

    /// <summary>
    ///     Lists a guild's stat roles.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The stat roles, oldest first.</returns>
    public async Task<List<StatRole>> GetAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.StatRoles.Where(x => x.GuildId == guildId).OrderBy(x => x.Id).ToListAsync();
    }

    /// <summary>
    ///     Gets one stat role.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The stat role ID.</param>
    /// <returns>The stat role, or null.</returns>
    public async Task<StatRole?> GetAsync(ulong guildId, int id)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.StatRoles.FirstOrDefaultAsync(x => x.GuildId == guildId && x.Id == id);
    }

    /// <summary>
    ///     Finds the stat role that manages a Discord role.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="roleId">The Discord role.</param>
    /// <returns>The stat role, or null.</returns>
    public async Task<StatRole?> GetByRoleAsync(ulong guildId, ulong roleId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.StatRoles.FirstOrDefaultAsync(x => x.GuildId == guildId && x.RoleId == roleId);
    }

    /// <summary>
    ///     Creates a stat role. Each Discord role can be managed by one stat role only.
    /// </summary>
    /// <param name="role">The stat role to create; its ID is filled in.</param>
    /// <returns>The created stat role, or null when the Discord role is already managed.</returns>
    public async Task<StatRole?> CreateAsync(StatRole role)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        if (await db.StatRoles.AnyAsync(x => x.GuildId == role.GuildId && x.RoleId == role.RoleId))
            return null;

        Normalize(role);
        role.DateAdded = DateTime.UtcNow;
        role.Id = await db.InsertWithInt32IdentityAsync(role);
        return role;
    }

    /// <summary>
    ///     Saves changes to a stat role.
    /// </summary>
    /// <param name="role">The stat role.</param>
    public async Task UpdateAsync(StatRole role)
    {
        Normalize(role);
        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(role);
    }

    /// <summary>
    ///     Deletes a stat role. Members keep whatever they currently hold.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The stat role ID.</param>
    /// <returns>True when deleted.</returns>
    public async Task<bool> DeleteAsync(ulong guildId, int id)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.StatRoles.Where(x => x.GuildId == guildId && x.Id == id).DeleteAsync() > 0;
    }

    private static void Normalize(StatRole role)
    {
        role.IntervalMinutes = Math.Max(MinimumIntervalMinutes, role.IntervalMinutes);
        role.LookbackDays = Math.Clamp(role.LookbackDays, 0, ServerStatsService.MaxLookbackDays);
        role.Minimum = Math.Max(0, role.Minimum);
        role.TopStart = Math.Max(1, role.TopStart);
        role.TopEnd = Math.Max(role.TopStart, role.TopEnd);
        role.RequiredDays = Math.Max(1, role.RequiredDays);
        if (role.Maximum.HasValue && role.Maximum < role.Minimum)
            role.Maximum = role.Minimum;
        if (string.IsNullOrWhiteSpace(role.Name))
            role.Name = $"Stat role {role.RoleId}";
    }

    /// <summary>
    ///     Reads a JSON snowflake list column.
    /// </summary>
    /// <param name="json">The column value.</param>
    /// <returns>The IDs, or empty.</returns>
    public static HashSet<ulong> ReadIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<HashSet<ulong>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    ///     Writes a JSON snowflake list column.
    /// </summary>
    /// <param name="ids">The IDs.</param>
    /// <returns>The column value, or null when empty.</returns>
    public static string? WriteIds(IEnumerable<ulong> ids)
    {
        var list = ids.Distinct().ToList();
        return list.Count == 0 ? null : JsonSerializer.Serialize(list, JsonOptions);
    }

    #endregion

    #region Scheduling

    private async Task RunDueAsync()
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var now = DateTime.UtcNow;
            var enabled = await db.StatRoles.Where(x => x.Enabled).ToListAsync();

            var dueGuilds = enabled
                .Where(x => x.LastRunAt == null ||
                            now - x.LastRunAt.Value >= TimeSpan.FromMinutes(Math.Max(MinimumIntervalMinutes,
                                x.IntervalMinutes)))
                .Select(x => x.GuildId)
                .Distinct()
                .ToList();

            foreach (var guildId in dueGuilds)
            {
                var guild = client.GetGuild(guildId);
                if (guild == null)
                    continue;

                var roles = enabled.Where(x => x.GuildId == guildId).ToList();
                var due = roles.Where(x => x.LastRunAt == null ||
                                           now - x.LastRunAt.Value >= TimeSpan.FromMinutes(
                                               Math.Max(MinimumIntervalMinutes, x.IntervalMinutes))).ToList();
                try
                {
                    await RunGuildAsync(guild, roles, due, false);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Stat role run failed for guild {GuildId}", guildId);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Stat role scheduler tick failed");
        }
    }

    /// <summary>
    ///     Evaluates one stat role now. Grouped roles are evaluated with their siblings so the highest tier rule
    ///     holds, but only the requested role is applied.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="role">The stat role.</param>
    /// <param name="dryRun">When true nothing is changed and the result only describes what would happen.</param>
    /// <returns>The outcome.</returns>
    public async Task<StatRoleRunResult> RunAsync(SocketGuild guild, StatRole role, bool dryRun)
    {
        var siblings = string.IsNullOrWhiteSpace(role.GroupName)
            ? [role]
            : (await GetAsync(guild.Id)).Where(x => x.Enabled && x.GroupName == role.GroupName).ToList();
        if (siblings.All(x => x.Id != role.Id))
            siblings.Add(role);

        var results = await RunGuildAsync(guild, siblings, [role], dryRun);
        return results.First(x => x.StatRoleId == role.Id);
    }

    /// <summary>
    ///     Reports whether a manual run is allowed right now. Manual runs are limited to one every ten minutes per
    ///     guild so they cannot hammer the role endpoints.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>True when a manual run may proceed.</returns>
    public bool TryReserveManualRun(ulong guildId)
    {
        var now = DateTime.UtcNow;
        if (manualRuns.TryGetValue(guildId, out var last) && now - last < TimeSpan.FromMinutes(10))
            return false;
        manualRuns[guildId] = now;
        return true;
    }

    private async Task<List<StatRoleRunResult>> RunGuildAsync(SocketGuild guild, List<StatRole> roles,
        List<StatRole> toApply, bool dryRun)
    {
        var gate = guildLocks.GetOrAdd(guild.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            if (!guild.HasAllMembers)
                await guild.DownloadUsersAsync();

            var members = guild.Users.ToList();
            var qualifying = new Dictionary<int, Dictionary<ulong, StatRoleMember>>();
            foreach (var role in roles)
                qualifying[role.Id] = await EvaluateAsync(guild, role, members);

            ApplyGroups(roles, qualifying);

            var results = new List<StatRoleRunResult>();
            foreach (var role in toApply)
            {
                var discordRole = guild.GetRole(role.RoleId);
                var q = qualifying[role.Id];
                var holders = discordRole == null
                    ? []
                    : members.Where(m => m.Roles.Any(r => r.Id == role.RoleId)).Select(m => m.Id).ToHashSet();

                var ignored = ReadIds(role.IgnoredUsers);
                var toGrant = q.Values.Where(x => !holders.Contains(x.UserId)).ToList();
                var toRemove = role.Permanent
                    ? []
                    : holders.Where(h => !q.ContainsKey(h) && !ignored.Contains(h))
                        .Select(h => new StatRoleMember(h, 0, null)).ToList();

                var granted = 0;
                var removed = 0;
                var failed = 0;

                if (!dryRun && discordRole != null && guild.CurrentUser.GuildPermissions.ManageRoles &&
                    discordRole.Position < guild.CurrentUser.Hierarchy)
                {
                    foreach (var member in toGrant)
                    {
                        var user = guild.GetUser(member.UserId);
                        if (user == null) continue;
                        try
                        {
                            await user.AddRoleAsync(discordRole);
                            granted++;
                            await NotifyAsync(guild, role, discordRole, user, StatRoleAction.Granted, member.Value);
                            await Task.Delay(250);
                        }
                        catch (Exception ex)
                        {
                            failed++;
                            logger.LogWarning(ex, "Could not grant stat role {RoleId} to {UserId}", role.RoleId,
                                member.UserId);
                        }
                    }

                    foreach (var member in toRemove)
                    {
                        var user = guild.GetUser(member.UserId);
                        if (user == null) continue;
                        try
                        {
                            await user.RemoveRoleAsync(discordRole);
                            removed++;
                            await NotifyAsync(guild, role, discordRole, user, StatRoleAction.Removed, member.Value);
                            await Task.Delay(250);
                        }
                        catch (Exception ex)
                        {
                            failed++;
                            logger.LogWarning(ex, "Could not remove stat role {RoleId} from {UserId}", role.RoleId,
                                member.UserId);
                        }
                    }

                    collector.Feature("stat_roles", guild.Id);
                }

                if (!dryRun)
                {
                    await using var db = await dbFactory.CreateConnectionAsync();
                    await db.StatRoles.Where(x => x.Id == role.Id)
                        .Set(x => x.LastRunAt, DateTime.UtcNow)
                        .UpdateAsync();
                }

                results.Add(new StatRoleRunResult(role.Id, role.RoleId,
                    q.Values.OrderByDescending(x => x.Value).ToList(), toGrant, toRemove, granted, removed, failed));
            }

            return results;
        }
        finally
        {
            gate.Release();
        }
    }

    #endregion

    #region Evaluation

    private async Task<Dictionary<ulong, StatRoleMember>> EvaluateAsync(SocketGuild guild, StatRole role,
        List<SocketGuildUser> members)
    {
        var whitelist = ReadIds(role.RoleWhitelist);
        var blacklist = ReadIds(role.RoleBlacklist);
        var ignored = ReadIds(role.IgnoredUsers);
        var channels = ReadIds(role.ChannelFilter);

        var candidates = members
            .Where(m => role.ApplyToBots || !m.IsBot)
            .Where(m => !ignored.Contains(m.Id))
            .Where(m => whitelist.Count == 0 || m.Roles.Any(r => whitelist.Contains(r.Id)))
            .Where(m => blacklist.Count == 0 || !m.Roles.Any(r => blacklist.Contains(r.Id)))
            .ToList();

        var values = await MeasureAsync(guild, role, candidates, channels);

        var ranked = values.Where(x => x.Value > 0).OrderByDescending(x => x.Value).ToList();
        var ranks = new Dictionary<ulong, int>();
        var lastValue = long.MinValue;
        var lastRank = 0;
        for (var i = 0; i < ranked.Count; i++)
        {
            if (ranked[i].Value != lastValue)
            {
                lastRank = i + 1;
                lastValue = ranked[i].Value;
            }

            ranks[ranked[i].Key] = lastRank;
        }

        var limit = (StatRoleLimit)role.LimitType;
        var result = new Dictionary<ulong, StatRoleMember>();
        foreach (var member in candidates)
        {
            var value = values.GetValueOrDefault(member.Id);
            ranks.TryGetValue(member.Id, out var rank);
            int? rankOrNull = rank == 0 ? null : rank;

            var qualifies = limit switch
            {
                StatRoleLimit.TopRank => rank != 0 && rank >= role.TopStart && rank <= role.TopEnd,
                StatRoleLimit.TopPercent => rank != 0 && ranked.Count > 0 &&
                                            InPercentile(rank, ranked.Count, role.TopStart, role.TopEnd),
                StatRoleLimit.DailyStreak => value >= role.RequiredDays,
                _ => value >= role.Minimum && (!role.Maximum.HasValue || value <= role.Maximum.Value)
            };

            if (role.Invert)
                qualifies = !qualifies;

            if (qualifies)
                result[member.Id] = new StatRoleMember(member.Id, value, rankOrNull);
        }

        return result;
    }

    private static bool InPercentile(int rank, int total, int start, int end)
    {
        var percentile = rank * 100.0 / total;
        return percentile >= start - 0.0001 && percentile <= end + 0.0001 ||
               rank == 1 && start <= 1;
    }

    private async Task<Dictionary<ulong, long>> MeasureAsync(SocketGuild guild, StatRole role,
        List<SocketGuildUser> candidates, HashSet<ulong> channels)
    {
        var now = DateTime.UtcNow;
        var stat = (StatRoleStat)role.StatType;
        var limit = (StatRoleLimit)role.LimitType;
        var channelFilter = channels.Count == 0 ? null : channels;

        var activityName = string.IsNullOrWhiteSpace(role.ActivityName) ||
                           role.ActivityName.Equals("any", StringComparison.OrdinalIgnoreCase)
            ? null
            : role.ActivityName;

        switch (stat)
        {
            case StatRoleStat.Messages when limit == StatRoleLimit.DailyStreak:
            case StatRoleStat.VoiceMinutes when limit == StatRoleLimit.DailyStreak:
            {
                var kind = stat == StatRoleStat.Messages ? StatKind.Messages : StatKind.Voice;
                var perDay = await stats.GetPerUserDailyTotalsAsync(guild.Id, kind,
                    Math.Max(1, role.LookbackDays), channelFilter);
                var perDayMinimum = stat == StatRoleStat.VoiceMinutes ? role.Minimum * 60 : role.Minimum;
                return perDay.ToDictionary(x => x.Key,
                    x => (long)x.Value.Count(d => d.Value >= Math.Max(1, perDayMinimum)));
            }
            case StatRoleStat.ActivityMinutes when limit == StatRoleLimit.DailyStreak:
            {
                var perDay = await stats.GetPerUserDailyActivitySecondsAsync(guild.Id, activityName,
                    Math.Max(1, role.LookbackDays));
                return perDay.ToDictionary(x => x.Key,
                    x => (long)x.Value.Count(d => d.Value >= Math.Max(60, role.Minimum * 60)));
            }
            case StatRoleStat.ActivityMinutes:
            {
                var seconds = await stats.GetPerUserActivitySecondsAsync(guild.Id, activityName, role.LookbackDays);
                return seconds.ToDictionary(x => x.Key, x => x.Value / 60);
            }
            case StatRoleStat.Messages:
                return await stats.GetPerUserTotalsAsync(guild.Id, StatKind.Messages, role.LookbackDays,
                    channelFilter);
            case StatRoleStat.VoiceMinutes:
            {
                var seconds = await stats.GetPerUserTotalsAsync(guild.Id, StatKind.Voice, role.LookbackDays,
                    channelFilter);
                return seconds.ToDictionary(x => x.Key, x => x.Value / 60);
            }
            case StatRoleStat.Invites:
            {
                await using var db = await dbFactory.CreateConnectionAsync();
                return (await db.InviteCounts.Where(x => x.GuildId == guild.Id)
                        .Select(x => new
                        {
                            x.UserId, x.Count
                        })
                        .ToListAsync())
                    .ToDictionary(x => x.UserId, x => (long)x.Count);
            }
            case StatRoleStat.JoinedDays:
                return candidates.Where(m => m.JoinedAt.HasValue)
                    .ToDictionary(m => m.Id, m => (long)(now - m.JoinedAt!.Value.UtcDateTime).TotalDays);
            case StatRoleStat.AccountDays:
                return candidates.ToDictionary(m => m.Id, m => (long)(now - m.CreatedAt.UtcDateTime).TotalDays);
            default:
                return new Dictionary<ulong, long>();
        }
    }

    /// <summary>
    ///     Within each group, keeps only the highest tier a member qualifies for. Tier order is the minimum for
    ///     threshold and streak roles, and the top start for rank and percent roles (lower is better there).
    /// </summary>
    private static void ApplyGroups(List<StatRole> roles, Dictionary<int, Dictionary<ulong, StatRoleMember>> qualifying)
    {
        foreach (var group in roles.Where(r => !string.IsNullOrWhiteSpace(r.GroupName)).GroupBy(r => r.GroupName))
        {
            var ordered = group.OrderByDescending(Tier).ToList();
            if (ordered.Count < 2)
                continue;

            var claimed = new HashSet<ulong>();
            foreach (var role in ordered)
            {
                var q = qualifying[role.Id];
                foreach (var userId in q.Keys.ToList())
                {
                    if (!claimed.Add(userId))
                        q.Remove(userId);
                }
            }
        }
    }

    private static double Tier(StatRole role)
    {
        var limit = (StatRoleLimit)role.LimitType;
        return limit is StatRoleLimit.TopRank or StatRoleLimit.TopPercent
            ? 1_000_000_000d - role.TopStart
            : role.Minimum;
    }

    #endregion
}