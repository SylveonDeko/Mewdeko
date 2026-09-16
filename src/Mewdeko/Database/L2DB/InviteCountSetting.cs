using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     Per-guild invite tracking configuration.
/// </summary>
[Table("InviteCountSettings")]
public class InviteCountSetting
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Whether an inviter loses credit when the invited member leaves.
    /// </summary>
    [Column("RemoveInviteOnLeave")]
    public bool RemoveInviteOnLeave { get; set; }

    /// <summary>
    ///     Accounts younger than this are flagged as fake joins. Zero disables the check.
    /// </summary>
    [Column("MinAccountAge")]
    public TimeSpan MinAccountAge { get; set; }

    [Column("IsEnabled")]
    public bool IsEnabled { get; set; }

    /// <summary>
    ///     Whether a member who has joined before earns the inviter a regular invite again. When false the rejoin is
    ///     flagged as fake instead.
    /// </summary>
    [Column("CountRejoins")]
    public bool CountRejoins { get; set; } = true;

    /// <summary>
    ///     Whether members without an avatar are flagged as fake joins.
    /// </summary>
    [Column("FakeOnNoAvatar")]
    public bool FakeOnNoAvatar { get; set; }

    /// <summary>
    ///     The channel that invites created through the link command point at, or null for the system channel.
    /// </summary>
    [Column("LinkChannelId")]
    public ulong? LinkChannelId { get; set; }

    /// <summary>
    ///     Optional channel that receives an embed for every tracked join and leave.
    /// </summary>
    [Column("LogChannelId")]
    public ulong? LogChannelId { get; set; }

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}