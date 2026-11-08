namespace Mewdeko.Controllers.Common.Tickets;

/// <summary>
///     Request to change who gets notified about ticket activity in a server.
/// </summary>
public class UpdateNotificationSettingsRequest
{
    /// <summary>
    ///     Whether the notification roles are pinged when a ticket is created.
    /// </summary>
    public bool EnableStaffPings { get; set; }

    /// <summary>
    ///     Whether a ticket's creator gets a DM when staff claim or unclaim it.
    /// </summary>
    public bool EnableDmNotifications { get; set; }

    /// <summary>
    ///     The roles pinged when a ticket is created and staff pings are on.
    /// </summary>
    public List<ulong>? NotificationRoles { get; set; }
}
