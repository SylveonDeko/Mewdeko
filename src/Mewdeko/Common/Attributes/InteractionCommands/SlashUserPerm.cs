using System.Threading.Tasks;
using Discord.Interactions;
using Mewdeko.Modules.Administration.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Common.Attributes.InteractionCommands;

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
            return await UserPermissionAttribute.CheckRequirementsAsync(context, command, services);
        }

        if (permResult)
            if (!((IGuildUser)context.User).GuildPermissions.Has(perm))
                return PreconditionResult.FromError($"You need the `{perm}` permission to use this command.");
        return await UserPermissionAttribute.CheckRequirementsAsync(context, command, services);
    }
}