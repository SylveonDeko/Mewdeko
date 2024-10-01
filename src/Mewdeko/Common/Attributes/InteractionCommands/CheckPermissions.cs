using Discord.Interactions;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Modules.Permissions.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Common.Attributes.InteractionCommands;

/// <summary>
///     Attribute to check permissions before executing a command or method.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class CheckPermissions : PreconditionAttribute
{
    /// <summary>
    ///     Checks the requirements before executing a command or method.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="executingCommand">The command being executed.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the precondition result.</returns>
    public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context,
        ICommandInfo executingCommand, IServiceProvider services)
    {
        // If the context does not have a guild, return success.
        if (context.Guild is null) return PreconditionResult.FromSuccess();

        // Determine the command name based on the method name and group name.
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

        // Get the permission service and guild settings service.
        var perms = services.GetService<PermissionService>();
        var guildSettingsService = services.GetService<GuildSettingsService>();

        // Determine the group name based on the method name and group name.
        var groupname = executingCommand.MethodName switch
        {
            "Confess" => "Confessions",
            "StealEmotes" => "servermanagement",
            _ => executingCommand.Module.SlashGroupName
        };

        // If the group name is "snipe", set it to "utility".
        if (executingCommand.Module.SlashGroupName?.ToLower() == "snipe")
            groupname = "utility";

        // Get the permission cache for the guild.
        var pc = await perms.GetCacheFor(context.Guild.Id);

        // Check the permissions and return the result.
        var index = 0;
        return
            pc.Permissions != null &&
            pc.Permissions.CheckSlashPermissions(groupname, commandname, context.User, context.Channel, out index)
                ? PreconditionResult.FromSuccess()
                : PreconditionResult.FromError(perms.Strings.GetText("perm_prevent", context.Guild.Id, index + 1,
                    Format.Bold(pc.Permissions[index].GetCommand(await guildSettingsService.GetPrefix(context.Guild),
                        context.Guild as SocketGuild))));
    }
}