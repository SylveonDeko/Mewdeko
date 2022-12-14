using System.Threading.Tasks;
using Discord.Interactions;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Modules.Permissions.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Common.Attributes.InteractionCommands;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class CheckPermissions : PreconditionAttribute
{
    public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context,
        ICommandInfo executingCommand, IServiceProvider services)
    {
        if (context.Guild is null) return PreconditionResult.FromSuccess();
        var commandname = executingCommand.MethodName.ToLower() switch
        {
            "addhighlight" when executingCommand.Module.SlashGroupName == "highlights" => "highlights",
            "listhighlights" when executingCommand.Module.SlashGroupName == "highlights" => "highlights",
            "deletehighlight" when executingCommand.Module.SlashGroupName == "highlights" => "highlights",
            "matchhighlight" when executingCommand.Module.SlashGroupName == "highlights" => "highlights",
            "toggleuser" when executingCommand.Module.SlashGroupName == "highlights" => "highlights",
            "togglechannel" when executingCommand.Module.SlashGroupName == "highlights" => "highlights",
            "toggleglobal" when executingCommand.Module.SlashGroupName == "highlights" => "highlights",
            _ => executingCommand.MethodName.ToLower()
        };
        var perms = services.GetService<PermissionService>();
        var guildSettingsService = services.GetService<GuildSettingsService>();
        var groupname = executingCommand.MethodName switch
        {
            "Confess" => "Confessions",
            "StealEmotes" => "servermanagement",
            _ => executingCommand.Module.SlashGroupName
        };
        if (executingCommand.Module.SlashGroupName?.ToLower() == "snipe")
            groupname = "utility";
        var pc = await perms.GetCacheFor(context.Guild.Id);
        var index = 0;
        return
            pc.Permissions != null && pc.Permissions.CheckSlashPermissions(groupname, commandname, context.User, context.Channel, out index)
                ? PreconditionResult.FromSuccess()
                : PreconditionResult.FromError(perms.Strings.GetText("perm_prevent", context.Guild.Id, index + 1,
                    Format.Bold(pc.Permissions[index].GetCommand(await guildSettingsService.GetPrefix(context.Guild), context.Guild as SocketGuild))));
    }
}