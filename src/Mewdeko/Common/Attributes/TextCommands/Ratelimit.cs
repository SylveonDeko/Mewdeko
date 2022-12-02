using System.Diagnostics;
using System.Threading.Tasks;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using Swan;

namespace Mewdeko.Common.Attributes.TextCommands;

[AttributeUsage(AttributeTargets.Method)]
public sealed class RatelimitAttribute : PreconditionAttribute
{
    public RatelimitAttribute(int seconds)
    {
        if (seconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(seconds));

        Seconds = seconds;
    }

    public int Seconds { get; }

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