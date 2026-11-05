using Discord.Commands;
using Discord.Net;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Utility.Common;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    /// <summary>
    ///     Commands for pinging a role in a channel with an announcement.
    /// </summary>
    [Group]
    public class AnnounceCommands : MewdekoSubmodule
    {
        /// <summary>
        ///     Pings a role in a channel with a message. The role is made mentionable only while the message is sent.
        /// </summary>
        /// <param name="channel">The channel to post in.</param>
        /// <param name="role">The role to ping.</param>
        /// <param name="message">The announcement text.</param>
        /// <example>.announcerole #news @Updates New version is out!</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task AnnounceRole(ITextChannel channel, IRole role, [Remainder] string message)
        {
            var self = await ctx.Guild.GetCurrentUserAsync().ConfigureAwait(false);
            if (!self.GetPermissions(channel).SendMessages)
            {
                await ReplyErrorAsync(Strings.AnnounceNoSend(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
                return;
            }

            try
            {
                await RolePinger.AnnounceAsync(channel, role, message).ConfigureAwait(false);
            }
            catch (HttpException)
            {
                await ReplyErrorAsync(Strings.AnnounceFailed(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmAsync(Strings.AnnounceSent(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
        }
    }
}
