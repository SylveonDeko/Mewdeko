namespace Mewdeko.Database.Models;

/// <summary>
///     Represents a user who is whitelisted from role monitoring punishments in a guild.
/// </summary>
public class WhitelistedUser : DbEntity
{
    /// <summary>
    ///     Gets or sets the guild ID where the user is whitelisted.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the user ID of the whitelisted user.
    /// </summary>
    public ulong UserId { get; set; }
}