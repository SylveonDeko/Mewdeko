using Discord;
using Discord.Commands;
using Mewdeko.Modules.Administration.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Common.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class ChatTriggerPermCheck : PreconditionAttribute
{
    public ChatTriggerPermCheck(GuildPermission permission) => UserPermissionAttribute = new RequireUserPermissionAttribute(permission);

    public ChatTriggerPermCheck(ChannelPermission permission) => UserPermissionAttribute = new RequireUserPermissionAttribute(permission);

    public RequireUserPermissionAttribute UserPermissionAttribute { get; }

    public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command,
        IServiceProvider services)
    {
        var creds = services.GetService<IBotCredentials>();
        if (context.Channel is IDMChannel)
        {
            return Task.FromResult(!creds.IsOwner(context.User) ? PreconditionResult.FromError("You must be a bot owner to add global Chat Triggers!") : PreconditionResult.FromSuccess());
        }
        var permService = services.GetService<DiscordPermOverrideService>();
        return permService.TryGetOverrides(context.Guild?.Id ?? 0, command.Name.ToUpperInvariant(), out var _)
            ? Task.FromResult(PreconditionResult.FromSuccess())
            : UserPermissionAttribute.CheckPermissionsAsync(context, command, services);
    }
}