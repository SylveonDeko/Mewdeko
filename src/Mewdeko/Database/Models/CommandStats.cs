#nullable enable

namespace Mewdeko.Database.Models;

/// <summary>
/// Represents statistics for a command's usage.
/// </summary>
public class CommandStats : DbEntity
{
    /// <summary>
    /// Gets or sets the name or ID of the command.
    /// </summary>
    public string? NameOrId { get; set; } = "";

    /// <summary>
    /// Gets or sets the module the command belongs to.
    /// </summary>
    public string? Module { get; set; } = "";

    /// <summary>
    /// Gets or sets a value indicating whether this is a slash command.
    /// </summary>
    public bool IsSlash { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether this command was triggered.
    /// </summary>
    public bool Trigger { get; set; } = false;

    /// <summary>
    /// Gets or sets the ID of the guild where the command was used.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the channel where the command was used.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the user who used the command.
    /// </summary>
    public ulong UserId { get; set; }
}