namespace Mewdeko.Controllers.Common.Patreon;

/// <summary>
///     Request model for updating Patreon configuration
/// </summary>
public class PatreonConfigUpdateRequest
{
    /// <summary>
    ///     Discord channel ID for announcements
    /// </summary>
    public ulong? ChannelId { get; set; }

    /// <summary>
    ///     Custom announcement message
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    ///     Day of month for announcements (1-31)
    /// </summary>
    public int? AnnouncementDay { get; set; }

    /// <summary>
    ///     Whether to toggle announcements on/off
    /// </summary>
    public bool? ToggleAnnouncements { get; set; }

    /// <summary>
    ///     Whether to toggle role sync on/off
    /// </summary>
    public bool? ToggleRoleSync { get; set; }

    /// <summary>
    ///     Turns announcements on or off. Applied after the channel, which switches them on when set.
    /// </summary>
    public bool? AnnouncementsEnabled { get; set; }

    /// <summary>
    ///     Turns role sync on or off
    /// </summary>
    public bool? RoleSyncEnabled { get; set; }
}