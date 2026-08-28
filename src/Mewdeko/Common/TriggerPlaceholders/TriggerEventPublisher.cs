using Mewdeko.Modules.Chat_Triggers.Common;
using Mewdeko.Modules.Chat_Triggers.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Common.TriggerPlaceholders;

/// <summary>
///     Lets a module hand an event to the chat trigger system without taking a direct dependency on it.
/// </summary>
/// <remarks>
///     The chat trigger service is resolved on demand rather than injected, so a module can raise trigger events
///     without creating a dependency cycle or paying for the service when nothing listens. A failure to publish is
///     logged and swallowed: a module's own work must never fail because a trigger misbehaved.
/// </remarks>
public sealed class TriggerEventPublisher : INService
{
    private readonly ILogger<TriggerEventPublisher> logger;
    private readonly IServiceProvider services;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TriggerEventPublisher" /> class.
    /// </summary>
    /// <param name="services">The service provider used to resolve the chat trigger service.</param>
    /// <param name="logger">The logger.</param>
    public TriggerEventPublisher(IServiceProvider services, ILogger<TriggerEventPublisher> logger)
    {
        this.services = services;
        this.logger = logger;
    }

    /// <summary>
    ///     Fires the chat triggers listening for an event.
    /// </summary>
    /// <param name="guildId">The guild the event occurred in.</param>
    /// <param name="eventType">The event that occurred.</param>
    /// <param name="user">The user the event concerns.</param>
    /// <param name="channel">The channel to respond in when a trigger does not name one of its own.</param>
    public async Task PublishAsync(ulong guildId, CtEventType eventType, IUser user, IMessageChannel? channel = null)
    {
        try
        {
            var triggers = services.GetService<ChatTriggersService>();
            if (triggers is null)
                return;

            await triggers.FireEventTriggersAsync(guildId, eventType, user, channel).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish {EventType} to chat triggers in {GuildId}", eventType, guildId);
        }
    }
}