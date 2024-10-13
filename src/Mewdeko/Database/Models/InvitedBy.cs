using LinqToDB.Mapping;

namespace Mewdeko.Database.Models;

/// <summary>
///     Represents an invited by entry in the database.
/// </summary>
[Table("InvitedBy")]
public class InvitedBy : DbEntity
{
    /// <summary>
    ///     Gets or sets the user ID of the invited user.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the user ID of the inviter.
    /// </summary>
    public ulong InviterId { get; set; }

    /// <summary>
    ///     Gets or sets the guild ID associated with this invite.
    /// </summary>
    public ulong GuildId { get; set; }
}