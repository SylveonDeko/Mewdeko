using System.Globalization;
using System.Threading;
using DataModel;
using Discord.Rest;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Services.Impl;
using Mewdeko.Services.Settings;

namespace Mewdeko.Modules.OwnerOnly.Services;

/// <summary>
///     Purges everything stored for a server once the bot has been out of it for a grace period, so quick
///     kick-and-reinvite cycles keep their data while servers that are really gone stop lingering in the database.
/// </summary>
/// <remarks>
///     The purge is schema driven rather than a hand maintained table list: every table in the public schema
///     with a "GuildId" column is swept, and rows in child tables that reference a swept row through a
///     non-cascading foreign key are removed first. New tables and foreign keys are picked up automatically.
///     Owner level analytics tables keep their own retention and are left alone, as is the leave feedback the
///     owner gave on the way out.
/// </remarks>
public class GuildDataRetentionService : INService, IReadyExecutor
{
    /// <summary>
    ///     How many due guilds are purged per pass over the tables. The first orphan scan on a long lived database
    ///     queues tens of thousands of guilds, and one pass per guild would take weeks.
    /// </summary>
    private const int PurgeBatchSize = 100;

    /// <summary>
    ///     Tables with more estimated rows than this are skipped by the orphan scan. Every guild that ever touched
    ///     the bot has a "GuildConfigs" row, so the big activity tables add nothing but a multi gigabyte scan.
    /// </summary>
    private const long ScanRowLimit = 1_000_000;

    /// <summary>
    ///     Gap between sweeps of the pending queue.
    /// </summary>
    private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(1);

    /// <summary>
    ///     Gap between scans for guild ids that have data but no leave record, which catches leaves that
    ///     happened while the bot was offline as well as data from before this service existed.
    /// </summary>
    private static readonly TimeSpan OrphanScanInterval = TimeSpan.FromHours(24);

    /// <summary>
    ///     How long after ready the first sweep waits, so every shard has had a chance to populate its guild cache.
    /// </summary>
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     Pause between per-table statements so a large purge doesn't starve normal traffic.
    /// </summary>
    private static readonly TimeSpan TableDelay = TimeSpan.FromMilliseconds(100);

    /// <summary>
    ///     Tables with a "GuildId" column that must survive a purge.
    /// </summary>
    private static readonly HashSet<string> ExcludedTables = new(StringComparer.Ordinal)
    {
        "GuildDataRetention", "GuildLeaveFeedbacks"
    };

    /// <summary>
    ///     Table name prefixes that must survive a purge. Analytics tables are bot level and have their own retention.
    /// </summary>
    private static readonly string[] ExcludedPrefixes = ["Analytics"];

    private readonly BotConfigService bss;
    private readonly DiscordShardedClient client;
    private readonly BotCredentials creds;
    private readonly IDataConnectionFactory dbFactory;
    private readonly EventHandler handler;
    private readonly ILogger<GuildDataRetentionService> logger;
    private readonly SemaphoreSlim purgeLock = new(1, 1);
    private DateTime lastOrphanScan = DateTime.MinValue;
    private SchemaSnapshot? schema;

    /// <summary>
    ///     Initializes a new instance of <see cref="GuildDataRetentionService" />.
    /// </summary>
    /// <param name="handler">The event handler the guild join and leave events are subscribed on.</param>
    /// <param name="client">The discord client.</param>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="bss">The bot config service.</param>
    /// <param name="creds">The bot credentials.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public GuildDataRetentionService(EventHandler handler, DiscordShardedClient client,
        IDataConnectionFactory dbFactory, BotConfigService bss, BotCredentials creds,
        ILogger<GuildDataRetentionService> logger)
    {
        this.handler = handler;
        this.client = client;
        this.dbFactory = dbFactory;
        this.bss = bss;
        this.creds = creds;
        this.logger = logger;
    }

    /// <summary>
    ///     Whether purges run automatically.
    /// </summary>
    public bool Enabled
    {
        get
        {
            return bss.Data.GuildDataRetentionEnabled;
        }
    }

    /// <summary>
    ///     How long a server's data is kept after the bot leaves it.
    /// </summary>
    public TimeSpan GracePeriod
    {
        get
        {
            return TimeSpan.FromDays(Math.Max(0, bss.Data.GuildDataRetentionDays));
        }
    }

    /// <inheritdoc />
    public Task OnReadyAsync()
    {
        handler.Subscribe("LeftGuild", "GuildDataRetentionService", OnLeftGuild);
        handler.Subscribe("JoinedGuild", "GuildDataRetentionService", OnJoinedGuild);
        _ = RunLoop();
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Queues a guild for purging after the grace period, or refreshes the deadline if it is already queued.
    /// </summary>
    /// <param name="guildId">The guild to queue.</param>
    /// <param name="guildName">The guild's name, for the owner facing listings.</param>
    /// <param name="source">Where the request came from: "left", "scan" or "manual".</param>
    /// <param name="delay">How long to wait before purging, or null for the configured grace period.</param>
    public async Task QueueAsync(ulong guildId, string guildName, string source, TimeSpan? delay = null)
    {
        var now = DateTime.UtcNow;
        var purgeAfter = now + (delay ?? GracePeriod);

        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        await db.GuildDataRetention
            .Merge()
            .Using([
                new GuildDataRetention
                {
                    GuildId = guildId,
                    GuildName = guildName,
                    LeftAt = now,
                    PurgeAfter = purgeAfter,
                    Source = source,
                    DateAdded = now
                }
            ])
            .OnTargetKey()
            .UpdateWhenMatched((target, src) => new GuildDataRetention
            {
                GuildName = src.GuildName,
                LeftAt = src.LeftAt,
                PurgeAfter = src.PurgeAfter,
                PurgedAt = null,
                RowsDeleted = 0,
                Source = src.Source
            })
            .InsertWhenNotMatched()
            .MergeAsync().ConfigureAwait(false);

        logger.LogInformation("Queued data purge for {GuildName} [{GuildId}] after {PurgeAfter:u} ({Source})",
            guildName, guildId, purgeAfter, source);
    }

    /// <summary>
    ///     Removes a guild from the purge queue.
    /// </summary>
    /// <param name="guildId">The guild to keep.</param>
    /// <returns>True when a pending entry was removed.</returns>
    public async Task<bool> CancelAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        var removed = await db.GuildDataRetention
            .Where(x => x.GuildId == guildId && x.PurgedAt == null)
            .DeleteAsync().ConfigureAwait(false);
        return removed > 0;
    }

    /// <summary>
    ///     Lists guilds waiting to be purged, soonest first.
    /// </summary>
    public async Task<List<GuildDataRetention>> GetPendingAsync()
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        return await db.GuildDataRetention
            .Where(x => x.PurgedAt == null)
            .OrderBy(x => x.PurgeAfter)
            .ToListAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists the most recent completed purges.
    /// </summary>
    /// <param name="limit">Maximum rows to return.</param>
    public async Task<List<GuildDataRetention>> GetRecentPurgesAsync(int limit)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        return await db.GuildDataRetention
            .Where(x => x.PurgedAt != null)
            .OrderByDescending(x => x.PurgedAt)
            .Take(limit)
            .ToListAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Purges a guild immediately, skipping the grace period. Refuses if the bot is still in the guild.
    /// </summary>
    /// <param name="guildId">The guild to purge.</param>
    /// <returns>The number of rows removed, or null when the bot is still in the guild.</returns>
    public async Task<long?> PurgeNowAsync(ulong guildId)
    {
        if (client.GetGuild(guildId) is not null)
            return null;

        await using (var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false))
        {
            var existing = await db.GuildDataRetention.FirstOrDefaultAsync(x => x.GuildId == guildId)
                .ConfigureAwait(false);
            if (existing is null || existing.PurgedAt is not null)
                await QueueAsync(guildId, existing?.GuildName ?? string.Empty, "manual", TimeSpan.Zero)
                    .ConfigureAwait(false);
        }

        return await PurgeGuildsAsync([guildId]).ConfigureAwait(false);
    }

    /// <summary>
    ///     Finds guild ids that have data but that the bot is not in and are not already queued, and queues them
    ///     with the normal grace period.
    /// </summary>
    /// <returns>How many guilds were newly queued.</returns>
    public async Task<int> ScanOrphansAsync()
    {
        if (!AllShardsConnected() || client.Guilds.Count == 0)
            return 0;

        var snapshot = await GetSchemaAsync().ConfigureAwait(false);
        var present = client.Guilds.Select(g => g.Id).ToHashSet();
        var found = new HashSet<ulong>();

        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        foreach (var table in snapshot.ScanTables)
        {
            var ids = await db.QueryToListAsync<decimal>(
                    $"""SELECT DISTINCT "GuildId"::numeric FROM {Quote(table)} WHERE "GuildId" IS NOT NULL AND "GuildId" > 0""")
                .ConfigureAwait(false);
            foreach (var id in ids)
            {
                var guildId = (ulong)id;
                if (!present.Contains(guildId))
                    found.Add(guildId);
            }

            await Task.Delay(TableDelay).ConfigureAwait(false);
        }

        var known = await db.GuildDataRetention
            .Select(x => x.GuildId)
            .ToListAsync().ConfigureAwait(false);
        found.ExceptWith(known);

        var now = DateTime.UtcNow;
        var purgeAfter = now + GracePeriod;
        var rows = found.Select(guildId => new GuildDataRetention
        {
            GuildId = guildId,
            GuildName = string.Empty,
            LeftAt = now,
            PurgeAfter = purgeAfter,
            Source = "scan",
            DateAdded = now
        }).ToList();

        if (rows.Count > 0)
            await db.BulkCopyAsync(rows).ConfigureAwait(false);

        lastOrphanScan = now;
        if (rows.Count > 0)
            logger.LogInformation("Orphan scan queued {Count} guilds the bot is no longer in", rows.Count);
        return rows.Count;
    }

    private Task OnLeftGuild(SocketGuild guild)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await QueueAsync(guild.Id, guild.Name, "left").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to queue data purge for guild {GuildId}", guild.Id);
            }
        });
        return Task.CompletedTask;
    }

    private Task OnJoinedGuild(SocketGuild guild)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                if (await CancelAsync(guild.Id).ConfigureAwait(false))
                    logger.LogInformation("Cancelled pending data purge for {GuildName} [{GuildId}], bot was re-added",
                        guild.Name, guild.Id);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to cancel data purge for guild {GuildId}", guild.Id);
            }
        });
        return Task.CompletedTask;
    }

    private async Task RunLoop()
    {
        await Task.Delay(StartupDelay).ConfigureAwait(false);

        while (true)
        {
            try
            {
                await SweepAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Guild data retention sweep failed");
            }

            await Task.Delay(SweepInterval).ConfigureAwait(false);
        }
    }

    private async Task SweepAsync()
    {
        if (!AllShardsConnected())
        {
            logger.LogDebug("Skipping guild data retention sweep, not every shard is connected");
            return;
        }

        await ReconcileAsync().ConfigureAwait(false);

        if (!Enabled)
            return;

        if (DateTime.UtcNow - lastOrphanScan >= OrphanScanInterval)
            await ScanOrphansAsync().ConfigureAwait(false);

        List<GuildDataRetention> due;
        await using (var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false))
        {
            var now = DateTime.UtcNow;
            due = await db.GuildDataRetention
                .Where(x => x.PurgedAt == null && x.PurgeAfter <= now)
                .OrderBy(x => x.PurgeAfter)
                .ToListAsync().ConfigureAwait(false);
        }

        var rejoined = due.Where(x => client.GetGuild(x.GuildId) is not null).ToList();
        foreach (var entry in rejoined)
            await CancelAsync(entry.GuildId).ConfigureAwait(false);
        due = due.Except(rejoined).ToList();

        foreach (var batch in due.Chunk(PurgeBatchSize))
        {
            try
            {
                var rows = await PurgeGuildsAsync(batch.Select(x => x.GuildId).ToList()).ConfigureAwait(false);
                await NotifyAsync(batch, rows).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to purge data for {Count} guilds starting at {GuildId}", batch.Length,
                    batch[0].GuildId);
            }
        }
    }

    /// <summary>
    ///     Drops queue entries for guilds the bot turns out to be in, which happens when a join was missed while offline.
    /// </summary>
    private async Task ReconcileAsync()
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        var pending = await db.GuildDataRetention
            .Where(x => x.PurgedAt == null)
            .Select(x => x.GuildId)
            .ToListAsync().ConfigureAwait(false);

        var rejoined = pending.Where(id => client.GetGuild(id) is not null).ToList();
        if (rejoined.Count == 0)
            return;

        await db.GuildDataRetention
            .Where(x => rejoined.Contains(x.GuildId))
            .DeleteAsync().ConfigureAwait(false);
        logger.LogInformation("Cancelled {Count} pending data purges for guilds the bot is in", rejoined.Count);
    }

    /// <summary>
    ///     Deletes every row belonging to the given guilds across all guild scoped tables and records the result.
    ///     Guilds in the same batch share one "PurgedAt" stamp and the batch's row total, so a batch can be read
    ///     back as a unit.
    /// </summary>
    /// <param name="guildIds">The guilds to purge.</param>
    /// <returns>The number of rows removed across the batch.</returns>
    private async Task<long> PurgeGuildsAsync(IReadOnlyList<ulong> guildIds)
    {
        if (guildIds.Count == 0)
            return 0;

        await purgeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var snapshot = await GetSchemaAsync().ConfigureAwait(false);
            var total = 0L;

            await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
            var idList = string.Join(",", guildIds.Select(id => id.ToString(CultureInfo.InvariantCulture)));

            foreach (var table in snapshot.GuildTables)
            {
                await using var transaction = await db.BeginTransactionAsync().ConfigureAwait(false);
                total += await DeleteWhereAsync(db, snapshot, table,
                    $"""{Quote(table)}."GuildId" = ANY(ARRAY[{idList}]::{snapshot.GuildIdType(table)}[])""",
                    []).ConfigureAwait(false);
                await transaction.CommitAsync().ConfigureAwait(false);
                await Task.Delay(TableDelay).ConfigureAwait(false);
            }

            var now = DateTime.UtcNow;
            await db.GuildDataRetention
                .Where(x => guildIds.Contains(x.GuildId))
                .Set(x => x.PurgedAt, now)
                .Set(x => x.RowsDeleted, total)
                .UpdateAsync().ConfigureAwait(false);

            logger.LogInformation("Purged {Rows} rows for {Guilds} guilds across {Tables} tables", total,
                guildIds.Count, snapshot.GuildTables.Count);
            return total;
        }
        finally
        {
            purgeLock.Release();
        }
    }

    /// <summary>
    ///     Deletes rows of <paramref name="table" /> matching <paramref name="predicate" />, first removing rows in
    ///     other tables that reference them through foreign keys that would otherwise block the delete.
    /// </summary>
    private async Task<long> DeleteWhereAsync(MewdekoDb db, SchemaSnapshot snapshot, string table,
        string predicate, HashSet<string> stack)
    {
        if (!stack.Add(table))
            return 0;

        var total = 0L;
        try
        {
            foreach (var fk in snapshot.BlockingReferences(table))
            {
                var childPredicate =
                    $"""{Quote(fk.ChildTable)}.{Quote(fk.ChildColumn)} IN (SELECT {Quote(fk.ParentColumn)} FROM {Quote(table)} WHERE {predicate})""";
                total += await DeleteWhereAsync(db, snapshot, fk.ChildTable, childPredicate, stack)
                    .ConfigureAwait(false);
            }

            total += await db.ExecuteAsync($"DELETE FROM {Quote(table)} WHERE {predicate}")
                .ConfigureAwait(false);
        }
        finally
        {
            stack.Remove(table);
        }

        return total;
    }

    private async Task NotifyAsync(IReadOnlyList<GuildDataRetention> batch, long rows)
    {
        if (creds.GuildJoinsChannelId is 0 || batch.Count == 0)
            return;

        try
        {
            if (await client.Rest.GetChannelAsync(creds.GuildJoinsChannelId).ConfigureAwait(false) is not
                RestTextChannel channel)
                return;

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Server Data Purged")
                .AddField("Servers", batch.Count, true)
                .AddField("Rows Removed", rows, true)
                .AddField("Sources", string.Join(", ", batch.GroupBy(x => x.Source)
                    .Select(g => $"{g.Key} {g.Count()}")), true);

            var listed = batch.Take(15)
                .Select(x => $"{(string.IsNullOrWhiteSpace(x.GuildName) ? "Unknown" : x.GuildName)} `{x.GuildId}`");
            var description = string.Join("\n", listed);
            if (batch.Count > 15)
                description += $"\n... and {batch.Count - 15} more";
            eb.WithDescription(description);

            await channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to post purge notification for {Count} guilds", batch.Count);
        }
    }

    private bool AllShardsConnected()
    {
        return client.Shards.Count > 0 && client.Shards.All(s => s.ConnectionState == ConnectionState.Connected);
    }

    /// <summary>
    ///     Reads the guild scoped tables and the foreign keys between public tables. Cached for the process
    ///     lifetime because the schema only changes on restart when migrations run.
    /// </summary>
    private async Task<SchemaSnapshot> GetSchemaAsync()
    {
        if (schema is not null)
            return schema;

        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);

        var tables = await db.QueryToListAsync<GuildTableRow>(
            """
            SELECT c.table_name AS "Table", c.data_type AS "DataType", GREATEST(pc.reltuples, 0)::float8 AS "EstimatedRows"
            FROM information_schema.columns c
                     JOIN information_schema.tables t
                          ON t.table_schema = c.table_schema AND t.table_name = c.table_name
                     JOIN pg_class pc ON pc.relname = c.table_name
                     JOIN pg_namespace pn ON pn.oid = pc.relnamespace AND pn.nspname = c.table_schema
            WHERE c.table_schema = 'public'
              AND c.column_name = 'GuildId'
              AND t.table_type = 'BASE TABLE'
            ORDER BY c.table_name
            """).ConfigureAwait(false);

        var references = await db.QueryToListAsync<ForeignKeyRow>(
            """
            SELECT child.relname  AS "ChildTable",
                   catt.attname   AS "ChildColumn",
                   parent.relname AS "ParentTable",
                   patt.attname   AS "ParentColumn",
                   con.confdeltype AS "DeleteAction"
            FROM pg_constraint con
                     JOIN pg_class child ON child.oid = con.conrelid
                     JOIN pg_class parent ON parent.oid = con.confrelid
                     JOIN pg_namespace n ON n.oid = child.relnamespace
                     JOIN pg_attribute catt ON catt.attrelid = con.conrelid AND catt.attnum = con.conkey[1]
                     JOIN pg_attribute patt ON patt.attrelid = con.confrelid AND patt.attnum = con.confkey[1]
            WHERE con.contype = 'f'
              AND n.nspname = 'public'
              AND array_length(con.conkey, 1) = 1
            """).ConfigureAwait(false);

        var guildTables = tables
            .Where(t => !ExcludedTables.Contains(t.Table) &&
                        !ExcludedPrefixes.Any(p => t.Table.StartsWith(p, StringComparison.Ordinal)))
            .ToList();

        schema = new SchemaSnapshot(guildTables, references);
        logger.LogInformation("Guild data retention tracking {Tables} guild scoped tables and {Fks} foreign keys",
            guildTables.Count, references.Count);
        return schema;
    }

    private static string Quote(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"")}\"";
    }

    private sealed class GuildTableRow
    {
        public string Table { get; } = string.Empty;
        public string DataType { get; } = string.Empty;
        public double EstimatedRows { get; set; }
    }

    private sealed class ForeignKeyRow
    {
        public string ChildTable { get; } = string.Empty;
        public string ChildColumn { get; } = string.Empty;
        public string ParentTable { get; } = string.Empty;
        public string ParentColumn { get; } = string.Empty;
        public char DeleteAction { get; set; }
    }

    private sealed class SchemaSnapshot
    {
        private readonly ILookup<string, ForeignKeyRow> byParent;
        private readonly Dictionary<string, string> columnTypes;

        public SchemaSnapshot(List<GuildTableRow> guildTables, List<ForeignKeyRow> references)
        {
            GuildTables = guildTables.Select(t => t.Table).ToList();
            ScanTables = guildTables.Where(t => t.EstimatedRows <= ScanRowLimit).Select(t => t.Table).ToList();
            columnTypes = guildTables.ToDictionary(t => t.Table, t => t.DataType, StringComparer.Ordinal);
            byParent = references
                .Where(fk => fk.ChildTable != fk.ParentTable && fk.DeleteAction != 'n')
                .ToLookup(fk => fk.ParentTable, StringComparer.Ordinal);
        }

        public List<string> GuildTables { get; }

        /// <summary>
        ///     The subset of <see cref="GuildTables" /> small enough for the orphan scan to read distinct guild ids from.
        /// </summary>
        public List<string> ScanTables { get; }

        /// <summary>
        ///     The SQL type to cast the guild id parameter to for <paramref name="table" />, so the comparison
        ///     matches the column type and can use its index. Mixed numeric and bigint columns exist mid-migration.
        /// </summary>
        public string GuildIdType(string table)
        {
            return columnTypes.GetValueOrDefault(table) == "bigint" ? "bigint" : "numeric";
        }

        /// <summary>
        ///     Foreign keys pointing at <paramref name="table" /> whose rows have to go before its own. Cascading
        ///     references are included because a cascade can itself be blocked further down the chain.
        /// </summary>
        public IEnumerable<ForeignKeyRow> BlockingReferences(string table)
        {
            return byParent[table];
        }
    }
}