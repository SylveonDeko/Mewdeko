using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Modules.Permissions.Services;
using Mewdeko.Services.strings.impl;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Common.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class CheckPermissions : PreconditionAttribute
{
    public override Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context,
        ICommandInfo executingCommand, IServiceProvider services)
    {
        var perms = services.GetService<PermissionService>();
        var cmhandl = services.GetService<CommandHandler>();
        string groupname = executingCommand.Module.SlashGroupName;
        if (executingCommand.MethodName == "StealEmotes")
            groupname = "servermanagement";
        if (executingCommand.Module.SlashGroupName?.ToLower() == "snipe")
            groupname = "utility";
        var pc = perms.GetCacheFor(context.Guild.Id);
        int index = 0;
        return Task.FromResult(
            pc.Permissions != null && pc.Permissions.CheckSlashPermissions(groupname, executingCommand.MethodName.ToLower(), context.User, context.Channel, out index)
                ? PreconditionResult.FromSuccess()
                : PreconditionResult.FromError(perms._strings.GetText("perm_prevent", context.Guild.Id, index + 1,
                    Format.Bold(pc.Permissions[index].GetCommand(cmhandl.GetPrefix(context.Guild), context.Guild as SocketGuild)))));
    }
}