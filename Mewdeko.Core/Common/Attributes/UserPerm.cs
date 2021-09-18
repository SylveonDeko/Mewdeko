using System;
using System.Threading.Tasks;
using Discord.Commands;
using Mewdeko.Modules.Administration.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Discord
{
    [AttributeUsage(AttributeTargets.Method)]
    public class UserPermAttribute : PreconditionAttribute
    {
        public UserPermAttribute(GuildPerm permission)
        {
            UserPermissionAttribute = new RequireUserPermissionAttribute((GuildPermission)permission);
        }

        public UserPermAttribute(ChannelPerm permission)
        {
            UserPermissionAttribute = new RequireUserPermissionAttribute((ChannelPermission)permission);
        }

        public RequireUserPermissionAttribute UserPermissionAttribute { get; }

        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command,
            IServiceProvider services)
        {
            var permService = services.GetService<DiscordPermOverrideService>();
            if (permService.TryGetOverrides(context.Guild?.Id ?? 0, command.Name.ToUpperInvariant(), out var _))
                return Task.FromResult(PreconditionResult.FromSuccess());

            return UserPermissionAttribute.CheckPermissionsAsync(context, command, services);
        }
    }
}