#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents a channel bridged to a channel on the other platform.
/// </summary>
[Table("ChannelBridges")]
public class ChannelBridge
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the id both sides of the bridge share.
    /// </summary>
    [Column("BridgeId")]
    public Guid BridgeId { get; set; }

    /// <summary>
    ///     Gets or sets the guild the bridged channel is in.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the bridged channel.
    /// </summary>
    [Column("ChannelId")]
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the webhook relayed messages are posted through.
    /// </summary>
    [Column("WebhookUrl")]
    public string WebhookUrl { get; set; } = "";

    /// <summary>
    ///     Gets or sets the community on the other platform.
    /// </summary>
    [Column("RemoteGuildId")]
    public ulong RemoteGuildId { get; set; }

    /// <summary>
    ///     Gets or sets the channel on the other platform.
    /// </summary>
    [Column("RemoteChannelId")]
    public ulong RemoteChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the name of the community on the other platform.
    /// </summary>
    [Column("RemoteGuildName")]
    public string RemoteGuildName { get; set; } = "";

    /// <summary>
    ///     Gets or sets the name of the channel on the other platform.
    /// </summary>
    [Column("RemoteChannelName")]
    public string RemoteChannelName { get; set; } = "";

    /// <summary>
    ///     Gets or sets whether relaying is paused.
    /// </summary>
    [Column("Paused")]
    public bool Paused { get; set; }

    /// <summary>
    ///     Gets or sets whether messages from bots are relayed.
    /// </summary>
    [Column("AllowBots")]
    public bool AllowBots { get; set; }

    /// <summary>
    ///     Gets or sets which way messages flow on this side, as a <c>BridgeDirection</c> value.
    /// </summary>
    [Column("Direction")]
    public int Direction { get; set; }

    /// <summary>
    ///     Gets or sets the space-separated roles allowed to be relayed outward. Empty means everyone.
    /// </summary>
    [Column("AllowedRoleIds")]
    public string AllowedRoleIds { get; set; } = "";

    /// <summary>
    ///     Gets or sets the space-separated roles that are never relayed outward.
    /// </summary>
    [Column("BlockedRoleIds")]
    public string BlockedRoleIds { get; set; } = "";

    /// <summary>
    ///     Gets or sets the space-separated users that are never relayed outward.
    /// </summary>
    [Column("BlockedUserIds")]
    public string BlockedUserIds { get; set; } = "";

    /// <summary>
    ///     Gets or sets how many minutes a member must have been in the community to be relayed outward. 0 is off.
    /// </summary>
    [Column("MinMemberAgeMinutes")]
    public int MinMemberAgeMinutes { get; set; }

    /// <summary>
    ///     Gets or sets whether outgoing messages containing an invite link are dropped.
    /// </summary>
    [Column("BlockInvites")]
    public bool BlockInvites { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether outgoing messages caught by the community's word filter are dropped.
    /// </summary>
    [Column("UseWordFilter")]
    public bool UseWordFilter { get; set; } = true;

    /// <summary>
    ///     Gets or sets how incoming authors are named on this side's webhook.
    /// </summary>
    [Column("NameFormat")]
    public string NameFormat { get; set; } = "{name}";

    /// <summary>
    ///     Gets or sets the pair the bridge was made through, or null when it was made with a code.
    /// </summary>
    [Column("PairId")]
    public Guid? PairId { get; set; }

    /// <summary>
    ///     Gets or sets the user who created the bridge.
    /// </summary>
    [Column("CreatedBy")]
    public ulong CreatedBy { get; set; }

    /// <summary>
    ///     Gets or sets when the row was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}
