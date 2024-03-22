using System.Diagnostics;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using Swan;

namespace Mewdeko.Common.Attributes.TextCommands;

/// <summary>
/// Attribute to define a rate limit for a command or method.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RatelimitAttribute : PreconditionAttribute
{
    /// <summary>
    /// Initializes a new instance of the RatelimitAttribute class.
    /// </summary>
    /// <param name="seconds">The rate limit in seconds.</param>
    public RatelimitAttribute(int seconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(seconds);

        Seconds = seconds;
    }

    /// <summary>
    /// Gets the rate limit in seconds.
    /// </summary>
    public int Seconds { get; }

    /// <summary>
    /// Checks the permissions of the command or method before execution.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="command">The command being executed.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the precondition result.</returns>
    public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command,
        IServiceProvider services)
    {
        var credService = services.GetRequiredService<IBotCredentials>();
        if (credService.IsOwner(context.User))
            return Task.FromResult(PreconditionResult.FromSuccess());
        if (Seconds == 0)
            return Task.FromResult(PreconditionResult.FromSuccess());
        var cache = services.GetService<IDataCache>();
        Debug.Assert(cache != null, $"{nameof(cache)} != null");
        var rem = cache.TryAddRatelimit(context.User.Id, command.Name, Seconds);

        if (rem is null || rem == TimeSpan.Zero)
            return Task.FromResult(PreconditionResult.FromSuccess());

        var msgContent = $"You can use this command again <t:{(DateTime.Now + rem.Value).ToUnixEpochDate()}:R>.";

        return Task.FromResult(PreconditionResult.FromError(msgContent));
    }
}