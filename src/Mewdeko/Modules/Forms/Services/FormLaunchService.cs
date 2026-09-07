using System.Threading;
using LinqToDB;
using LinqToDB.Async;
using Microsoft.Extensions.Hosting;

namespace Mewdeko.Modules.Forms.Services;

/// <summary>
///     Posts a form's launch announcement once its scheduled opening time has passed, and closes
///     forms whose expiry has passed.
/// </summary>
/// <remarks>
///     Whether a form accepts a response is decided when someone tries to submit, so nothing here
///     has to run on time for a scheduled form to actually open. This only sends the announcement
///     and tidies the active flag, which is why a late run is harmless and a missed one only delays
///     a message.
/// </remarks>
public class FormLaunchService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);

    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<FormLaunchService> logger;
    private readonly FormsService service;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FormLaunchService" /> class.
    /// </summary>
    /// <param name="dbFactory">Provider for database connections.</param>
    /// <param name="service">The forms service, which owns the announcement itself.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public FormLaunchService(
        IDataConnectionFactory dbFactory,
        FormsService service,
        ILogger<FormLaunchService> logger)
    {
        this.dbFactory = dbFactory;
        this.service = service;
        this.logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(StartupDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await AnnounceDueFormsAsync(stoppingToken);
                await CloseExpiredFormsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error running the scheduled form check");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    /// <summary>
    ///     Announces every form whose opening time has passed and that has not been announced yet.
    ///     The announcement itself lives on the service, because publishing a form by hand sends
    ///     the same message and the two must not drift apart.
    /// </summary>
    private async Task AnnounceDueFormsAsync(CancellationToken stoppingToken)
    {
        await using var db = await dbFactory.CreateConnectionAsync(stoppingToken);

        var now = DateTime.UtcNow;

        var due = await db.Forms
            .Where(f => f.OpensAt != null
                        && f.OpensAt <= now
                        && f.AnnouncedAt == null
                        && f.AnnounceChannelId != null
                        && !f.IsDraft)
            .Select(f => f.Id)
            .ToListAsync(stoppingToken);

        foreach (var formId in due)
            await service.AnnounceLaunchAsync(formId);
    }

    /// <summary>
    ///     Marks forms inactive once they close, so a closed form reads as closed on the dashboard
    ///     rather than looking open while quietly refusing every response.
    /// </summary>
    private async Task CloseExpiredFormsAsync(CancellationToken stoppingToken)
    {
        await using var db = await dbFactory.CreateConnectionAsync(stoppingToken);

        var now = DateTime.UtcNow;

        var closed = await db.Forms
            .Where(f => f.IsActive && f.ExpiresAt != null && f.ExpiresAt <= now)
            .Set(f => f.IsActive, false)
            .Set(f => f.UpdatedAt, now)
            .UpdateAsync(stoppingToken);

        if (closed > 0)
            logger.LogInformation("Closed {Count} expired forms", closed);
    }
}