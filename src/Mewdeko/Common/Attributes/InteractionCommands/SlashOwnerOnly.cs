using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Common.Attributes.InteractionCommands;

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
                : PreconditionResult.FromError(
                    "Not owner\nYou can host your own version of Mewdeko by following the instructions at https://github.com/sylveondeko/Mewdeko\nOr if you don't have anywhere to host it you can subscribe to our ko-fi at https://ko-fi.com/mewdeko"));
    }
}