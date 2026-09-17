using System.Globalization;
using System.Threading;
using LinqToDB.Async;
using Mewdeko.Controllers.Common.Analytics;
using Mewdeko.Database.DbContextStuff;

namespace Mewdeko.Services.Analytics;

public sealed partial class AnalyticsQueryService
{
    /// <summary>
    ///     How long the per guild configured and enabled feature sets are reused before being rebuilt. The
    ///     definitions run one query per feature over every guild, which is too much to do per request.
    /// </summary>
    private static readonly TimeSpan FeatureSetsTtl = TimeSpan.FromMinutes(10);

    private readonly SemaphoreSlim featureSetsLock = new(1, 1);
    private FeatureSets? featureSets;

    /// <summary>
    ///     Builds one row per guild the bot is in: member makeup from the gateway cache, plus commands, events
    ///     and feature use in range and the features each guild has configured.
    /// </summary>
    /// <param name="range">The range for commands, events and feature use.</param>
    /// <param name="bot">Instance filter for the activity tables.</param>
    /// <param name="search">Optional case insensitive match on guild name or id.</param>
    public async Task<List<GuildOverviewRow>> GuildOverviewAsync(AnalyticsRange range, string? bot, string? search)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);

        var commands = await CommandsIn(db, range, bot)
            .GroupBy(x => x.GuildId)
            .Select(g => new
            {
                GuildId = g.Key, Count = g.LongCount()
            })
            .ToListAsync().ConfigureAwait(false);
        var commandsByGuild = commands.Where(x => x.GuildId is not null)
            .ToDictionary(x => x.GuildId!.Value, x => x.Count);

        var events = await ActivityIn(db, range, bot)
            .GroupBy(x => x.GuildId)
            .Select(g => new
            {
                GuildId = g.Key, Count = g.Sum(x => (long)x.Count)
            })
            .ToListAsync().ConfigureAwait(false);
        var eventsByGuild = events.ToDictionary(x => x.GuildId, x => x.Count);

        var activity = await FeatureActivityByGuildAsync(db, range, bot).ConfigureAwait(false);
        var featuresByGuild = activity.GroupBy(x => x.GuildId)
            .ToDictionary(g => g.Key,
                g => g.OrderByDescending(x => x.Count).Select(x => x.Feature).Distinct().ToList());

        var sets = await GetFeatureSetsAsync(db).ConfigureAwait(false);

        var rows = new List<GuildOverviewRow>(client.Guilds.Count);
        foreach (var guild in client.Guilds)
        {
            if (!string.IsNullOrWhiteSpace(search) &&
                !guild.Name.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !guild.Id.ToString(CultureInfo.InvariantCulture).Contains(search, StringComparison.Ordinal))
                continue;

            var used = featuresByGuild.GetValueOrDefault(guild.Id) ?? [];
            rows.Add(new GuildOverviewRow(
                Snowflake(guild.Id)!,
                guild.Name,
                client.GetShardIdFor(guild),
                Shape(guild),
                guild.CurrentUser?.JoinedAt?.UtcDateTime,
                commandsByGuild.GetValueOrDefault(guild.Id),
                eventsByGuild.GetValueOrDefault(guild.Id),
                used.Count,
                sets.Configured.GetValueOrDefault(guild.Id)?.Count ?? 0,
                sets.Enabled.GetValueOrDefault(guild.Id)?.Count ?? 0,
                used));
        }

        return rows.OrderByDescending(x => x.Shape.MemberCount).ToList();
    }

    /// <summary>
    ///     Reads member makeup and size from the gateway cache. Bot and online counts come from cached members,
    ///     which is every member because the client downloads them on join.
    /// </summary>
    private static GuildShape Shape(SocketGuild guild)
    {
        var bots = 0;
        var online = 0;
        foreach (var user in guild.Users)
        {
            if (user.IsBot) bots++;
            if (user.Status is UserStatus.Online or UserStatus.Idle or UserStatus.DoNotDisturb) online++;
        }

        var humans = Math.Max(0, guild.MemberCount - bots);
        return new GuildShape(guild.MemberCount, humans, bots, online, guild.PremiumSubscriptionCount,
            (int)guild.PremiumTier, guild.Channels.Count, guild.Roles.Count,
            guild.OwnerId.ToString(CultureInfo.InvariantCulture), guild.CreatedAt.UtcDateTime);
    }

    /// <summary>
    ///     Returns which features each guild has configured and enabled, rebuilt at most every
    ///     <see cref="FeatureSetsTtl" />.
    /// </summary>
    private async Task<FeatureSets> GetFeatureSetsAsync(MewdekoDb db)
    {
        var cached = featureSets;
        if (cached is not null && DateTime.UtcNow - cached.BuiltAt < FeatureSetsTtl)
            return cached;

        await featureSetsLock.WaitAsync().ConfigureAwait(false);
        try
        {
            cached = featureSets;
            if (cached is not null && DateTime.UtcNow - cached.BuiltAt < FeatureSetsTtl)
                return cached;

            var configured = new Dictionary<ulong, HashSet<string>>();
            var enabled = new Dictionary<ulong, HashSet<string>>();

            foreach (var feature in AnalyticsFeatureDefinitions.Features)
            {
                try
                {
                    foreach (var guildId in await feature.Configured(db).Distinct().ToListAsync()
                                 .ConfigureAwait(false))
                        Add(configured, guildId, feature.Key);
                    foreach (var guildId in await feature.Enabled(db).Distinct().ToListAsync()
                                 .ConfigureAwait(false))
                        Add(enabled, guildId, feature.Key);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Feature set query failed for {Feature}", feature.Key);
                }
            }

            featureSets = new FeatureSets(configured, enabled, DateTime.UtcNow);
            return featureSets;
        }
        finally
        {
            featureSetsLock.Release();
        }

        static void Add(Dictionary<ulong, HashSet<string>> map, ulong guildId, string feature)
        {
            if (!map.TryGetValue(guildId, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                map[guildId] = set;
            }

            set.Add(feature);
        }
    }

    private sealed record FeatureSets(
        Dictionary<ulong, HashSet<string>> Configured,
        Dictionary<ulong, HashSet<string>> Enabled,
        DateTime BuiltAt);
}