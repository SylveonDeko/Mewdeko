using Discord.Interactions;
using Mewdeko.Modules.Permissions.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Common.Attributes.InteractionCommands;

/// <summary>
/// Attribute to check if a user has the required role to execute a command or method.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class PermRoleCheck : PreconditionAttribute
{
    /// <summary>
    /// Checks the requirements before executing a command or method.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="executingCommand">The command being executed.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the precondition result.</returns>
    public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context,
        ICommandInfo executingCommand, IServiceProvider services)
    {
        // Get the permission service.
        var perms = services.GetService<PermissionService>();

        // Get the permission cache for the guild.
        var cache = await perms.GetCacheFor(context.Guild.Id);

        // Get the user as a guild user.
        var user = context.User as IGuildUser;

        // Get the permission role from the cache.
        var permRole = cache.PermRole;

        // Try to parse the permission role as a ulong.
        if (!ulong.TryParse(permRole, out var rid))
            rid = 0;

        string returnMsg;
        IRole role;

        // If the permission role is not set or the role does not exist in the guild, check if the user has administrator permissions.
        if (string.IsNullOrWhiteSpace(permRole) || (role = context.Guild.GetRole(rid)) == null)
        {
            // If the user has administrator permissions, return success.
            if (user.GuildPermissions.Administrator)
                return PreconditionResult.FromSuccess();

            // If the user does not have administrator permissions, return an error.
            returnMsg = "You need Admin permissions in order to use permission commands.";
            return PreconditionResult.FromError(returnMsg);
        }

        // If the user does not have the required role, return an error.
        if (!user.RoleIds.Contains(rid))
        {
            returnMsg = $"You need the {Format.Bold(role.Name)} role in order to use permission commands.";
            return PreconditionResult.FromError(returnMsg);
        }

        // If the user has the required role, return success.
        return PreconditionGroupResult.FromSuccess();
    }
}