using System.Threading.Tasks;
using Discord.Interactions;
using Mewdeko.Modules.Permissions.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Common.Attributes.InteractionCommands;
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class PermRoleCheck : PreconditionAttribute
{
    public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context,
        ICommandInfo executingCommand, IServiceProvider services)
    {
        var perms = services.GetService<PermissionService>();
        var cache = await perms.GetCacheFor(context.Guild.Id);

        var user = context.User as IGuildUser;
        var permRole = cache.PermRole;
        if (!ulong.TryParse(permRole, out var rid))
            rid = 0;
        string returnMsg;
        IRole role;
        if (string.IsNullOrWhiteSpace(permRole) || (role = context.Guild.GetRole(rid)) == null)
        {
            if (user.GuildPermissions.Administrator)
                return PreconditionResult.FromSuccess();
            returnMsg = "You need Admin permissions in order to use permission commands.";
            return PreconditionResult.FromError(returnMsg);
        }

        if (!user.RoleIds.Contains(rid))
        {
            returnMsg = $"You need the {Format.Bold(role.Name)} role in order to use permission commands.";
            return PreconditionResult.FromError(returnMsg);
        }
        return PreconditionGroupResult.FromSuccess();
    }
}