using Discord.Interactions;
using Mewdeko.Modules.Utility.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Common.Attributes.InteractionCommands;

/// <summary>
///     Requires Manage Server or the guild's auto reaction manager role.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class SlashAutoReactManagerAttribute : PreconditionAttribute
{
    private readonly SlashUserPermAttribute manageGuild = new(GuildPermission.ManageGuild);

    /// <inheritdoc />
    public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context,
        ICommandInfo command, IServiceProvider services)
    {
        var result = await manageGuild.CheckRequirementsAsync(context, command, services);
        if (result.IsSuccess)
            return result;

        if (context.User is IGuildUser user &&
            await services.GetRequiredService<AutoReactionService>().CanManageAsync(user))
            return PreconditionResult.FromSuccess();

        return result;
    }
}
