namespace Mewdeko.Database.Models;

/// <summary>
/// Represents an entry in the blacklist.
/// </summary>
public class BlacklistEntry : DbEntity
{
    /// <summary>
    /// Gets or sets the ID of the blacklisted item.
    /// </summary>
    public ulong ItemId { get; set; }

    /// <summary>
    /// Gets or sets the type of the blacklisted item.
    /// </summary>
    public BlacklistType Type { get; set; }

    /// <summary>
    /// Gets or sets the reason for the blacklist entry.
    /// </summary>
    public string Reason { get; set; } = "No reason provided.";
}

/// <summary>
/// Represents the types of items that can be blacklisted.
/// </summary>
public enum BlacklistType
{
    /// <summary>
    /// Represents a blacklisted server.
    /// </summary>
    Server,

    /// <summary>
    /// Represents a blacklisted channel.
    /// </summary>
    Channel,

    /// <summary>
    /// Represents a blacklisted user.
    /// </summary>
    User
}