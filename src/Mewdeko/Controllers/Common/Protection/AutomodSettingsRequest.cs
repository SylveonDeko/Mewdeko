namespace Mewdeko.Controllers.Common.Protection;

/// <summary>
///     Request model for the automod settings shared by every rule. Fields left out stay as they are.
/// </summary>
public class AutomodSettingsRequest
{
    /// <summary>
    ///     The channel every automod action is posted in, or 0 to turn the log off
    /// </summary>
    public ulong? LogChannelId { get; set; }

    /// <summary>
    ///     The staff channel flagged messages are sent to, or 0 to turn review off
    /// </summary>
    public ulong? ReviewChannelId { get; set; }

    /// <summary>
    ///     The roles exempt from every rule, protection and filter, replacing the current list
    /// </summary>
    public List<ulong>? ExemptRoleIds { get; set; }

    /// <summary>
    ///     The channels exempt from every rule, protection and filter, replacing the current list
    /// </summary>
    public List<ulong>? ExemptChannelIds { get; set; }
}
