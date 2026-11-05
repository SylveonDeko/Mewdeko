using System.Threading;

namespace Mewdeko.Modules.Utility.Common;

/// <summary>
///     Pings a role that is not mentionable by making it mentionable only while the message is sent.
/// </summary>
public static class RolePinger
{
    private static readonly ConcurrentDictionary<ulong, SemaphoreSlim> Locks = new();

    /// <summary>
    ///     Gets the mention rules that let a message ping exactly one role.
    /// </summary>
    /// <param name="roleId">The role ID.</param>
    /// <returns>The allowed mentions.</returns>
    public static AllowedMentions MentionsFor(ulong roleId)
    {
        return new AllowedMentions(AllowedMentionTypes.Roles)
        {
            RoleIds = [roleId]
        };
    }

    /// <summary>
    ///     Sends a message that pings a role, making the role mentionable only for the send.
    /// </summary>
    /// <param name="channel">The channel to post in.</param>
    /// <param name="role">The role to ping.</param>
    /// <param name="message">The message text, or an embed in the usual embed format.</param>
    /// <returns>The sent message.</returns>
    public static Task<IUserMessage> AnnounceAsync(ITextChannel channel, IRole role, string message)
    {
        var parsed = SmartEmbed.TryParse(message, channel.GuildId, out var embed, out var plainText,
            out var components);
        var mentions = MentionsFor(role.Id);
        var content = string.Join(' ', role.Mention, parsed ? plainText : message).Trim();
        return SendAsync(channel.Guild, channel, role, () => parsed
            ? channel.SendMessageAsync(content, embeds: embed, components: components?.Build(),
                allowedMentions: mentions)
            : channel.SendMessageAsync(content, allowedMentions: mentions));
    }

    /// <summary>
    ///     Runs a send while the role can be pinged, then puts the role back the way it was.
    /// </summary>
    /// <typeparam name="T">What the send returns.</typeparam>
    /// <param name="guild">The guild the role is in.</param>
    /// <param name="channel">The channel the message goes to.</param>
    /// <param name="role">The role to ping.</param>
    /// <param name="send">Sends the message.</param>
    /// <returns>What the send returned.</returns>
    public static async Task<T> SendAsync<T>(IGuild guild, IGuildChannel channel, IRole role, Func<Task<T>> send)
    {
        var self = await guild.GetCurrentUserAsync().ConfigureAwait(false);
        if (role.IsMentionable || self.GetPermissions(channel).MentionEveryone)
            return await send().ConfigureAwait(false);

        var gate = Locks.GetOrAdd(role.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await role.ModifyAsync(x => x.Mentionable = true).ConfigureAwait(false);
            try
            {
                return await send().ConfigureAwait(false);
            }
            finally
            {
                await role.ModifyAsync(x => x.Mentionable = false).ConfigureAwait(false);
            }
        }
        finally
        {
            gate.Release();
        }
    }
}
