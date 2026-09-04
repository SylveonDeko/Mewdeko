using System.Threading;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Extensions.Hosting;

namespace Mewdeko.Services;

/// <summary>
///     Background service that enforces a retention window on the "MessageTimestamps" table.
/// </summary>
/// <remarks>
///     The table had grown unbounded since 2025-04-23 to 47.27M rows and 7.5 GB, roughly 75% of the entire
///     database, while no consumer reads a timestamp older than 30 days. The server has no pg_cron, so the sweep
///     runs here instead of in the database.
/// </remarks>
public class MessageTimestampRetentionService : BackgroundService
{
    /// <summary>
    ///     Rows removed per statement. Keeping batches small bounds lock duration and WAL generation, which matters
    ///     for the first sweep since it has years of backlog to work through.
    /// </summary>
    private const int BatchSize = 20000;

    /// <summary>
    ///     Deletes by <c>ctid</c> rather than by key: script 202 drops the never-scanned primary key on
    ///     "MessageTimestamps", and the physical row address needs no index of its own. The subquery is served by
    ///     "IX_MessageTimestamps_Timestamp" from script 201.
    /// </summary>
    private const string DeleteBatchSql =
        """
        DELETE FROM "MessageTimestamps"
        WHERE ctid IN (SELECT ctid
                       FROM "MessageTimestamps"
                       WHERE "Timestamp" < @cutoff
                       LIMIT @batchSize)
        """;

    /// <summary>
    ///     How much history is kept. Every analytical query in <see cref="Modules.Utility.Services.MessageCountService" />
    ///     clamps its own window to 30 days, so this leaves three times the widest window any caller can ask for.
    /// </summary>
    public static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(90);

    /// <summary>
    ///     Pause between batches, so a large backlog is drained without starving normal traffic.
    /// </summary>
    private static readonly TimeSpan BatchDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    ///     Gap between sweeps.
    /// </summary>
    private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(6);

    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<MessageTimestampRetentionService> logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MessageTimestampRetentionService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="logger">The logger.</param>
    public MessageTimestampRetentionService(
        IDataConnectionFactory dbFactory,
        ILogger<MessageTimestampRetentionService> logger)
    {
        this.dbFactory = dbFactory;
        this.logger = logger;
    }

    /// <summary>
    ///     Runs a retention sweep on startup and then once per <see cref="SweepInterval" />.
    /// </summary>
    /// <param name="stoppingToken">Token signalled when the host is shutting down.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Message timestamp retention service started, keeping {Days} days",
            RetentionPeriod.TotalDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error sweeping expired message timestamps");
            }

            try
            {
                await Task.Delay(SweepInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Message timestamp retention service stopped");
    }

    /// <summary>
    ///     Deletes expired rows in batches until none are left or the host shuts down.
    /// </summary>
    /// <param name="stoppingToken">Token signalled when the host is shutting down.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task SweepAsync(CancellationToken stoppingToken)
    {
        var cutoff = DateTime.UtcNow - RetentionPeriod;
        var total = 0L;

        while (!stoppingToken.IsCancellationRequested)
        {
            await using var db = await dbFactory.CreateConnectionAsync(stoppingToken).ConfigureAwait(false);

            var deleted = await db.ExecuteAsync(
                DeleteBatchSql,
                new DataParameter("cutoff", cutoff, DataType.DateTime),
                new DataParameter("batchSize", BatchSize, DataType.Int32)).ConfigureAwait(false);

            if (deleted <= 0)
                break;

            total += deleted;

            await Task.Delay(BatchDelay, stoppingToken).ConfigureAwait(false);
        }

        if (total > 0)
            logger.LogInformation("Removed {Count} message timestamps older than {Cutoff:u}", total, cutoff);
    }
}