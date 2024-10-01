namespace Mewdeko.Database.Models;

/// <summary>
///     Represents a vote role in a guild.
/// </summary>
public class VoteRoles : DbEntity
{
    /// <summary>
    ///     Gets or sets the role ID.
    /// </summary>
    public ulong RoleId { get; set; }

    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the timer for the vote role.
    /// </summary>
    public int Timer { get; set; }
}