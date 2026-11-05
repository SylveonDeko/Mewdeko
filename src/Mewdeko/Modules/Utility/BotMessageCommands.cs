using System.IO;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    /// <summary>
    ///     Commands for editing messages the bot posted, by message link.
    /// </summary>
    [Group]
    public class BotMessageCommands : MewdekoSubmodule<BotMessageEditService>
    {
        /// <summary>
        ///     Replaces the text and embeds of a message the bot posted.
        /// </summary>
        /// <param name="messageLink">The link to the bot's message.</param>
        /// <param name="content">The new text or embed json.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task EditLink(string messageLink, [Remainder] string content)
        {
            var (result, message) = await Service.FindAsync(ctx.Guild.Id, messageLink).ConfigureAwait(false);
            if (message == null)
            {
                await ReplyErrorAsync(BotMessageEditService.Explain(Strings, ctx.Guild.Id, result))
                    .ConfigureAwait(false);
                return;
            }

            if (!((IGuildUser)ctx.User).GetPermissions((IGuildChannel)message.Channel).ManageMessages)
            {
                await ReplyErrorAsync(Strings.InsufPermsU(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            result = await Service.EditAsync(message, ctx.Guild.Id, content).ConfigureAwait(false);
            if (result != BotMessageEditResult.Ok)
            {
                await ReplyErrorAsync(BotMessageEditService.Explain(Strings, ctx.Guild.Id, result))
                    .ConfigureAwait(false);
                return;
            }

            await ReplyConfirmAsync(Strings.BotMsgEdited(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Shows the text and embed json of a message the bot posted so it can be copied and edited.
        /// </summary>
        /// <param name="messageLink">The link to the bot's message.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task MsgJson(string messageLink)
        {
            var (result, message) = await Service.FindAsync(ctx.Guild.Id, messageLink).ConfigureAwait(false);
            if (message == null)
            {
                await ReplyErrorAsync(BotMessageEditService.Explain(Strings, ctx.Guild.Id, result))
                    .ConfigureAwait(false);
                return;
            }

            if (!((IGuildUser)ctx.User).GetPermissions((IGuildChannel)message.Channel).ManageMessages)
            {
                await ReplyErrorAsync(Strings.InsufPermsU(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var content = BotMessageEditService.ToEditableContent(message);
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
            await ctx.Channel.SendFileAsync(stream, "message.json").ConfigureAwait(false);
        }
    }
}
