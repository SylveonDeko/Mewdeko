using System.Data;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database;
using Mewdeko.Database.DbContextStuff;
using Npgsql;
using NpgsqlTypes;
using StackExchange.Redis;

namespace Mewdeko.Modules.FluxerMigration.Services;

/// <summary>
///     Exports a server's data from the Postgres schema into Redis so it can be redeemed on Fluxer, and issues the
///     codes used to link a Discord account to its Fluxer account.
/// </summary>
/// <param name="dbFactory">The connection factory, used for its connection string.</param>
/// <param name="cache">The data cache that exposes the shared Redis connection.</param>
/// <param name="client">The Discord client, used to answer dashboard requests.</param>
/// <param name="logger">The logger.</param>
public class FluxerMigrationService(
    IDataConnectionFactory dbFactory,
    IDataCache cache,
    DiscordShardedClient client,
    ILogger<FluxerMigrationService> logger) : INService, IReadyExecutor
{
    private const string RequestChannel = "mewdeko_migration_requests";
    private const string ReplyChannel = "mewdeko_migration_replies";
    private static readonly TimeSpan ReplyLifetime = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<ulong, Lazy<Task<ExportResult>>> runningExports = new();
    private int subscribed;

    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 8;
    private const int ExportVersion = 1;

    private static readonly TimeSpan ExportLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(15);

    private static readonly string[] GuildColumnNames = ["guildid", "guild_id", "serverid"];
    private static readonly string[] ChannelColumnNames = ["channelid", "channel_id"];

    private static readonly string[] ExcludedTables =
    [
        "__EFMigrationsHistory", "AuthCodes", "BotInstances", "Blacklist", "RotatingStatus", "OwnerOnly",
        "GuildSnapshots", "ServerRecoveryStore"
    ];

    private static readonly (string Child, string ChildColumn, string Parent, string ParentColumn)[] SoftRelations =
    [
        ("GiveawayUsers", "GiveawayId", "Giveaways", "Id"),
        ("XpCompetitionEntries", "CompetitionId", "XpCompetitions", "Id"),
        ("XpCompetitionRewards", "CompetitionId", "XpCompetitions", "Id"),
        ("IgnoredLogChannels", "LogSettingId", "LoggingV2", "Id"),
        ("StarboardPosts", "StarboardConfigId", "Starboards", "Id"),
        ("PlaylistSong", "MusicPlaylistId", "MusicPlaylists", "Id"),
        ("Tickets", "PanelId", "TicketPanels", "Id")
    ];

    /// <summary>
    ///     The outcome of an export.
    /// </summary>
    /// <param name="Code">The code the bundle was stored under.</param>
    /// <param name="Tables">How many tables were written.</param>
    /// <param name="Rows">How many rows were written across all tables.</param>
    public record ExportResult(string Code, int Tables, long Rows);

    private record ColumnInfo(string Name, string DataType, string UdtName);

    private record TableInfo(string Name, List<ColumnInfo> Columns);

    private record Relation(string Child, string ChildColumn, string Parent, string ParentColumn);

    private record Filter(string Column, string UdtName, ICollection<object> Values);

    /// <summary>
    ///     Exports everything the bot stores for a server into Redis under a fresh code.
    /// </summary>
    /// <param name="guild">The server to export.</param>
    /// <param name="requestedBy">The id of the user who asked for the export.</param>
    /// <returns>The code and how much was exported.</returns>
    public async Task<ExportResult> ExportAsync(IGuild guild, ulong requestedBy)
    {
        var lazy = runningExports.GetOrAdd(guild.Id,
            _ => new Lazy<Task<ExportResult>>(() => ExportCoreAsync(guild, requestedBy)));
        try
        {
            return await lazy.Value.ConfigureAwait(false);
        }
        finally
        {
            runningExports.TryRemove(new KeyValuePair<ulong, Lazy<Task<ExportResult>>>(guild.Id, lazy));
        }
    }

    /// <summary>
    ///     Starts listening for dashboard requests on Redis.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task OnReadyAsync()
    {
        if (Interlocked.Exchange(ref subscribed, 1) == 1)
            return;

        try
        {
            var queue = await cache.Redis.GetSubscriber()
                .SubscribeAsync(RedisChannel.Literal(RequestChannel)).ConfigureAwait(false);
            queue.OnMessage(message =>
            {
                var payload = (string?)message.Message;
                _ = Task.Run(() => HandleRequestAsync(payload));
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to subscribe to Fluxer migration requests");
        }
    }

    private async Task HandleRequestAsync(string? payload)
    {
        string? id = null;
        try
        {
            if (string.IsNullOrWhiteSpace(payload))
                return;

            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return;

            id = ReadString(root, "id");
            var type = ReadString(root, "type");
            if (!ulong.TryParse(ReadString(root, "discordUserId"), out var userId) || string.IsNullOrEmpty(id))
                return;

            string reply;
            switch (type)
            {
                case "guilds":
                    var guildIds = new List<ulong>();
                    if (root.TryGetProperty("guildIds", out var idArray) && idArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in idArray.EnumerateArray())
                        {
                            if (ulong.TryParse(item.ValueKind == JsonValueKind.String ? item.GetString() : null,
                                    out var guildId))
                                guildIds.Add(guildId);
                        }
                    }

                    reply = await BuildGuildsReplyAsync(userId, guildIds).ConfigureAwait(false);
                    break;
                case "export":
                    if (ReadString(root, "botId") != client.CurrentUser.Id.ToString(CultureInfo.InvariantCulture))
                        return;

                    if (!ulong.TryParse(ReadString(root, "discordGuildId"), out var exportGuildId))
                        return;

                    reply = await BuildExportReplyAsync(userId, exportGuildId).ConfigureAwait(false);
                    break;
                default:
                    return;
            }

            await SendReplyAsync(id, reply).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to handle Fluxer migration request {Id}", id);
        }
    }

    private static string? ReadString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private async Task SendReplyAsync(string id, string reply)
    {
        var key = $"{id}:{client.CurrentUser.Id.ToString(CultureInfo.InvariantCulture)}";
        var redis = cache.Redis.GetDatabase();
        await redis.StringSetAsync($"mewdeko_migration_reply:{key}", reply, ReplyLifetime).ConfigureAwait(false);
        await cache.Redis.GetSubscriber().PublishAsync(RedisChannel.Literal(ReplyChannel), key)
            .ConfigureAwait(false);
    }

    private object BuildBotInfo()
    {
        var user = client.CurrentUser;
        return new
        {
            id = user.Id.ToString(CultureInfo.InvariantCulture),
            name = user.Username,
            avatar = user.GetAvatarUrl()
        };
    }

    private async Task<bool> CanMigrateAsync(SocketGuild guild, ulong userId)
    {
        if (guild.OwnerId == userId)
            return true;

        IGuildUser? member = guild.GetUser(userId);
        member ??= await ((IGuild)guild).GetUserAsync(userId, CacheMode.AllowDownload).ConfigureAwait(false);
        return member?.GuildPermissions.Administrator ?? false;
    }

    private async Task<string> BuildGuildsReplyAsync(ulong userId, List<ulong> guildIds)
    {
        var guilds = new List<object>();
        foreach (var guildId in guildIds.Distinct())
        {
            var guild = client.GetGuild(guildId);
            if (guild is null)
                continue;

            try
            {
                if (!await CanMigrateAsync(guild, userId).ConfigureAwait(false))
                    continue;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not check permissions of {UserId} in guild {GuildId}", userId, guildId);
                continue;
            }

            guilds.Add(new
            {
                id = guild.Id.ToString(CultureInfo.InvariantCulture),
                name = guild.Name,
                icon = guild.IconUrl,
                memberCount = guild.MemberCount
            });
        }

        return JsonSerializer.Serialize(new { bot = BuildBotInfo(), guilds });
    }

    private async Task<string> BuildExportReplyAsync(ulong userId, ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return JsonSerializer.Serialize(new { bot = BuildBotInfo(), error = "not_found" });

        try
        {
            if (!await CanMigrateAsync(guild, userId).ConfigureAwait(false))
                return JsonSerializer.Serialize(new { bot = BuildBotInfo(), error = "not_admin" });

            var result = await ExportAsync(guild, userId).ConfigureAwait(false);
            return JsonSerializer.Serialize(new
            {
                bot = BuildBotInfo(), code = result.Code, tables = result.Tables, rows = result.Rows
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Dashboard export failed for guild {GuildId}", guildId);
            return JsonSerializer.Serialize(new { bot = BuildBotInfo(), error = "failed" });
        }
    }

    private async Task<ExportResult> ExportCoreAsync(IGuild guild, ulong requestedBy)
    {
        var redis = cache.Redis.GetDatabase();
        var code = GenerateCode();
        var prefix = $"mewdeko_migration:{code}:";
        var written = new List<string>();
        var rows = 0L;

        var channels = await guild.GetChannelsAsync().ConfigureAwait(false);
        var channelIds = channels.Select(x => (object)(decimal)x.Id).ToList();

        try
        {
            await using var connection = new NpgsqlConnection(await GetConnectionStringAsync().ConfigureAwait(false));
            await connection.OpenAsync().ConfigureAwait(false);

            var schema = await LoadSchemaAsync(connection).ConfigureAwait(false);
            var relations = await LoadRelationsAsync(connection, schema).ConfigureAwait(false);
            var referenced = relations
                .Select(x => (x.Parent, x.ParentColumn))
                .ToHashSet();
            var captured = new Dictionary<(string Table, string Column), HashSet<object>>();
            var processed = new HashSet<string>(StringComparer.Ordinal);

            async Task ExportTableAsync(TableInfo table, List<Filter> filters)
            {
                processed.Add(table.Name);
                var count = await WriteTableAsync(connection, redis, prefix, table, filters, referenced, captured)
                    .ConfigureAwait(false);
                if (count < 0)
                    return;

                written.Add(table.Name);
                rows += count;
            }

            foreach (var table in schema.Values.OrderBy(x => x.Name, StringComparer.Ordinal))
            {
                var guildColumns = table.Columns
                    .Where(x => IsIdType(x) && GuildColumnNames.Contains(x.Name.ToLowerInvariant()))
                    .ToList();
                if (guildColumns.Count == 0)
                    continue;

                var filters = guildColumns
                    .Select(x => new Filter(x.Name, x.UdtName, new List<object> { (decimal)guild.Id }))
                    .ToList();
                await ExportTableAsync(table, filters).ConfigureAwait(false);
            }

            while (true)
            {
                var groups = relations
                    .Where(x => processed.Contains(x.Parent) && !processed.Contains(x.Child))
                    .GroupBy(x => x.Child)
                    .ToList();
                if (groups.Count == 0)
                    break;

                foreach (var group in groups)
                {
                    var table = schema[group.Key];
                    var filters = new List<Filter>();
                    foreach (var relation in group)
                    {
                        if (!captured.TryGetValue((relation.Parent, relation.ParentColumn), out var values)
                            || values.Count == 0)
                            continue;

                        var column = table.Columns.First(x => x.Name == relation.ChildColumn);
                        filters.Add(new Filter(column.Name, column.UdtName, values));
                    }

                    if (filters.Count == 0)
                    {
                        processed.Add(table.Name);
                        continue;
                    }

                    await ExportTableAsync(table, filters).ConfigureAwait(false);
                }
            }

            foreach (var table in schema.Values.OrderBy(x => x.Name, StringComparer.Ordinal))
            {
                if (processed.Contains(table.Name))
                    continue;

                var channelColumns = table.Columns
                    .Where(x => IsIdType(x) && ChannelColumnNames.Contains(x.Name.ToLowerInvariant()))
                    .ToList();
                if (channelColumns.Count == 0)
                    continue;

                var filters = channelColumns
                    .Select(x => new Filter(x.Name, x.UdtName, channelIds))
                    .ToList();
                await ExportTableAsync(table, filters).ConfigureAwait(false);
            }

            var meta = await BuildMetaAsync(guild, requestedBy, channels, written).ConfigureAwait(false);
            await redis.StringSetAsync($"{prefix}meta", meta, ExportLifetime).ConfigureAwait(false);

            return new ExportResult(code, written.Count, rows);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fluxer migration export failed for guild {GuildId}", guild.Id);
            var keys = written.Select(x => (RedisKey)$"{prefix}table:{x}").Append($"{prefix}meta").ToArray();
            await redis.KeyDeleteAsync(keys).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    ///     Creates a link code for a Discord user and stores it in Redis.
    /// </summary>
    /// <param name="userId">The Discord user id the code resolves to.</param>
    /// <returns>The new code.</returns>
    public async Task<string> CreateLinkCodeAsync(ulong userId)
    {
        var redis = cache.Redis.GetDatabase();
        var code = GenerateCode();
        await redis.StringSetAsync($"mewdeko_link:{code}", userId.ToString(CultureInfo.InvariantCulture),
            LinkLifetime).ConfigureAwait(false);
        return code;
    }

    private async Task<string> GetConnectionStringAsync()
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        return db.ConnectionString;
    }

    private static string GenerateCode()
    {
        var chars = new char[CodeLength];
        for (var i = 0; i < CodeLength; i++)
            chars[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];

        return new string(chars);
    }

    private static bool IsIdType(ColumnInfo column)
    {
        return column.DataType is "numeric" or "bigint";
    }

    private static bool IsExcluded(string table)
    {
        return table.StartsWith("Analytics", StringComparison.OrdinalIgnoreCase)
               || ExcludedTables.Contains(table, StringComparer.OrdinalIgnoreCase);
    }

    private static string Quote(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"")}\"";
    }

    private static async Task<Dictionary<string, TableInfo>> LoadSchemaAsync(NpgsqlConnection connection)
    {
        var tables = new Dictionary<string, TableInfo>(StringComparer.Ordinal);
        await using var command = new NpgsqlCommand(
            """
            select c.table_name, c.column_name, c.data_type, c.udt_name
            from information_schema.columns c
            join information_schema.tables t
              on t.table_schema = c.table_schema and t.table_name = c.table_name
            where c.table_schema = current_schema() and t.table_type = 'BASE TABLE'
            order by c.table_name, c.ordinal_position
            """, connection);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var name = reader.GetString(0);
            if (IsExcluded(name))
                continue;

            if (!tables.TryGetValue(name, out var table))
            {
                table = new TableInfo(name, []);
                tables[name] = table;
            }

            table.Columns.Add(new ColumnInfo(reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        }

        return tables;
    }

    private static async Task<List<Relation>> LoadRelationsAsync(NpgsqlConnection connection,
        Dictionary<string, TableInfo> schema)
    {
        var relations = new List<Relation>();
        await using (var command = new NpgsqlCommand(
                         """
                         select kcu.table_name, kcu.column_name, ccu.table_name, ccu.column_name
                         from information_schema.table_constraints tc
                         join information_schema.key_column_usage kcu
                           on kcu.constraint_name = tc.constraint_name and kcu.constraint_schema = tc.constraint_schema
                         join information_schema.referential_constraints rc
                           on rc.constraint_name = tc.constraint_name and rc.constraint_schema = tc.constraint_schema
                         join information_schema.key_column_usage ccu
                           on ccu.constraint_name = rc.unique_constraint_name
                          and ccu.constraint_schema = rc.unique_constraint_schema
                          and ccu.ordinal_position = kcu.position_in_unique_constraint
                         where tc.constraint_type = 'FOREIGN KEY' and tc.table_schema = current_schema()
                         """, connection))
        await using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
        {
            while (await reader.ReadAsync().ConfigureAwait(false))
                relations.Add(new Relation(reader.GetString(0), reader.GetString(1), reader.GetString(2),
                    reader.GetString(3)));
        }

        foreach (var (child, childColumn, parent, parentColumn) in SoftRelations)
        {
            var childTable =
                schema.Values.FirstOrDefault(x => x.Name.Equals(child, StringComparison.OrdinalIgnoreCase));
            var parentTable =
                schema.Values.FirstOrDefault(x => x.Name.Equals(parent, StringComparison.OrdinalIgnoreCase));
            var childCol = childTable?.Columns.FirstOrDefault(x =>
                x.Name.Equals(childColumn, StringComparison.OrdinalIgnoreCase));
            var parentCol = parentTable?.Columns.FirstOrDefault(x =>
                x.Name.Equals(parentColumn, StringComparison.OrdinalIgnoreCase));
            if (childTable is null || parentTable is null || childCol is null || parentCol is null)
                continue;

            relations.Add(new Relation(childTable.Name, childCol.Name, parentTable.Name, parentCol.Name));
        }

        return relations
            .Where(x => schema.ContainsKey(x.Child) && schema.ContainsKey(x.Parent) && x.Child != x.Parent)
            .DistinctBy(x => (x.Child, x.ChildColumn, x.Parent, x.ParentColumn))
            .ToList();
    }

    private static NpgsqlParameter BuildArrayParameter(string name, string udtName, ICollection<object> values)
    {
        var invariant = CultureInfo.InvariantCulture;
        return udtName switch
        {
            "int2" => new NpgsqlParameter(name, NpgsqlDbType.Array | NpgsqlDbType.Smallint)
                { Value = values.Select(x => Convert.ToInt16(x, invariant)).ToArray() },
            "int4" => new NpgsqlParameter(name, NpgsqlDbType.Array | NpgsqlDbType.Integer)
                { Value = values.Select(x => Convert.ToInt32(x, invariant)).ToArray() },
            "int8" => new NpgsqlParameter(name, NpgsqlDbType.Array | NpgsqlDbType.Bigint)
                { Value = values.Select(x => Convert.ToInt64(x, invariant)).ToArray() },
            "uuid" => new NpgsqlParameter(name, NpgsqlDbType.Array | NpgsqlDbType.Uuid)
                { Value = values.Select(x => x is Guid g ? g : Guid.Parse(x.ToString()!)).ToArray() },
            "text" or "varchar" or "bpchar" => new NpgsqlParameter(name, NpgsqlDbType.Array | NpgsqlDbType.Text)
                { Value = values.Select(x => Convert.ToString(x, invariant) ?? string.Empty).ToArray() },
            _ => new NpgsqlParameter(name, NpgsqlDbType.Array | NpgsqlDbType.Numeric)
                { Value = values.Select(x => Convert.ToDecimal(x, invariant)).ToArray() }
        };
    }

    private static async Task<long> WriteTableAsync(
        NpgsqlConnection connection,
        IDatabase redis,
        string prefix,
        TableInfo table,
        List<Filter> filters,
        HashSet<(string Parent, string ParentColumn)> referenced,
        Dictionary<(string Table, string Column), HashSet<object>> captured)
    {
        var select = string.Join(", ", table.Columns.Select(x => Quote(x.Name)));
        var where = string.Join(" or ", filters.Select((x, i) => $"{Quote(x.Column)} = any(@p{i})"));

        await using var command = new NpgsqlCommand($"select {select} from {Quote(table.Name)} where {where}",
            connection)
        {
            CommandTimeout = 600
        };
        for (var i = 0; i < filters.Count; i++)
            command.Parameters.Add(BuildArrayParameter($"p{i}", filters[i].UdtName, filters[i].Values));

        var captureIndexes = new Dictionary<int, HashSet<object>>();
        for (var i = 0; i < table.Columns.Count; i++)
        {
            var name = table.Columns[i].Name;
            if (!referenced.Contains((table.Name, name)))
                continue;

            var set = new HashSet<object>();
            captured[(table.Name, name)] = set;
            captureIndexes[i] = set;
        }

        using var buffer = new MemoryStream();
        var rowCount = 0L;
        await using (var gzip = new GZipStream(buffer, CompressionLevel.Optimal, true))
        await using (var writer = new Utf8JsonWriter(gzip))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("columns");
            foreach (var column in table.Columns)
                writer.WriteStringValue(column.Name);

            writer.WriteEndArray();
            writer.WriteStartArray("types");
            foreach (var column in table.Columns)
                writer.WriteStringValue(column.UdtName);

            writer.WriteEndArray();
            writer.WriteStartArray("rows");

            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            var values = new object[table.Columns.Count];
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                reader.GetValues(values);
                writer.WriteStartArray();
                for (var i = 0; i < values.Length; i++)
                {
                    WriteValue(writer, values[i]);
                    if (values[i] is not DBNull && captureIndexes.TryGetValue(i, out var set))
                        set.Add(values[i]);
                }

                writer.WriteEndArray();
                rowCount++;
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            await writer.FlushAsync().ConfigureAwait(false);
        }

        if (rowCount == 0)
            return -1;

        await redis.StringSetAsync($"{prefix}table:{table.Name}", buffer.ToArray(), ExportLifetime)
            .ConfigureAwait(false);
        return rowCount;
    }

    private static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null or DBNull:
                writer.WriteNullValue();
                break;
            case decimal or long or ulong or System.Numerics.BigInteger:
                writer.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
            case int i:
                writer.WriteNumberValue(i);
                break;
            case short s:
                writer.WriteNumberValue(s);
                break;
            case byte b:
                writer.WriteNumberValue(b);
                break;
            case float f when float.IsFinite(f):
                writer.WriteNumberValue(f);
                break;
            case double d when double.IsFinite(d):
                writer.WriteNumberValue(d);
                break;
            case float or double:
                writer.WriteNullValue();
                break;
            case bool flag:
                writer.WriteBooleanValue(flag);
                break;
            case string text:
                writer.WriteStringValue(text);
                break;
            case Guid guid:
                writer.WriteStringValue(guid.ToString());
                break;
            case DateTime dateTime:
                writer.WriteStringValue(dateTime.ToString("O", CultureInfo.InvariantCulture));
                break;
            case DateTimeOffset offset:
                writer.WriteStringValue(offset.ToString("O", CultureInfo.InvariantCulture));
                break;
            case DateOnly date:
                writer.WriteStringValue(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                break;
            case TimeSpan span:
                writer.WriteStringValue(span.ToString("c", CultureInfo.InvariantCulture));
                break;
            case byte[] bytes:
                writer.WriteStringValue(Convert.ToBase64String(bytes));
                break;
            case Array array:
                writer.WriteStartArray();
                foreach (var item in array)
                    WriteValue(writer, item);

                writer.WriteEndArray();
                break;
            default:
                writer.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
        }
    }

    private static async Task<byte[]> BuildMetaAsync(IGuild guild, ulong requestedBy,
        IReadOnlyCollection<IGuildChannel> channels, List<string> tables)
    {
        var meta = new
        {
            version = ExportVersion,
            discordGuildId = guild.Id.ToString(CultureInfo.InvariantCulture),
            guildName = guild.Name,
            requestedBy = requestedBy.ToString(CultureInfo.InvariantCulture),
            createdAt = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            channels = channels
                .Where(x => x is not IThreadChannel)
                .Select(x => new
                {
                    id = x.Id.ToString(CultureInfo.InvariantCulture),
                    name = x.Name,
                    type = GetChannelType(x),
                    parentId = (x as INestedChannel)?.CategoryId?.ToString(CultureInfo.InvariantCulture),
                    position = x.Position
                }),
            roles = guild.Roles.Select(x => new
            {
                id = x.Id.ToString(CultureInfo.InvariantCulture),
                name = x.Name,
                position = x.Position,
                color = x.Colors.PrimaryColor.RawValue,
                managed = x.IsManaged
            }),
            emojis = guild.Emotes.Select(x => new
            {
                id = x.Id.ToString(CultureInfo.InvariantCulture),
                name = x.Name,
                animated = x.Animated
            }),
            tables = tables.OrderBy(x => x, StringComparer.Ordinal)
        };

        using var buffer = new MemoryStream();
        await using (var gzip = new GZipStream(buffer, CompressionLevel.Optimal, true))
        {
            await JsonSerializer.SerializeAsync(gzip, meta).ConfigureAwait(false);
        }

        return buffer.ToArray();
    }

    private static int GetChannelType(IGuildChannel channel)
    {
        return channel switch
        {
            IStageChannel => 13,
            IVoiceChannel => 2,
            ICategoryChannel => 4,
            INewsChannel => 5,
            IMediaChannel => 16,
            IForumChannel => 15,
            _ => 0
        };
    }
}
