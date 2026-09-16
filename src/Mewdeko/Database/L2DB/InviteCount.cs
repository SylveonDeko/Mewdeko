using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     Per-inviter invite tally for a guild. <see cref="Count" /> is the net total, kept in step with the breakdown
///     columns as regular - left - fake + bonus so older readers keep working.
/// </summary>
[Table("InviteCounts")]
public class InviteCount
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("UserId")]
    public ulong UserId { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     The net invite total: regular - left - fake + bonus.
    /// </summary>
    [Column("Count")]
    public int Count { get; set; }

    /// <summary>
    ///     Joins credited to this inviter that were not flagged as fake.
    /// </summary>
    [Column("Regular")]
    public int Regular { get; set; }

    /// <summary>
    ///     Credited joins whose member has since left the guild.
    /// </summary>
    [Column("Left")]
    public int Left { get; set; }

    /// <summary>
    ///     Joins flagged by fake detection (young account, rejoin, self invite, missing avatar).
    /// </summary>
    [Column("Fake")]
    public int Fake { get; set; }

    /// <summary>
    ///     Invites granted or removed manually by staff.
    /// </summary>
    [Column("Bonus")]
    public int Bonus { get; set; }

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }

    /// <summary>
    ///     Recomputes <see cref="Count" /> from the breakdown columns.
    /// </summary>
    public void Recalculate()
    {
        Count = Regular - Left - Fake + Bonus;
    }
}