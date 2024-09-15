#nullable enable
using System.Reflection;
using System.Text;
using System.Threading;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.EntityFrameworkCore;
using LinqToDB.Mapping;
using Mewdeko.Database.Common;
using Mewdeko.Database.DbContextStuff;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Serilog;
using ConnectionState = System.Data.ConnectionState;

namespace Mewdeko.Database;

/// <summary>
/// Service for handling database migrations.
/// </summary>
public class MigrationService
{
    private readonly string token;
    private readonly DbContextProvider provider;

    /// <summary>
    /// Initializes a new instance of the <see cref="MigrationService"/> class.
    /// </summary>
    public MigrationService(DbContextProvider? provider, string? token, string psqlConnection, bool migrate = false)
    {
        this.provider = provider;
        this.token = token ?? "";
        LinqToDBForEFTools.Initialize();

        if (string.IsNullOrEmpty(psqlConnection))
        {
            throw new ArgumentException("PostgreSQL connection string must be provided.");
        }


        var builder = new DbContextOptionsBuilder()
            .UseNpgsql(psqlConnection)
            .EnableDetailedErrors()
            .EnableSensitiveDataLogging();

        if (migrate)
        {
            MigrateDataAsync().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Builds the SQLite connection string.
    /// </summary>
    /// <param name="token">The token to find the client ID.</param>
    /// <returns>The SQLite connection string.</returns>
    private static string BuildSqliteConnectionString(string token)
    {
        var folderpath = Environment.GetFolderPath(Environment.OSVersion.Platform == PlatformID.Unix
            ? Environment.SpecialFolder.UserProfile
            : Environment.SpecialFolder.ApplicationData);
        var tokenPart = token.Split(".")[0];
        var paddingNeeded = 28 - tokenPart.Length;
        if (paddingNeeded > 0 && tokenPart.Length % 4 != 0)
        {
            tokenPart = tokenPart.PadRight(28, '=');
        }

        var clientId = Encoding.UTF8.GetString(Convert.FromBase64String(tokenPart));

        var builder = new SqliteConnectionStringBuilder("Data Source=data/Mewdeko.db");
            if (Environment.OSVersion.Platform == PlatformID.Unix)
                builder.DataSource = builder.DataSource =
                    folderpath + $"/.local/share/Mewdeko/{clientId}/data/Mewdeko.db";
            else
                builder.DataSource = builder.DataSource = folderpath + $"/Mewdeko/{clientId}/data/Mewdeko.db";

        return builder.ToString();
    }

    private async Task MigrateDataAsync()
    {
        // Initialize destination context
        await using var destCont = new MewdekoPostgresContext(new DbContextOptions<MewdekoPostgresContext>());
        var destinationContext = destCont.CreateLinqToDBConnection();

        await using var sourceContext = new MewdekoSqLiteContext(BuildSqliteConnectionString(token));
        await ApplyMigrations(sourceContext);
        await ApplyMigrations(destCont);
        var options = new BulkCopyOptions
        {
            MaxDegreeOfParallelism = 50, MaxBatchSize = 5000, BulkCopyType = BulkCopyType.ProviderSpecific
        };
        Log.Information("Starting Data Migration...");
        await destinationContext.ExecuteAsync("SET session_replication_role = replica;");
        var gc = sourceContext.GuildConfigs.IncludeEverything().AsNoTracking();
        Log.Information("Copying {Count} entries of {Type} to the new Db...", gc.Count(), gc.GetType());
        var guildConfig = destinationContext.GetTable<GuildConfig>();
        await guildConfig.DeleteAsync();
        await guildConfig.BulkCopyAsync(options, gc);

        await TransferEntityDataAsync<Afk, Afk>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<AntiRaidSetting, AntiRaidSetting>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<AntiSpamSetting, AntiSpamSetting>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<AntiAltSetting, AntiAltSetting>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<AntiSpamIgnore, AntiSpamIgnore>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<AutoBanRoles, AutoBanRoles>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<AutoBanEntry, AutoBanEntry>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<AutoCommand, AutoCommand>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<AutoPublish, AutoPublish>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<BanTemplate, BanTemplate>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<BlacklistEntry, BlacklistEntry>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<ChatTriggers, ChatTriggers>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<CommandAlias, CommandAlias>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<CommandStats, CommandStats>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<Confessions, Confessions>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<DiscordPermOverride, DiscordPermOverride>(sourceContext, destinationContext,
            x => x);
        await TransferEntityDataAsync<DiscordUser, DiscordUser>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<FeedSub, FeedSub>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<Giveaways, Giveaways>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<SelfAssignedRole, SelfAssignedRole>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<Highlights, Highlights>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<HighlightSettings, HighlightSettings>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<JoinLeaveLogs, JoinLeaveLogs>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<MultiGreet, MultiGreet>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<MusicPlaylist, MusicPlaylist>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<MusicPlayerSettings, MusicPlayerSettings>(sourceContext, destinationContext,
            x => x);
        await TransferEntityDataAsync<MutedUserId, MutedUserId>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<GlobalUserBalance, GlobalUserBalance>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<GuildUserBalance, GuildUserBalance>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<OwnerOnly, OwnerOnly>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<PlaylistSong, PlaylistSong>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<PublishUserBlacklist, PublishUserBlacklist>(sourceContext, destinationContext,
            x => x);
        await TransferEntityDataAsync<PublishWordBlacklist, PublishWordBlacklist>(sourceContext, destinationContext,
            x => x);
        await TransferEntityDataAsync<Quote, Quote>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<Reminder, Reminder>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<RoleGreet, RoleGreet>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<RoleStateSettings, RoleStateSettings>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<RotatingPlayingStatus, RotatingPlayingStatus>(sourceContext, destinationContext,
            x => x);
        await TransferEntityDataAsync<ServerRecoveryStore, ServerRecoveryStore>(sourceContext, destinationContext,
            x => x);
        await TransferEntityDataAsync<StarboardPosts, StarboardPosts>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<StatusRolesTable, StatusRolesTable>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<SuggestionsModel, SuggestionsModel>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<SuggestThreads, SuggestThreads>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<SuggestVotes, SuggestVotes>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<UnbanTimer, UnbanTimer>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<UnmuteTimer, UnmuteTimer>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<UnroleTimer, UnroleTimer>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<UserRoleStates, UserRoleStates>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<UserXpStats, (ulong, ulong)>(sourceContext, destinationContext,
            x => (x.UserId, x.GuildId));
        await TransferEntityDataAsync<VcRoleInfo, VcRoleInfo>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<VoteRoles, VoteRoles>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<Models.Votes, Models.Votes>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<Warning, Warning>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<Warning2, Warning2>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<WarningPunishment, WarningPunishment>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<WarningPunishment2, WarningPunishment2>(sourceContext, destinationContext,
            x => x);

        await destinationContext.ExecuteAsync("SET session_replication_role = default;");

        Log.Warning(
            "Copy Complete. Please make sure to set MigrateToPsql to false in credentials to make sure your data wont get overwritten");
    }

    private static async Task TransferEntityDataAsync<T, TKey>(
        DbContext sourceContext,
        IDataContext destinationContext,
        Func<T, TKey> keySelector)
        where T : class, new()
    {
        // Get the table name from the entity type
        var tableNameAttribute = typeof(T).GetCustomAttribute<TableAttribute>();
        var tableName = tableNameAttribute != null ? tableNameAttribute.Name : typeof(T).Name;

        // Get the list of columns in the source database
        var sourceColumns = new HashSet<string>();
        await using (var command = sourceContext.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = $"PRAGMA table_info('{tableName}');";
            await sourceContext.Database.OpenConnectionAsync();
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var columnName = reader.GetString(1); // Column 1 is 'name'
                    sourceColumns.Add(columnName);
                }
            }

            await sourceContext.Database.CloseConnectionAsync();
        }

        // Build a SQL query that selects only the existing columns
        var columnList = string.Join(", ", sourceColumns);
        var sql = $"SELECT {columnList} FROM {tableName}";

        var entities = new List<T>();
        var entityProperties = typeof(T).GetProperties()
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name, p => p);

        await using (var command = sourceContext.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = sql;
            if (sourceContext.Database.GetDbConnection().State != ConnectionState.Open)
                await sourceContext.Database.OpenConnectionAsync();

            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    // Use a dictionary to store column data dynamically
                    var data = new Dictionary<string, object>();
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        var columnName = reader.GetName(i);
                        var value = reader.GetValue(i);
                        if (value != DBNull.Value)
                        {
                            data[columnName] = value;
                        }
                    }

                    var entity = new T();
                    foreach (var kvp in data)
                    {
                        if (!entityProperties.TryGetValue(kvp.Key, out var prop)) continue;
                        // Handle type conversion if necessary
                        try
                        {
                            var convertedValue = Convert.ChangeType(kvp.Value, prop.PropertyType);
                            prop.SetValue(entity, convertedValue);
                        }
                        catch
                        {
                            // Handle or log the error as needed
                        }
                    }

                    entities.Add(entity);
                }
            }

            await sourceContext.Database.CloseConnectionAsync();
        }

        Log.Information("Copying {Count} entries of {Type} to the new Db...", entities.Count, typeof(T).Name);
        var destTable = destinationContext.GetTable<T>();
        var options = new BulkCopyOptions
        {
            MaxDegreeOfParallelism = 50, MaxBatchSize = 5000, BulkCopyType = BulkCopyType.ProviderSpecific
        };
        await destTable.DeleteAsync();

        await destTable.BulkCopyAsync(options, entities.DistinctBy(keySelector));
        Log.Information("Copied");
    }

    private async Task TransferGuildConfigDataAsync(
    string sourceConnectionString,
    IDataContext destinationContext)
    {
        await using var connection = new SqliteConnection(sourceConnectionString);
        await connection.OpenAsync();

        const string tableName = "GuildConfigs";

        // Get the list of columns in the source database
        var sourceColumns = new HashSet<string>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $"PRAGMA table_info('{tableName}');";
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var columnName = reader.GetString(1); // Column 1 is 'name'
                    sourceColumns.Add(columnName);
                }
            }
        }

        // Build a SQL query that selects only the existing columns
        var columnList = string.Join(", ", sourceColumns.Select(c => $"\"{c}\""));
        var sql = $"SELECT {columnList} FROM \"{tableName}\"";

        var entities = new List<GuildConfig>();
        var entityProperties = typeof(GuildConfig).GetProperties()
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name, p => p);

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = sql;

            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var data = new Dictionary<string, object>();
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        var columnName = reader.GetName(i);
                        var value = reader.GetValue(i);
                        if (value != DBNull.Value)
                        {
                            data[columnName] = value;
                        }
                    }

                    var entity = new GuildConfig();
                    foreach (var kvp in data)
                    {
                        if (entityProperties.TryGetValue(kvp.Key, out var prop))
                        {
                            try
                            {
                                var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                                var convertedValue = Convert.ChangeType(kvp.Value, targetType);
                                prop.SetValue(entity, convertedValue);
                            }
                            catch (Exception ex)
                            {
                                // Log the error or handle it accordingly
                                Log.Warning("Could not set property {PropertyName}: {Exception}", prop.Name, ex.Message);
                            }
                        }
                    }
                    entities.Add(entity);
                }
            }
        }

        await connection.CloseAsync();

        Log.Information("Copying {Count} entries of {Type} to the new Db...", entities.Count, typeof(GuildConfig).Name);

        var destTable = destinationContext.GetTable<GuildConfig>();
        var options = new BulkCopyOptions
        {
            MaxDegreeOfParallelism = 50,
            MaxBatchSize = 5000,
            BulkCopyType = BulkCopyType.ProviderSpecific
        };
        await destTable.DeleteAsync();

        await destTable.BulkCopyAsync(options, entities);
        Log.Information("Copied");
    }


    /// <summary>
    /// Applies migrations to the database context.
    /// </summary>
    /// <param name="context">The database context.</param>
    public async Task ApplyMigrations(DbContext? context = null)
    {
        context ??= await provider.GetContextAsync();
        var toApply = (await context.Database.GetPendingMigrationsAsync().ConfigureAwait(false)).ToList();
        if (toApply.Count != 0)
        {
            await context.Database.MigrateAsync().ConfigureAwait(false);
            await context.SaveChangesAsync().ConfigureAwait(false);

            var env = Assembly.GetExecutingAssembly();
            var pmhs = env.GetTypes().Where(t => t.GetInterfaces().Any(i => i == typeof(IPostMigrationHandler)))
                .ToList();
            foreach (var id in toApply)
            {
                var pmhToRuns = pmhs.Where(pmh => pmh.GetCustomAttribute<MigrationAttribute>()?.Id == id).ToList();
                foreach (var pmh in pmhToRuns)
                {
                    pmh.GetMethod("PostMigrationHandler")?.Invoke(null, [id, context]);
                }
            }
        }

        await context.SaveChangesAsync();
    }


}