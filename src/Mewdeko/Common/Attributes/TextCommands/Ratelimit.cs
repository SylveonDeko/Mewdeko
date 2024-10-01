using System.Diagnostics;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using Swan;
using ZiggyCreatures.Caching.Fusion;

namespace Mewdeko.Common.Attributes.TextCommands;

/// <summary>
///     Attribute to define a rate limit for a command or method.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RatelimitAttribute : PreconditionAttribute
{
    /// <summary>
    ///     Initializes a new instance of the RatelimitAttribute class.
    /// </summary>
    /// <param name="seconds">The rate limit in seconds.</param>
    public RatelimitAttribute(int seconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(seconds);
        Seconds = seconds;
    }

    /// <summary>
    ///     Gets the rate limit in seconds.
    /// </summary>
    public int Seconds { get; }

    /// <summary>
    ///     Checks the permissions of the command or method before execution.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="command">The command being executed.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the precondition result.</returns>
    public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command,
        IServiceProvider services)
    {
        var credService = services.GetRequiredService<IBotCredentials>();
        if (credService.IsOwner(context.User))
            return PreconditionResult.FromSuccess();
        if (Seconds == 0)
            return PreconditionResult.FromSuccess();

        var cache = services.GetService<IFusionCache>();
        Debug.Assert(cache != null, $"{nameof(cache)} != null");

        var cacheKey = $"{context.User.Id}_{command.Name}_ratelimit";
        var rem = await TryAddRatelimitAsync(cache, cacheKey, Seconds);

        if (rem == null || rem == TimeSpan.Zero)
            return PreconditionResult.FromSuccess();

        var msgContent = $"You can use this command again <t:{(DateTime.Now + rem.Value).ToUnixEpochDate()}:R>.";
        return PreconditionResult.FromError(msgContent);
    }

    private async Task<TimeSpan?> TryAddRatelimitAsync(IFusionCache cache, string cacheKey, int expireIn)
    {
        var existingRatelimit = await cache.GetOrSetAsync<TimeSpan?>(cacheKey,
            async _ => TimeSpan.FromSeconds(expireIn), options => options.SetDuration(TimeSpan.FromSeconds(expireIn)));

        return existingRatelimit == TimeSpan.FromSeconds(expireIn) ? null : existingRatelimit;
    }
}