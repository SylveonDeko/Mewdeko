using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     How a server hands out its invite milestone roles.
/// </summary>
public enum InviteRoleMode
{
    /// <summary>
    ///     Members keep every milestone role they have reached.
    /// </summary>
    StackAll = 0,

    /// <summary>
    ///     Members only hold the milestone role for the highest milestone they have reached.
    /// </summary>
    HighestOnly = 1
}

/// <summary>
///     A server's invite milestone role settings.
/// </summary>
[Table("InviteRoleSettings")]
public class InviteRoleSetting
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Whether members keep every role they reached or only the highest.
    /// </summary>
    [Column("Mode")]
    public InviteRoleMode Mode { get; set; }

    /// <summary>
    ///     Whether a member loses a milestone role when their invites fall below it.
    /// </summary>
    [Column("RemoveOnDrop")]
    public bool RemoveOnDrop { get; set; }
}
