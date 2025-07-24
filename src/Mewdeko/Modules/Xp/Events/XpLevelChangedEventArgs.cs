using Mewdeko.Modules.Xp.Models;

namespace Mewdeko.Modules.Xp.Events;

/// <summary>
///     Event arguments for when a user's XP level changes.
/// </summary>
public class XpLevelChangedEventArgs
{
    /// <summary>
    ///     Gets or sets the guild ID where the level change occurred.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the user ID whose level changed.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the previous level.
    /// </summary>
    public int OldLevel { get; set; }

    /// <summary>
    ///     Gets or sets the new level.
    /// </summary>
    public int NewLevel { get; set; }

    /// <summary>
    ///     Gets or sets the total XP amount.
    /// </summary>
    public long TotalXp { get; set; }

    /// <summary>
    ///     Gets or sets the channel ID where the XP was gained.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the source of the XP gain that triggered the level change.
    /// </summary>
    public XpSource Source { get; set; }

    /// <summary>
    ///     Gets or sets whether this was a level up (true) or level down (false).
    /// </summary>
    public bool IsLevelUp { get; set; }

    /// <summary>
    ///     Gets or sets the notification type preference for this user.
    /// </summary>
    public XpNotificationType NotificationType { get; set; }
}