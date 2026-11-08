namespace Mewdeko.Controllers.Common.Patreon;

/// <summary>
///     A guild's Patreon announcement and role sync settings
/// </summary>
public class PatreonConfigResponse
{
    /// <summary>
    ///     Discord guild ID
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Channel the monthly announcement is posted in, 0 when unset
    /// </summary>
    public ulong PatreonChannelId { get; set; }

    /// <summary>
    ///     Custom announcement message, null for the default
    /// </summary>
    public string? PatreonMessage { get; set; }

    /// <summary>
    ///     Day of the month the announcement is posted
    /// </summary>
    public int PatreonAnnouncementDay { get; set; }

    /// <summary>
    ///     Whether the monthly announcement is on
    /// </summary>
    public bool PatreonEnabled { get; set; }

    /// <summary>
    ///     Whether tier roles are given to linked supporters
    /// </summary>
    public bool PatreonRoleSync { get; set; }

    /// <summary>
    ///     When the last announcement was posted
    /// </summary>
    public DateTime? PatreonLastAnnouncement { get; set; }
}
