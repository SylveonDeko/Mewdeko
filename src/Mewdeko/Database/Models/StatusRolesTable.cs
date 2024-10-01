namespace Mewdeko.Database.Models;

/// <summary>
///     Represents a status roles table in a guild.
/// </summary>
public class StatusRolesTable : DbEntity
{
    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the status.
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    ///     Gets or sets the roles to add.
    /// </summary>
    public string? ToAdd { get; set; }

    /// <summary>
    ///     Gets or sets the roles to remove.
    /// </summary>
    public string? ToRemove { get; set; }

    /// <summary>
    ///     Gets or sets the status embed.
    /// </summary>
    public string? StatusEmbed { get; set; } = null;

    /// <summary>
    ///     Gets or sets a value indicating whether to re-add removed roles.
    /// </summary>
    public bool ReaddRemoved { get; set; } = false;

    /// <summary>
    ///     Gets or sets a value indicating whether to remove added roles.
    /// </summary>
    public bool RemoveAdded { get; set; } = true;

    /// <summary>
    ///     Gets or sets the status channel ID.
    /// </summary>
    public ulong StatusChannelId { get; set; }
}