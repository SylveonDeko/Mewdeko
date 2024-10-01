namespace Mewdeko.Database.Models;

/// <summary>
///     Represents the role monitoring settings for a guild.
/// </summary>
public class RoleMonitoringSettings : DbEntity
{
    /// <summary>
    ///     Gets or sets the guild ID associated with these settings.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the default punishment action to be taken when a violation occurs.
    /// </summary>
    public PunishmentAction DefaultPunishmentAction { get; set; }
}