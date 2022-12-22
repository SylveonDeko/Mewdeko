using System.Diagnostics;
using System.Threading.Tasks;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using Swan;

namespace Mewdeko.Common.Attributes.InteractionCommands;

[AttributeUsage(AttributeTargets.Method)]
public sealed class InteractionRatelimitAttribute : PreconditionAttribute
{
    public InteractionRatelimitAttribute(int seconds)
    {
        if (seconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(seconds));

        Seconds = seconds;
    }

    public int Seconds { get; }

    public override Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
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
            IComponentInteractionData compData => cache.TryAddRatelimit(context.User.Id, $"app_command.{compData.CustomId.Split('$')[0]}", Seconds),
            IApplicationCommandInteractionData intData => cache.TryAddRatelimit(context.User.Id, $"app_command.{intData.Id}", Seconds),
            _ => null
        };
        if (rem == null)
            return Task.FromResult(PreconditionResult.FromSuccess());

        var msgContent = $"You can use this command again <t:{(DateTime.Now + rem.Value).ToUnixEpochDate()}:R>.";

        return Task.FromResult(PreconditionResult.FromError(msgContent));
    }
}