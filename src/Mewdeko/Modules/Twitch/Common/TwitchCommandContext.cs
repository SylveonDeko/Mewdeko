using TwitchLib.Client.Models;

namespace Mewdeko.Modules.Twitch.Common;

/// <summary>
///     Permission level of a Twitch chatter, ordered from lowest to highest privilege.
/// </summary>
public enum TwitchPermissionLevel
{
    /// <summary>Any viewer in chat.</summary>
    Everyone = 0,

    /// <summary>Active channel subscriber.</summary>
    Subscriber = 1,

    /// <summary>Channel VIP.</summary>
    Vip = 2,

    /// <summary>Channel moderator.</summary>
    Mod = 3,

    /// <summary>The channel owner/broadcaster.</summary>
    Broadcaster = 4
}

/// <summary>
///     Context passed to every Twitch command handler, analogous to <c>ICommandContext</c> in Discord.Net.
///     Carries the raw message, resolved permission level, and the Discord guild association.
/// </summary>
public class TwitchCommandContext
{
    /// <summary>
    ///     Initializes a new <see cref="TwitchCommandContext" /> from a TwitchLib chat message.
    /// </summary>
    /// <param name="message">The raw TwitchLib chat message.</param>
    /// <param name="guildId">The Discord guild ID whose config maps to this Twitch channel.</param>
    /// <param name="commandPrefix">The command prefix configured for this guild.</param>
    public TwitchCommandContext(ChatMessage message, ulong guildId, string commandPrefix)
    {
        Message = message;
        GuildId = guildId;
        CommandPrefix = commandPrefix;

        Username = message.Username;
        DisplayName = message.DisplayName;
        TwitchChannel = message.Channel;
        MessageText = message.Message;

        IsBroadcaster = message.IsBroadcaster;
        IsMod = message.IsModerator || message.IsBroadcaster;
        IsSubscriber = message.IsSubscriber;
        IsVip = message.IsVip;

        PermissionLevel = IsBroadcaster
            ? TwitchPermissionLevel.Broadcaster
            : IsMod
                ? TwitchPermissionLevel.Mod
                : IsVip
                    ? TwitchPermissionLevel.Vip
                    : IsSubscriber
                        ? TwitchPermissionLevel.Subscriber
                        : TwitchPermissionLevel.Everyone;
    }

    /// <summary>Gets the raw TwitchLib chat message.</summary>
    public ChatMessage Message { get; }

    /// <summary>Gets the Discord guild ID this Twitch channel is configured for.</summary>
    public ulong GuildId { get; }

    /// <summary>Gets the command prefix active for this guild's Twitch channel.</summary>
    public string CommandPrefix { get; }

    /// <summary>Gets the sender's Twitch login name (lowercase).</summary>
    public string Username { get; }

    /// <summary>Gets the sender's Twitch display name.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the Twitch channel name the message was sent in.</summary>
    public string TwitchChannel { get; }

    /// <summary>Gets the full text of the chat message.</summary>
    public string MessageText { get; }

    /// <summary>Gets whether the sender is the channel broadcaster.</summary>
    public bool IsBroadcaster { get; }

    /// <summary>Gets whether the sender is a channel moderator (includes broadcaster).</summary>
    public bool IsMod { get; }

    /// <summary>Gets whether the sender is an active channel subscriber.</summary>
    public bool IsSubscriber { get; }

    /// <summary>Gets whether the sender has VIP status in the channel.</summary>
    public bool IsVip { get; }

    /// <summary>Gets the resolved permission level for this sender.</summary>
    public TwitchPermissionLevel PermissionLevel { get; }

    /// <summary>
    ///     Gets or sets the Discord user ID linked to this Twitch user via account linking.
    ///     <see langword="null" /> if no link exists.
    /// </summary>
    public ulong? LinkedDiscordUserId { get; set; }

    /// <summary>
    ///     Gets or sets the parsed arguments following the command name.
    ///     Set by <c>TwitchCommandHandler</c> before the module method is invoked.
    /// </summary>
    public string[] Args { get; set; } = [];

    /// <summary>
    ///     Gets or sets the BCP-47 language tag configured for this Twitch channel, if any.
    ///     When set, overrides the guild locale for Twitch chat responses.
    ///     Set by <c>TwitchCommandHandler</c> after loading the guild config.
    /// </summary>
    public string? ChannelLanguage { get; set; }
}