namespace Mewdeko.Database.Models;

/// <summary>
///     Represents a blacklisted role in a guild.
/// </summary>
public class BlacklistedRole : DbEntity
{
    /// <summary>
    ///     Gets or sets the guild ID where the role is blacklisted.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the blacklisted role.
    /// </summary>
    public ulong RoleId { get; set; }

    /// <summary>
    ///     Gets or sets the punishment action to be taken when this role is assigned.
    ///     If null, the default punishment action will be used.
    /// </summary>
    public PunishmentAction? PunishmentAction { get; set; }
}