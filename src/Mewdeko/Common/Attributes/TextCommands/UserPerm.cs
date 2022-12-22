using System.Threading.Tasks;
using Discord.Commands;
using Mewdeko.Modules.Administration.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Common.Attributes.TextCommands;

[AttributeUsage(AttributeTargets.Method)]
public class UserPermAttribute : PreconditionAttribute
{
    public UserPermAttribute(GuildPermission permission) => UserPermissionAttribute = new RequireUserPermissionAttribute(permission);

    public UserPermAttribute(ChannelPermission permission) => UserPermissionAttribute = new RequireUserPermissionAttribute(permission);

    public RequireUserPermissionAttribute UserPermissionAttribute { get; }

    public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command,
        IServiceProvider services)
    {
        var permService = services.GetService<DiscordPermOverrideService>();
        var permResult = permService.TryGetOverrides(context.Guild?.Id ?? 0, command.Name.ToUpperInvariant(), out var perm);
        if (command.Module.Name.ToLower() == "chattriggers")
        {
            var creds = services.GetService<IBotCredentials>();
            if (context.Channel is IDMChannel)
            {
                return !creds.IsOwner(context.User) ? PreconditionResult.FromError("You must be a bot owner to add global Chat Triggers!") : PreconditionResult.FromSuccess();
            }

            // ReSharper disable once InvertIf (stupid)
            if (permResult)
                if (!((IGuildUser)context.User).GuildPermissions.Has(perm))
                    return PreconditionResult.FromError($"You need the `{perm}` permission to use this command.");
            return await UserPermissionAttribute.CheckPermissionsAsync(context, command, services);
        }

        if (!permResult) return await UserPermissionAttribute.CheckPermissionsAsync(context, command, services);
        if (!((IGuildUser)context.User).GuildPermissions.Has(perm))
            return PreconditionResult.FromError($"You need the `{perm}` permission to use this command.");
        return await UserPermissionAttribute.CheckPermissionsAsync(context, command, services);
    }
}