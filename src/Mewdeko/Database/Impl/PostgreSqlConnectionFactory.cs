using System.Threading;
using LinqToDB;
using LinqToDB.DataProvider.PostgreSQL;
using Mewdeko.Database.DbContextStuff;
using Npgsql;

namespace Mewdeko.Database.Impl;

/// <summary>
///     Implementation of <see cref="IDataConnectionFactory" /> for PostgreSQL database, creating <see cref="MewdekoDb" />
///     instances.
/// </summary>
public class PostgreSqlConnectionFactory : IDataConnectionFactory
{
    /// <summary>
    ///     Upper bound on pooled connections. The server allows 400 in total and that budget is shared with the
    ///     other bots on the same instance, so the pool is capped well below the default of 100.
    /// </summary>
    private const int MaxPoolSize = 50;

    /// <summary>
    ///     Connections kept open while idle, so a burst of traffic does not pay connection setup cost.
    /// </summary>
    private const int MinPoolSize = 5;

    /// <summary>
    ///     SQL dialect level used against the production server, which runs PostgreSQL 17.6.
    /// </summary>
    /// <remarks>
    ///     linq2db exposes no <c>v17</c> member; the enum jumps from <c>v15</c> straight to <c>v18</c>. This is the
    ///     highest level the server can actually satisfy, because <c>v18</c> would emit PostgreSQL 18 syntax that
    ///     17.6 rejects. Raise this to <c>v18</c> only after the server itself is upgraded.
    /// </remarks>
    private const PostgreSQLVersion ServerDialect = PostgreSQLVersion.v15;

    private readonly DataOptions dataOptions;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PostgreSqlConnectionFactory" /> class.
    /// </summary>
    /// <param name="connectionString">The connection string to the PostgreSQL database.</param>
    public PostgreSqlConnectionFactory(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            throw new ArgumentNullException(nameof(connectionString));

        dataOptions = new DataOptions()
            .UsePostgreSQL(BuildConnectionString(connectionString), ServerDialect);
    }

    /// <summary>
    ///     Creates a new instance of <see cref="MewdekoDb" /> data connection.
    /// </summary>
    /// <returns>A new instance of a <see cref="MewdekoDb" /> data connection.</returns>
    public MewdekoDb CreateConnection()
    {
        return new MewdekoDb(dataOptions);
    }

    /// <summary>
    ///     Creates a new instance of <see cref="MewdekoDb" /> data connection asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains a new instance of
    ///     <see cref="MewdekoDb" />.
    /// </returns>
    public Task<MewdekoDb> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateConnection());
    }

    /// <summary>
    ///     Applies the pooling and error-reporting settings this bot requires on top of the configured connection
    ///     string, overriding them if they were set elsewhere.
    /// </summary>
    /// <remarks>
    ///     <c>IncludeErrorDetail</c> is forced off because it puts parameter values into exception messages, which
    ///     then reach the logs and Sentry and leak user data out of the database.
    /// </remarks>
    /// <param name="connectionString">The configured connection string.</param>
    /// <returns>The connection string with the required settings applied.</returns>
    private static string BuildConnectionString(string connectionString)
    {
        return new NpgsqlConnectionStringBuilder(connectionString)
        {
            IncludeErrorDetail = false, Pooling = true, MaxPoolSize = MaxPoolSize, MinPoolSize = MinPoolSize
        }.ConnectionString;
    }
}