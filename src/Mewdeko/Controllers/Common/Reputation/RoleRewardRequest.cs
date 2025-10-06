namespace Mewdeko.Controllers.Common.Reputation;

/// <summary>
///     Request model for role reward configuration
/// </summary>
public class RoleRewardRequest
{
    /// <summary>
    ///     The role ID to grant
    /// </summary>
    public ulong RoleId { get; set; }

    /// <summary>
    ///     Reputation required to get this role
    /// </summary>
    public int RepRequired { get; set; }

    /// <summary>
    ///     Whether to remove the role if reputation drops below threshold (default: true)
    /// </summary>
    public bool RemoveOnDrop { get; set; } = true;

    /// <summary>
    ///     Optional announcement channel ID
    /// </summary>
    public ulong? AnnounceChannelId { get; set; }

    /// <summary>
    ///     Whether to send DM notification (default: false)
    /// </summary>
    public bool AnnounceDM { get; set; } = false;

    /// <summary>
    ///     Optional XP reward for reaching this milestone
    /// </summary>
    public int? XpReward { get; set; }
}