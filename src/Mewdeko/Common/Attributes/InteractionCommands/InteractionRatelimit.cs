using System.Diagnostics;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using Swan;
using ZiggyCreatures.Caching.Fusion;

namespace Mewdeko.Common.Attributes.InteractionCommands;

/// <summary>
/// Attribute to apply a rate limit to an interaction command.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class InteractionRatelimitAttribute : PreconditionAttribute
{
    /// <summary>
    /// Initializes a new instance of the InteractionRatelimitAttribute class.
    /// </summary>
    /// <param name="seconds">The rate limit duration in seconds.</param>
    public InteractionRatelimitAttribute(int seconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(seconds);
        Seconds = seconds;
    }

    /// <summary>
    /// Gets the rate limit duration in seconds.
    /// </summary>
    private int Seconds { get; }

    /// <summary>
    /// Checks the requirements before executing a command or method.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="commandInfo">The command being executed.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the precondition result.</returns>
    public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context,
        ICommandInfo commandInfo, IServiceProvider services)
    {
        var credService = services.GetRequiredService<IBotCredentials>();
        if (credService.IsOwner(context.User))
            return PreconditionResult.FromSuccess();
        if (Seconds == 0)
            return PreconditionResult.FromSuccess();

        var cache = services.GetService<IFusionCache>();
        Debug.Assert(cache != null, $"{nameof(cache)} != null");

        var cacheKey = context.Interaction.Data switch
        {
            IComponentInteractionData compData => $"app_command.{compData.CustomId.Split('$')[0]}_{context.User.Id}",
            IApplicationCommandInteractionData intData => $"app_command.{intData.Id}_{context.User.Id}",
            _ => null
        };

        if (cacheKey == null)
            return PreconditionResult.FromSuccess();

        var rem = await TryAddRatelimitAsync(cache, cacheKey, Seconds);

        if (rem == null || rem == TimeSpan.Zero)
            return PreconditionResult.FromSuccess();

        var msgContent = $"You can use this command again <t:{(DateTime.Now + rem.Value).ToUnixEpochDate()}:R>.";
        return PreconditionResult.FromError(msgContent);
    }

    private async Task<TimeSpan?> TryAddRatelimitAsync(IFusionCache cache, string cacheKey, int expireIn)
    {
        var existingRatelimit = await cache.GetOrSetAsync<TimeSpan?>(cacheKey, async _ => TimeSpan.FromSeconds(expireIn), options => options.SetDuration(TimeSpan.FromSeconds(expireIn)));

        return existingRatelimit == TimeSpan.FromSeconds(expireIn) ? null : existingRatelimit;
    }
}
