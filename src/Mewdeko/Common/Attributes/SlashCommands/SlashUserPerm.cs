using Discord.Interactions;
using Mewdeko.Modules.Administration.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Mewdeko.Common.Attributes.SlashCommands;

[AttributeUsage(AttributeTargets.Method)]
public class SlashUserPermAttribute : PreconditionAttribute
{
    public SlashUserPermAttribute(GuildPermission permission) => UserPermissionAttribute = new RequireUserPermissionAttribute(permission);

    public SlashUserPermAttribute(ChannelPermission permission) => UserPermissionAttribute = new RequireUserPermissionAttribute(permission);

    public RequireUserPermissionAttribute UserPermissionAttribute { get; }

    public override Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo command,
        IServiceProvider services)
    {
        var permService = services.GetService<DiscordPermOverrideService>();
        Debug.Assert(permService != null, $"{nameof(permService)} != null");
        return permService.TryGetOverrides(context.Guild?.Id ?? 0, command.MethodName.ToUpperInvariant(), out var _)
            ? Task.FromResult(PreconditionResult.FromSuccess())
            : UserPermissionAttribute.CheckRequirementsAsync(context, command, services);
    }
}