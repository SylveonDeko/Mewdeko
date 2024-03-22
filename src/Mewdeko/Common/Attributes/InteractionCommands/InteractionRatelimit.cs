using System.Diagnostics;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using Swan;

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
    public int Seconds { get; }

    /// <summary>
    /// Checks the requirements before executing a command or method.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="commandInfo">The command being executed.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the precondition result.</returns>
    public override Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context,
        ICommandInfo commandInfo, IServiceProvider services)
    {
        var credService = services.GetRequiredService<IBotCredentials>();
        if (credService.IsOwner(context.User))
            return Task.FromResult(PreconditionResult.FromSuccess());
        if (Seconds == 0)
            return Task.FromResult(PreconditionResult.FromSuccess());

        var cache = services.GetService<IDataCache>();
        Debug.Assert(cache != null, $"{nameof(cache)} != null");
        var rem = context.Interaction.Data switch
        {
            IComponentInteractionData compData => cache.TryAddRatelimit(context.User.Id,
                $"app_command.{compData.CustomId.Split('$')[0]}", Seconds),
            IApplicationCommandInteractionData intData => cache.TryAddRatelimit(context.User.Id,
                $"app_command.{intData.Id}", Seconds),
            _ => null
        };
        if (rem == null)
            return Task.FromResult(PreconditionResult.FromSuccess());

        var msgContent = $"You can use this command again <t:{(DateTime.Now + rem.Value).ToUnixEpochDate()}:R>.";

        return Task.FromResult(PreconditionResult.FromError(msgContent));
    }
}