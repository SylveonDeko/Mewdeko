namespace Mewdeko.Database.Models;

/// <summary>
///     Represents a role that triggers an automatic ban when assigned.
/// </summary>
public class AutoBanRoles : DbEntity
{
    /// <summary>
    ///     Gets or sets the ID of the guild where this auto-ban role is configured.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the role that triggers the auto-ban.
    /// </summary>
    public ulong RoleId { get; set; }
}