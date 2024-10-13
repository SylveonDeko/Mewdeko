using LinqToDB.Mapping;

namespace Mewdeko.Database.Models;

/// <summary>
///     Represents an invite count entry in the database.
/// </summary>
[Table("InviteCounts")]
public class InviteCount : DbEntity
{
    /// <summary>
    ///     Gets or sets the user ID associated with this invite count.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the guild ID associated with this invite count.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the number of invites for this user in this guild.
    /// </summary>
    public int Count { get; set; }
}