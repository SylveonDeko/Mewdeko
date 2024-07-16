#nullable enable
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.EntityFrameworkCore;
using Mewdeko.Database.Common;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Database.Extensions;
using Mewdeko.Database.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Migrations;
using Serilog;

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
        //var gc = sourceContext.GuildConfigs.IncludeEverything().AsNoTracking();
        //Log.Information("Copying {Count} entries of {Type} to the new Db...", gc.Count(), gc.GetType());
        var guildConfig = destinationContext.GetTable<GuildConfig>();
        await guildConfig.DeleteAsync();
        //await guildConfig.BulkCopyAsync(options, gc);

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
        await TransferEntityDataAsync<Polls, Polls>(sourceContext, destinationContext, x => x);
        await TransferEntityDataAsync<PollVote, PollVote>(sourceContext, destinationContext, x => x);
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

    /// <summary>
    /// Transfers entity data asynchronously.
    /// </summary>
    /// <typeparam name="T">The source entity type.</typeparam>
    /// <typeparam name="T2">The destination entity type.</typeparam>
    /// <param name="sourceContext">The source context.</param>
    /// <param name="destinationContext">The destination context.</param>
    /// <param name="thing">The transformation function.</param>
    private static async Task TransferEntityDataAsync<T, T2>(DbContext sourceContext, IDataContext destinationContext, Func<T, T2> thing)
        where T : class
    {
        var entities = await sourceContext.Set<T>().AsNoTracking()
            .ToArrayAsync(cancellationToken: CancellationToken.None);
        Log.Information("Copying {Count} entries of {Type} to the new Db...", entities.Length, entities.GetType());
        var destTable = destinationContext.GetTable<T>();
        var options = new BulkCopyOptions
        {
            MaxDegreeOfParallelism = 50, MaxBatchSize = 5000, BulkCopyType = BulkCopyType.ProviderSpecific
        };
        await destTable.DeleteAsync();
        await destTable.BulkCopyAsync(options, entities.DistinctBy(thing));
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
                    pmh.GetMethod("PostMigrationHandler")?.Invoke(null, new object[] { id, context });
                }
            }
        }

        await context.SaveChangesAsync();
    }


}