using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Common.Attributes.InteractionCommands;

/// <summary>
///     Attribute to restrict the execution of a command or method to the bot owner only.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class SlashOwnerOnlyAttribute : PreconditionAttribute
{
    /// <summary>
    ///     Checks the requirements before executing a command or method.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="executingCommand">The command being executed.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the precondition result.</returns>
    public override Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context,
        ICommandInfo executingCommand, IServiceProvider services)
    {
        // Get the bot credentials service.
        var creds = services.GetService<IBotCredentials>();

        // If the user is the bot owner or the bot itself, return success.
        // Otherwise, return an error with a message.
        return Task.FromResult(
            creds != null && (creds.IsOwner(context.User) || context.Client.CurrentUser.Id == context.User.Id)
                ? PreconditionResult.FromSuccess()
                : PreconditionResult.FromError(
                    "Not owner\nYou can host your own version of Mewdeko by following the instructions at https://github.com/sylveondeko/Mewdeko\nOr if you don't have anywhere to host it you can subscribe to our ko-fi at https://ko-fi.com/mewdeko"));
    }
}