using Discord;
using Discord.Interactions;
using Mewdeko.Modules.Administration.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace Mewdeko.Common.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class InteractionChatTriggerPermCheck : PreconditionAttribute
{
    public InteractionChatTriggerPermCheck(GuildPermission permission) => UserPermissionAttribute = new RequireUserPermissionAttribute(permission);

    public InteractionChatTriggerPermCheck(ChannelPermission permission) => UserPermissionAttribute = new RequireUserPermissionAttribute(permission);

    public RequireUserPermissionAttribute UserPermissionAttribute { get; }

    public override Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
    {
        var creds = services.GetService<IBotCredentials>();
        if (context.Channel is IDMChannel)
        {
            return Task.FromResult(!creds.IsOwner(context.User) ? PreconditionResult.FromError("You must be a bot owner to add global Chat Triggers!") : PreconditionResult.FromSuccess());
        }
        var permService = services.GetService<DiscordPermOverrideService>();
        Debug.Assert(permService != null, $"{nameof(permService)} != null");
        return permService.TryGetOverrides(context.Guild?.Id ?? 0, commandInfo.Name.ToUpperInvariant(), out var _)
            ? Task.FromResult(PreconditionResult.FromSuccess())
            : UserPermissionAttribute.CheckRequirementsAsync(context, commandInfo, services);
    }
}
