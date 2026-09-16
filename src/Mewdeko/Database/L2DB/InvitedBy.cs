using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     One row per witnessed join, recording who (if anyone) brought the member in and how. Rows are kept after the
///     member leaves so retention and rejoin detection can look back at them.
/// </summary>
[Table("InvitedBy")]
public class InvitedBy
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("UserId")]
    public ulong UserId { get; set; }

    /// <summary>
    ///     The inviter credited for this join, or 0 when nobody was (vanity, bot, unknown).
    /// </summary>
    [Column("InviterId")]
    public ulong InviterId { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     The invite code that was used, when it could be determined.
    /// </summary>
    [Column("InviteCode")]
    public string? InviteCode { get; set; }

    /// <summary>
    ///     The <see cref="Mewdeko.Modules.Utility.Common.InviteJoinType" /> describing how the member arrived.
    /// </summary>
    [Column("JoinType")]
    public int JoinType { get; set; } = 1;

    /// <summary>
    ///     Whether fake detection flagged this join.
    /// </summary>
    [Column("IsFake")]
    public bool IsFake { get; set; }

    /// <summary>
    ///     The <see cref="Mewdeko.Modules.Utility.Common.InviteFakeReason" /> when <see cref="IsFake" /> is set.
    /// </summary>
    [Column("FakeReason")]
    public int FakeReason { get; set; }

    /// <summary>
    ///     When the member left again, or null while they are still in the guild.
    /// </summary>
    [Column("LeftAt")]
    public DateTime? LeftAt { get; set; }

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}