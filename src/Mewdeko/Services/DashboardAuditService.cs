using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.Enums;

namespace Mewdeko.Services;

/// <summary>
///     Persists and queries dashboard audit log entries: who accessed the
///     dashboard, what they changed, and what they viewed. Writes come from the
///     dashboard audit action filter; reads come from the audit log controller.
///     A daily timer purges entries past the retention window.
/// </summary>
public class DashboardAuditService(IDataConnectionFactory dbFactory, ILogger<DashboardAuditService> logger)
    : INService, IReadyExecutor, IDisposable
{
    /// <summary>
    ///     How long audit entries are kept before the retention job removes them.
    /// </summary>
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(90);

    private Timer? retentionTimer;
    private bool disposed;

    /// <summary>
    ///     Starts the daily retention job once the bot is ready.
    /// </summary>
    public Task OnReadyAsync()
    {
        retentionTimer = new Timer(
            _ => _ = RunRetentionAsync(),
            null,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromHours(24));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        retentionTimer?.Dispose();
    }

    private async Task RunRetentionAsync()
    {
        try
        {
            var removed = await PurgeOlderThanAsync(DateTime.UtcNow - RetentionPeriod);
            if (removed > 0)
                logger.LogInformation("Dashboard audit log retention removed {Count} expired entries", removed);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Dashboard audit log retention job failed");
        }
    }

    /// <summary>
    ///     Records a single dashboard audit entry. Never throws; a logging failure
    ///     must not break the request that triggered it.
    /// </summary>
    public async Task LogAsync(
        ulong guildId,
        ulong userId,
        string userName,
        AuditAction action,
        string section,
        string endpoint,
        string httpMethod,
        string? changes,
        string? userAgent)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            await db.InsertAsync(new DashboardAuditLog
            {
                GuildId = guildId,
                UserId = userId,
                UserName = userName,
                Action = action,
                Section = section,
                Endpoint = endpoint,
                HttpMethod = httpMethod,
                Changes = changes,
                UserAgent = userAgent,
                DateAdded = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to write dashboard audit log entry for guild {GuildId}", guildId);
        }
    }

    /// <summary>
    ///     Returns a page of audit entries for a guild, newest first, with optional filters.
    /// </summary>
    /// <returns>The matching page of entries and the total count across all pages.</returns>
    public async Task<(IReadOnlyList<DashboardAuditLog> Items, int Total)> GetForGuildAsync(
        ulong guildId,
        ulong? userId,
        AuditAction? action,
        string? section,
        DateTime? after,
        DateTime? before,
        int page,
        int pageSize)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var query = db.DashboardAuditLogs.Where(x => x.GuildId == guildId);

        if (userId.HasValue)
            query = query.Where(x => x.UserId == userId.Value);
        if (action.HasValue)
            query = query.Where(x => x.Action == action.Value);
        if (!string.IsNullOrWhiteSpace(section))
            query = query.Where(x => x.Section == section);
        if (after.HasValue)
            query = query.Where(x => x.DateAdded >= after.Value);
        if (before.HasValue)
            query = query.Where(x => x.DateAdded <= before.Value);

        var total = await query.CountAsync();

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > 200) pageSize = 200;

        var items = await query
            .OrderByDescending(x => x.DateAdded)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    /// <summary>
    ///     Deletes audit entries older than the given cutoff. Used by the retention job.
    /// </summary>
    /// <returns>The number of entries removed.</returns>
    public async Task<int> PurgeOlderThanAsync(DateTime cutoff)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.DashboardAuditLogs
            .Where(x => x.DateAdded < cutoff)
            .DeleteAsync();
    }
}
