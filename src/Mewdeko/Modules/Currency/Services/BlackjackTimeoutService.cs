using System.Threading;
using Microsoft.Extensions.Hosting;

namespace Mewdeko.Modules.Currency.Services;

/// <summary>
///     Moves blackjack tables on when their current phase runs out of time, including tables left over from before
///     a restart: the joining window closes and the cards are dealt, open insurance offers are declined, and the
///     player whose turn it is stands.
/// </summary>
/// <remarks>
///     Each step goes through the same versioned step a button press uses, so a press and the timeout landing
///     together cannot both apply.
/// </remarks>
public class BlackjackTimeoutService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);

    private readonly GuildOwnershipService ownership;
    private readonly ILogger<BlackjackTimeoutService> logger;
    private readonly BlackjackService service;

    /// <summary>
    ///     Initializes a new instance of the <see cref="BlackjackTimeoutService" /> class.
    /// </summary>
    /// <param name="service">The blackjack service that runs the steps.</param>
    /// <param name="ownership">The guild ownership service.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public BlackjackTimeoutService(
        BlackjackService service,
        GuildOwnershipService ownership,
        ILogger<BlackjackTimeoutService> logger)
    {
        this.service = service;
        this.ownership = ownership;
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
                foreach (var (guildId, channelId) in await service.ListDueAsync())
                {
                    if (ownership.IsMine(guildId))
                        await StepAsync(guildId, channelId);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error moving blackjack tables on");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task StepAsync(ulong guildId, ulong channelId)
    {
        try
        {
            var session = await service.ExpireAsync(guildId, channelId);

            if (session is null)
                return;

            await service.UpdateMessageAsync(session);
            await service.AnnounceTurnAsync(session);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error moving the blackjack table in {ChannelId} of {GuildId} on", channelId,
                guildId);
        }
    }
}
