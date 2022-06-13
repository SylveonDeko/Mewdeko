using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

namespace Mewdeko.Common.Attributes.SlashCommands;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class SlashOwnerOnlyAttribute : PreconditionAttribute
{
    public override Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context,
        ICommandInfo executingCommand, IServiceProvider services)
    {
        var creds = services.GetService<IBotCredentials>();

        return Task.FromResult(
            creds != null && (creds.IsOwner(context.User) || context.Client.CurrentUser.Id == context.User.Id)
                ? PreconditionResult.FromSuccess()
                : PreconditionResult.FromError("Not owner"));
    }
}