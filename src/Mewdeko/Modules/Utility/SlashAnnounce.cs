using Discord.Interactions;
using Discord.Net;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Utility.Common;

namespace Mewdeko.Modules.Utility;

/// <summary>
///     Slash command for pinging a role in a channel with an announcement.
/// </summary>
public class SlashAnnounce : MewdekoSlashCommandModule
{
    /// <summary>
    ///     Pings a role in a channel with a message. The role is made mentionable only while the message is sent.
    /// </summary>
    /// <param name="channel">The channel to post in.</param>
    /// <param name="role">The role to ping.</param>
    /// <param name="message">The announcement text.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("announce", "Pings a role in a channel with a message")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    [CheckPermissions]
    public async Task Announce([Summary("channel", "The channel to post in")] ITextChannel channel,
        [Summary("role", "The role to ping")] IRole role,
        [Summary("message", "The announcement")]
        string message)
    {
        var self = await ctx.Guild.GetCurrentUserAsync().ConfigureAwait(false);
        if (!self.GetPermissions(channel).SendMessages)
        {
            await ctx.Interaction.SendEphemeralErrorAsync(Strings.AnnounceNoSend(ctx.Guild.Id, channel.Mention), Config)
                .ConfigureAwait(false);
            return;
        }

        await DeferAsync(true).ConfigureAwait(false);
        try
        {
            await RolePinger.AnnounceAsync(channel, role, message).ConfigureAwait(false);
        }
        catch (HttpException)
        {
            await ctx.Interaction.SendEphemeralFollowupErrorAsync(Strings.AnnounceFailed(ctx.Guild.Id), Config)
                .ConfigureAwait(false);
            return;
        }

        await ctx.Interaction.SendEphemeralFollowupConfirmAsync(Strings.AnnounceSent(ctx.Guild.Id, channel.Mention))
            .ConfigureAwait(false);
    }
}
