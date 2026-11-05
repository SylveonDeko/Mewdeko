namespace Mewdeko.Modules.Chat_Triggers.Common;

/// <summary>
///     Builds the <see cref="TriggerGateContext" /> for a member firing a trigger.
/// </summary>
public static class TriggerGateFactory
{
    /// <summary>
    ///     Builds the context for a response, or returns null when the response has no gate to check.
    /// </summary>
    /// <param name="response">The response text before blocks are evaluated.</param>
    /// <param name="channel">The channel the trigger fired in.</param>
    /// <param name="user">The member that fired the trigger.</param>
    /// <returns>The context, or null when the response has no gate or the member is not in a cached guild.</returns>
    public static TriggerGateContext? Create(string? response, IMessageChannel? channel, IUser user)
    {
        if (response is null ||
            !response.Contains("{require(", StringComparison.Ordinal) &&
            !response.Contains("{blacklist(", StringComparison.Ordinal))
        {
            return null;
        }

        if (channel is not SocketGuildChannel guildChannel)
            return null;

        var guild = guildChannel.Guild;
        var member = user as SocketGuildUser ?? guild.GetUser(user.Id);
        if (member is null)
            return null;

        var channelIds = new HashSet<ulong>
        {
            guildChannel.Id
        };

        if (channel is SocketThreadChannel { ParentChannel: { } parent })
            channelIds.Add(parent.Id);

        return new TriggerGateContext(
            guild.Roles.ToDictionary(x => x.Id, x => x.Name),
            guild.Channels.ToDictionary(x => x.Id, x => x.Name),
            member.Roles.Select(x => x.Id).ToHashSet(),
            channelIds,
            member.GuildPermissions.ManageMessages);
    }
}
