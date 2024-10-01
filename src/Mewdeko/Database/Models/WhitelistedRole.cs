namespace Mewdeko.Database.Models;

/// <summary>
///     Represents a role that is whitelisted from role monitoring punishments in a guild.
/// </summary>
public class WhitelistedRole : DbEntity
{
    /// <summary>
    ///     Gets or sets the guild ID where the role is whitelisted.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the role ID of the whitelisted role.
    /// </summary>
    public ulong RoleId { get; set; }
}