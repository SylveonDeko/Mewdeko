#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents a member timeout that is being watched so its end can be logged.
/// </summary>
[Table("TrackedTimeouts")]
public class TrackedTimeout
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
    ///     Gets or sets the timed out member's user ID.
    /// </summary>
    [Column("UserId")]
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets when the timeout ends.
    /// </summary>
    [Column("EndsAt")]
    public DateTime EndsAt { get; set; }
}
