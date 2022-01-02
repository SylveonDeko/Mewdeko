using System.Diagnostics;
using Discord;
using Discord.Commands;
using Mewdeko.Modules.Administration.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Common.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class UserPermAttribute : PreconditionAttribute
{
    public UserPermAttribute(GuildPermission permission) => UserPermissionAttribute = new RequireUserPermissionAttribute(permission);

    public UserPermAttribute(ChannelPermission permission) => UserPermissionAttribute = new RequireUserPermissionAttribute(permission);

    public RequireUserPermissionAttribute UserPermissionAttribute { get; }

    public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command,
        IServiceProvider services)
    {
        var permService = services.GetService<DiscordPermOverrideService>();
        Debug.Assert(permService != null, nameof(permService) + " != null");
        return permService.TryGetOverrides(context.Guild?.Id ?? 0, command.Name.ToUpperInvariant(), out var _)
            ? Task.FromResult(PreconditionResult.FromSuccess())
            : UserPermissionAttribute.CheckPermissionsAsync(context, command, services);
    }
}