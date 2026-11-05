using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Permissions.Services;

namespace Mewdeko.Modules.Permissions;

public partial class Permissions
{
    /// <summary>
    ///     Commands for sending the replies of chosen commands or modules to a set channel.
    /// </summary>
    [Group]
    public class ReplyRedirectCommands : MewdekoSubmodule<CommandReplyRedirectService>
    {
        /// <summary>
        ///     Sends a command's or module's replies to a channel instead of where it was used.
        /// </summary>
        /// <param name="target">A command, alias or module name.</param>
        /// <param name="channel">The channel replies go to.</param>
        /// <param name="deleteInvoker">Whether to delete the message that used the command.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task CmdReply(string target, ITextChannel channel, bool deleteInvoker = false)
        {
            var resolved = Service.ResolveTarget(target);
            if (resolved == null)
            {
                await ReplyErrorAsync(Strings.CmdReplyNoTarget(ctx.Guild.Id, target)).ConfigureAwait(false);
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
        /// <param name="target">A command, alias or module name.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task CmdReplyRemove(string target)
        {
            var resolved = Service.ResolveTarget(target);
            if (resolved == null ||
                !await Service.RemoveAsync(ctx.Guild.Id, resolved.Value.Target, resolved.Value.IsModule)
                    .ConfigureAwait(false))
            {
                await ReplyErrorAsync(Strings.CmdReplyNoRule(ctx.Guild.Id, target)).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmAsync(Strings.CmdReplyRemoved(ctx.Guild.Id, resolved.Value.Target))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Lists the commands and modules whose replies go to a set channel.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task CmdReplyList()
        {
            var rules = await Service.GetRulesAsync(ctx.Guild.Id).ConfigureAwait(false);
            if (rules.Count == 0)
            {
                await ReplyErrorAsync(Strings.CmdReplyNone(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var lines = rules.Select(r => CommandReplyRedirectService.Describe(Strings, ctx.Guild.Id, r));
            await ctx.Channel.EmbedAsync(new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.CmdReplyListTitle(ctx.Guild.Id))
                .WithDescription(string.Join("\n", lines))).ConfigureAwait(false);
        }    }
}
