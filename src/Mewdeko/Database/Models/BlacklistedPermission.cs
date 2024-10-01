namespace Mewdeko.Database.Models;

/// <summary>
///     Represents a blacklisted permission in a guild.
/// </summary>
public class BlacklistedPermission : DbEntity
{
    /// <summary>
    ///     Gets or sets the guild ID where the permission is blacklisted.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the permission that is blacklisted.
    /// </summary>
    public GuildPermission Permission { get; set; }

    /// <summary>
    ///     Gets or sets the punishment action to be taken when a role with this permission is assigned.
    ///     If null, the default punishment action will be used.
    /// </summary>
    public PunishmentAction? PunishmentAction { get; set; }
}