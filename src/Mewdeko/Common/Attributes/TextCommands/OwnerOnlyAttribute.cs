using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Common.Attributes.TextCommands;

/// <summary>
/// Attribute to restrict command or method execution to the bot owner.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class OwnerOnlyAttribute : PreconditionAttribute
{
    /// <summary>
    /// Gets or sets a value indicating whether the command or method is restricted to the bot owner.
    /// </summary>
    public bool IsOwnerOnly { get; set; } = true;

    /// <summary>
    /// Initializes a new instance of the OwnerOnlyAttribute class with default settings.
    /// </summary>
    public OwnerOnlyAttribute() { }

    /// <summary>
    /// Initializes a new instance of the OwnerOnlyAttribute class with a specified owner-only setting.
    /// </summary>
    /// <param name="isOwnerOnly">A value indicating whether the command or method is restricted to the bot owner.</param>
    public OwnerOnlyAttribute(bool isOwnerOnly) => IsOwnerOnly = isOwnerOnly;

    /// <summary>
    /// Checks the permissions of the command or method before execution.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="executingCommand">The command being executed.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the precondition result.</returns>
    public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context,
        CommandInfo executingCommand, IServiceProvider services)
    {
        var creds = services.GetService<IBotCredentials>();

        return Task.FromResult(
            creds != null && (creds.IsOwner(context.User) || context.Client.CurrentUser.Id == context.User.Id)
                ? PreconditionResult.FromSuccess()
                : PreconditionResult.FromError(
                    "Not owner\nYou can host your own version of Mewdeko by following the instructions at https://github.com/sylveondeko/Mewdeko\nOr if you don't have anywhere to host it you can subscribe to our ko-fi at https://ko-fi.com/mewdeko"));
    }
}