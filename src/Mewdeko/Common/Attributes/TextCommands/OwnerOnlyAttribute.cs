using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Common.Attributes.TextCommands;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class OwnerOnlyAttribute : PreconditionAttribute
{
    public bool IsOwnerOnly { get; set; } = true;

    public OwnerOnlyAttribute() { }
    public OwnerOnlyAttribute(bool isOwnerOnly) => IsOwnerOnly = isOwnerOnly;

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