namespace Mewdeko.Controllers.Common.Filter;

/// <summary>
///     Request model for a channel's link limit
/// </summary>
public class LinkLimitRequest
{
    /// <summary>
    ///     The most links one member may post in the window, or 0 to remove the limit
    /// </summary>
    public int MaxLinks { get; set; }

    /// <summary>
    ///     The length of the window in seconds
    /// </summary>
    public int WindowSeconds { get; set; } = 30;
}
