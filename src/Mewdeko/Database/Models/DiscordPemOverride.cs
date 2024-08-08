namespace Mewdeko.Database.Models;

/// <summary>
/// Represents an override for Discord permissions.
/// </summary>
public class DiscordPermOverride : DbEntity
{
    /// <summary>
    /// Gets or sets the guild permission being overridden.
    /// </summary>
    public GuildPermission Perm { get; set; }

    /// <summary>
    /// Gets or sets the ID of the guild this override applies to. Null if global.
    /// </summary>
    public ulong? GuildId { get; set; }

    /// <summary>
    /// Gets or sets the command this override applies to.
    /// </summary>
    public string? Command { get; set; }
}