using Discord.Commands;
using Mewdeko.Modules.Administration.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Common.Attributes.TextCommands;

/// <summary>
/// Attribute to define user permissions for a command or method.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class UserPermAttribute : PreconditionAttribute
{
    /// <summary>
    /// Initializes a new instance of the UserPermAttribute class with a specified guild permission.
    /// </summary>
    /// <param name="permission">The guild permission required to execute the command or method.</param>
    public UserPermAttribute(GuildPermission permission) =>
        UserPermissionAttribute = new RequireUserPermissionAttribute(permission);

    /// <summary>
    /// Initializes a new instance of the UserPermAttribute class with a specified channel permission.
    /// </summary>
    /// <param name="permission">The channel permission required to execute the command or method.</param>
    public UserPermAttribute(ChannelPermission permission) =>
        UserPermissionAttribute = new RequireUserPermissionAttribute(permission);

    /// <summary>
    /// Gets the user permission attribute.
    /// </summary>
    public RequireUserPermissionAttribute UserPermissionAttribute { get; }

    /// <summary>
    /// Checks the permissions of the command or method before execution.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="command">The command being executed.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the precondition result.</returns>
    public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command,
        IServiceProvider services)
    {
        var permService = services.GetService<DiscordPermOverrideService>();
        var permResult =
            permService.TryGetOverrides(context.Guild?.Id ?? 0, command.Name.ToUpperInvariant(), out var perm);
        if (command.Module.Name.Equals("chattriggers", StringComparison.CurrentCultureIgnoreCase))
        {
            var creds = services.GetService<IBotCredentials>();
            if (context.Channel is IDMChannel)
            {
                return !creds.IsOwner(context.User)
                    ? PreconditionResult.FromError("You must be a bot owner to add global Chat Triggers!")
                    : PreconditionResult.FromSuccess();
            }

            // ReSharper disable once InvertIf (stupid)
            if (permResult)
                if (!((IGuildUser)context.User).GuildPermissions.Has(perm))
                    return PreconditionResult.FromError($"You need the `{perm}` permission to use this command.");
            return await UserPermissionAttribute.CheckPermissionsAsync(context, command, services);
        }

        if (!permResult) return await UserPermissionAttribute.CheckPermissionsAsync(context, command, services);
        return !((IGuildUser)context.User).GuildPermissions.Has(perm)
            ? PreconditionResult.FromError($"You need the `{perm}` permission to use this command.")
            : PreconditionResult.FromSuccess();
    }
}