#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents a guild's automod extras: log and review channels, server-wide exemptions and filter options.
/// </summary>
[Table("AutomodExtraSettings")]
public class AutomodExtraSetting
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the channel every automod action is posted in.
    /// </summary>
    [Column("LogChannelId")]
    public ulong? LogChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the staff channel flagged messages are sent to for review.
    /// </summary>
    [Column("ReviewChannelId")]
    public ulong? ReviewChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the space separated IDs of roles exempt from every automod rule and filter.
    /// </summary>
    [Column("ExemptRoleIds")]
    public string ExemptRoleIds { get; set; } = "";

    /// <summary>
    ///     Gets or sets the space separated IDs of channels exempt from every automod rule and filter.
    /// </summary>
    [Column("ExemptChannelIds")]
    public string ExemptChannelIds { get; set; } = "";

    /// <summary>
    ///     Gets or sets whether filtered words are also matched after undoing common spelling tricks.
    /// </summary>
    [Column("NormalizeWords")]
    public bool NormalizeWords { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether the invite filter only acts on members with no roles.
    /// </summary>
    [Column("InviteNoRolesOnly")]
    public bool InviteNoRolesOnly { get; set; }

    /// <summary>
    ///     Gets or sets whether the link filter only acts on members with no roles.
    /// </summary>
    [Column("LinkNoRolesOnly")]
    public bool LinkNoRolesOnly { get; set; }

    /// <summary>
    ///     Gets or sets the space separated domains the link filter lets through.
    /// </summary>
    [Column("LinkAllowedDomains")]
    public string LinkAllowedDomains { get; set; } = "";

    /// <summary>
    ///     Gets or sets the space separated domains the link filter always removes.
    /// </summary>
    [Column("LinkBlockedDomains")]
    public string LinkBlockedDomains { get; set; } = "";

    /// <summary>
    ///     Gets or sets when the row was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; } = DateTime.UtcNow;
}

/// <summary>
///     Represents how many links one member may post in a channel within a window.
/// </summary>
[Table("AutomodLinkLimits")]
public class AutomodLinkLimit
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the channel ID.
    /// </summary>
    [Column("ChannelId")]
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets how many links one member may post in the window.
    /// </summary>
    [Column("MaxLinks")]
    public int MaxLinks { get; set; } = 3;

    /// <summary>
    ///     Gets or sets the length of the window in seconds.
    /// </summary>
    [Column("WindowSeconds")]
    public int WindowSeconds { get; set; } = 30;

    /// <summary>
    ///     Gets or sets when the row was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; } = DateTime.UtcNow;
}
