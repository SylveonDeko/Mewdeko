using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Permissions.Services;

namespace Mewdeko.Modules.Permissions;

/// <summary>
///     Slash commands for sending the text command replies of chosen commands or modules to a set channel.
/// </summary>
[Group("command-reply", "Send command replies to a set channel")]
public class SlashReplyRedirect : MewdekoSlashModuleBase<CommandReplyRedirectService>
{
    /// <summary>
    ///     Sends a command's or module's replies to a channel instead of where it was used.
    /// </summary>
    /// <param name="target">A command or module name.</param>
    /// <param name="channel">The channel replies go to.</param>
    /// <param name="deleteInvoker">Whether to delete the message that used the command.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("set", "Sends a command's or module's replies to a channel")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task Set(
        [Summary("target", "A command or module name")]
        string target,
        [Summary("channel", "The channel replies go to")]
        ITextChannel channel,
        [Summary("delete-invoker", "Delete the message that used the command")]
        bool deleteInvoker = false)
    {
        var resolved = Service.ResolveTarget(target);
        if (resolved == null)
        {
            await EphemeralReplyErrorAsync(Strings.CmdReplyNoTarget(ctx.Guild.Id, target)).ConfigureAwait(false);
            return;
        }

        await Service.SetAsync(ctx.Guild.Id, resolved.Value.Target, resolved.Value.IsModule, channel.Id,
            deleteInvoker).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.CmdReplySet(ctx.Guild.Id, resolved.Value.Target, channel.Id))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Stops sending a command's or module's replies to a set channel.
    /// </summary>
    /// <param name="target">A command or module name.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("remove", "Stops sending a command's or module's replies to a set channel")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task Remove([Summary("target", "A command or module name")] string target)
    {
        var resolved = Service.ResolveTarget(target);
        if (resolved == null ||
            !await Service.RemoveAsync(ctx.Guild.Id, resolved.Value.Target, resolved.Value.IsModule)
                .ConfigureAwait(false))
        {
            await EphemeralReplyErrorAsync(Strings.CmdReplyNoRule(ctx.Guild.Id, target)).ConfigureAwait(false);
            return;
        }

        await ReplyConfirmAsync(Strings.CmdReplyRemoved(ctx.Guild.Id, resolved.Value.Target)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists the commands and modules whose replies go to a set channel.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("list", "Lists the commands and modules whose replies go to a set channel")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task List()
    {
        var rules = await Service.GetRulesAsync(ctx.Guild.Id).ConfigureAwait(false);
        if (rules.Count == 0)
        {
            await EphemeralReplyErrorAsync(Strings.CmdReplyNone(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var lines = rules.Select(r => CommandReplyRedirectService.Describe(Strings, ctx.Guild.Id, r));
        await RespondAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.CmdReplyListTitle(ctx.Guild.Id))
            .WithDescription(string.Join("\n", lines)).Build()).ConfigureAwait(false);
    }
}
