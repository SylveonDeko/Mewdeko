using Discord.Interactions;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Modules.Permissions.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Common.Attributes.InteractionCommands;

/// <summary>
///     Checks the guild permission system before executing a slash or context command. The command is resolved to
///     its text command identity first, so rules written against text commands and modules apply here too.
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
    /// <returns>The precondition result.</returns>
    public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context,
        ICommandInfo executingCommand, IServiceProvider services)
    {
        if (context.Guild is null) return PreconditionResult.FromSuccess();

        var perms = services.GetRequiredService<PermissionService>();
        var guildSettingsService = services.GetRequiredService<GuildSettingsService>();
        var identity = services.GetRequiredService<SlashCommandIdentityService>().Resolve(executingCommand);

        var pc = await perms.GetCacheFor(context.Guild.Id);
        if (pc.Permissions is null)
            return PreconditionResult.FromSuccess();

        return pc.Permissions.CheckSlashPermissions(identity.ModuleName, identity.Alias, context.User,
            context.Channel, out var index)
            ? PreconditionResult.FromSuccess()
            : PreconditionResult.FromError(perms.Strings.PermPrevent(context.Guild.Id, index + 1,
                Format.Bold(pc.Permissions[index].GetCommand(await guildSettingsService.GetPrefix(context.Guild),
                    context.Guild as SocketGuild))));
    }
}