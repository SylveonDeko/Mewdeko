using DbUp;
using DbUp.Engine;

namespace Mewdeko.Database;

/// <summary>
///     Database upgrade service using DbUp
/// </summary>
public class DatabaseUpgrader
{
    private readonly string connectionString;

    /// <summary>
    ///     Initializes a new instance of the DatabaseUpgrader
    /// </summary>
    /// <param name="connectionString">Database connection string</param>
    public DatabaseUpgrader(string connectionString)
    {
        this.connectionString = connectionString;
    }

    /// <summary>
    ///     Checks if database upgrade is required
    /// </summary>
    /// <returns>True if upgrade is needed</returns>
    public bool IsUpgradeRequired()
    {
        return BuildUpgrader().IsUpgradeRequired() || BuildConcurrentUpgrader().IsUpgradeRequired();
    }

    /// <summary>
    ///     Gets the list of scripts that will be executed
    /// </summary>
    /// <returns>List of scripts to execute</returns>
    public IEnumerable<string> GetScriptsToExecute()
    {
        return BuildUpgrader().GetScriptsToExecute().Select(x => x.Name)
            .Concat(BuildConcurrentUpgrader().GetScriptsToExecute().Select(x => x.Name));
    }

    /// <summary>
    ///     Tests database connection
    /// </summary>
    /// <returns>True if connection is successful</returns>
    public bool TestConnection()
    {
        var upgrader = BuildUpgrader();
        return upgrader.TryConnect(out _);
    }

    /// <summary>
    ///     Performs database upgrade with embedded SQL scripts
    /// </summary>
    /// <returns>Upgrade result with success status and error information</returns>
    public DatabaseUpgradeResult PerformUpgrade()
    {
        // Transactional scripts run first so that any schema they introduce exists before the
        // concurrent lane (indexes) references it.
        var result = BuildUpgrader().PerformUpgrade();
        if (!result.Successful)
            return result;

        return BuildConcurrentUpgrader().PerformUpgrade();
    }

    /// <summary>
    ///     Marks scripts as executed without running them (useful for syncing environments)
    /// </summary>
    /// <returns>True if operation was successful</returns>
    public bool MarkAsExecuted()
    {
        return BuildUpgrader().MarkAsExecuted().Successful
               && BuildConcurrentUpgrader().MarkAsExecuted().Successful;
    }

    /// <summary>
    ///     Builds the DbUp upgrader with configuration
    /// </summary>
    /// <returns>Configured upgrade engine</returns>
    private UpgradeEngine BuildUpgrader()
    {
        return DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(typeof(DatabaseUpgrader).Assembly, x => !IsConcurrentScript(x))
            .WithTransaction() // Single transaction across the whole upgrade run
            .LogToConsole()
            .Build();
    }

    /// <summary>
    ///     Builds an upgrader for scripts that cannot run inside a transaction.
    /// </summary>
    /// <remarks>
    ///     PostgreSQL forbids <c>CREATE INDEX CONCURRENTLY</c>, <c>DROP INDEX CONCURRENTLY</c>,
    ///     <c>REINDEX CONCURRENTLY</c> and <c>VACUUM</c> inside a transaction block, so these scripts
    ///     get their own journal and run with transactions disabled. Because there is no transaction,
    ///     each script must be individually re-runnable: a failed <c>CREATE INDEX CONCURRENTLY</c>
    ///     leaves an INVALID index behind, which the scripts drop before recreating.
    /// </remarks>
    /// <returns>Configured upgrade engine for non-transactional scripts.</returns>
    private UpgradeEngine BuildConcurrentUpgrader()
    {
        return DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(typeof(DatabaseUpgrader).Assembly, IsConcurrentScript)
            .WithoutTransaction()
            .JournalToPostgresqlTable("public", "schemaversions_concurrent")
            .LogToConsole()
            .Build();
    }

    /// <summary>
    ///     Identifies scripts that must run outside a transaction by their <c>.concurrent.sql</c> suffix.
    /// </summary>
    /// <param name="scriptName">The embedded resource name of the script.</param>
    /// <returns>True when the script must run without a transaction.</returns>
    private static bool IsConcurrentScript(string scriptName)
    {
        return scriptName.EndsWith(".concurrent.sql", StringComparison.OrdinalIgnoreCase);
    }
}