using Discord.Commands;
using Mewdeko.Modules.Utility.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Common.Attributes.TextCommands;

/// <summary>
///     Requires Manage Server or the guild's auto reaction manager role.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class AutoReactManagerAttribute : PreconditionAttribute
{
    private readonly UserPermAttribute manageGuild = new(GuildPermission.ManageGuild);

    /// <inheritdoc />
    public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command,
        IServiceProvider services)
    {
        var result = await manageGuild.CheckPermissionsAsync(context, command, services);
        if (result.IsSuccess)
            return result;

        if (context.User is IGuildUser user &&
            await services.GetRequiredService<AutoReactionService>().CanManageAsync(user))
            return PreconditionResult.FromSuccess();

        return result;
    }
}
