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

    public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo command,
        IServiceProvider services)
    {
        var permService = services.GetService<DiscordPermOverrideService>();
        Debug.Assert(permService != null, $"{nameof(permService)} != null");
        if (permService.TryGetOverrides(context.Guild?.Id ?? 0, command.MethodName.ToUpperInvariant(), out var a))
        {
            var user = await context.Guild.GetUserAsync(context.User.Id);
            Debug.Assert(a != null, nameof(a) + " != null");
            if (!user.GuildPermissions.Has((GuildPermission)a))
                return PreconditionResult.FromError($"You need the `{a}` permission to use this command.");
        }
        else
        {
            return await UserPermissionAttribute.CheckRequirementsAsync(context, command, services);
        }
        return PreconditionResult.FromSuccess();
    }
}