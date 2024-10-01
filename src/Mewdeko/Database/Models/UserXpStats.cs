namespace Mewdeko.Database.Models;

/// <summary>
///     Represents the XP stats of a user in a guild.
/// </summary>
public class UserXpStats : DbEntity
{
    /// <summary>
    ///     Gets or sets the user ID.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the XP.
    /// </summary>
    public int Xp { get; set; }

    /// <summary>
    ///     Gets or sets the awarded XP.
    /// </summary>
    public int AwardedXp { get; set; }

    /// <summary>
    ///     Gets or sets the notification location for level up.
    /// </summary>
    public XpNotificationLocation NotifyOnLevelUp { get; set; }

    /// <summary>
    ///     Gets or sets the date and time of the last level up.
    /// </summary>
    public DateTime LastLevelUp { get; set; } = DateTime.UtcNow;
}

/// <summary>
///     Specifies the location for XP notifications.
/// </summary>
public enum XpNotificationLocation
{
    /// <summary>
    ///     No XP notifications.
    /// </summary>
    None,

    /// <summary>
    ///     XP notifications via direct message.
    /// </summary>
    Dm,

    /// <summary>
    ///     XP notifications in the channel.
    /// </summary>
    Channel
}