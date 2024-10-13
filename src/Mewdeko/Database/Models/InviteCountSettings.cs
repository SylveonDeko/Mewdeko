namespace Mewdeko.Database.Models;

/// <summary>
/// Settings for invite counting
/// </summary>
public class InviteCountSettings : DbEntity
{
    /// <summary>
    /// The guild id these settings are for
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Whether to remove an invite when a user leaves.
    /// </summary>
    public bool RemoveInviteOnLeave { get; set; }

    /// <summary>
    /// Minimum account age for an invite to be counted.
    /// </summary>
    public TimeSpan MinAccountAge { get; set; }

    /// <summary>
    /// Whether invite tracking is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }
}