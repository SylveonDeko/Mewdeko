using Discord.Interactions;
using Mewdeko.Modules.Administration.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Common.Attributes.InteractionCommands;

/// <summary>
/// Attribute to check user permissions before executing a command or method.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class SlashUserPermAttribute : PreconditionAttribute
{
    /// <summary>
    /// Initializes a new instance of the SlashUserPermAttribute class with guild permissions.
    /// </summary>
    /// <param name="permission">The required guild permission.</param>
    public SlashUserPermAttribute(GuildPermission permission) =>
        UserPermissionAttribute = new RequireUserPermissionAttribute(permission);

    /// <summary>
    /// Initializes a new instance of the SlashUserPermAttribute class with channel permissions.
    /// </summary>
    /// <param name="permission">The required channel permission.</param>
    public SlashUserPermAttribute(ChannelPermission permission) =>
        UserPermissionAttribute = new RequireUserPermissionAttribute(permission);

    /// <summary>
    /// Gets the user permission attribute.
    /// </summary>
    public RequireUserPermissionAttribute UserPermissionAttribute { get; }

    /// <summary>
    /// Checks the requirements before executing a command or method.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="command">The command being executed.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the precondition result.</returns>
    public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context,
        ICommandInfo command,
        IServiceProvider services)
    {
        // Get the permission service.
        var permService = services.GetService<DiscordPermOverrideService>();

        // Try to get the permission overrides.
        var permResult =
            permService.TryGetOverrides(context.Guild?.Id ?? 0, command.Name.ToUpperInvariant(), out var perm);

        // If the module name is "chattriggers", check if the user is a bot owner or has the required permissions.
        if (command.Module.Name.ToLower() == "chattriggers")
        {
            var creds = services.GetService<IBotCredentials>();
            if (context.Channel is IDMChannel)
            {
                return !creds.IsOwner(context.User)
                    ? PreconditionResult.FromError("You must be a bot owner to add global Chat Triggers!")
                    : PreconditionResult.FromSuccess();
            }

            // If the user does not have the required permissions, return an error.
            if (permResult)
                if (!((IGuildUser)context.User).GuildPermissions.Has(perm))
                    return PreconditionResult.FromError($"You need the `{perm}` permission to use this command.");
            return await UserPermissionAttribute.CheckRequirementsAsync(context, command, services);
        }

        // If the user does not have the required permissions, return an error.
        if (permResult)
            if (!((IGuildUser)context.User).GuildPermissions.Has(perm))
                return PreconditionResult.FromError($"You need the `{perm}` permission to use this command.");
        return await UserPermissionAttribute.CheckRequirementsAsync(context, command, services);
    }
}