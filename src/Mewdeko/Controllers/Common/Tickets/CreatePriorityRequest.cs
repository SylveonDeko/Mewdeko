using Color = Discord.Color;

namespace Mewdeko.Controllers.Common.Tickets;

/// <summary>
///     Request model for creating a priority
/// </summary>
public class CreatePriorityRequest
{
    /// <summary>
    ///     The unique identifier for the priority
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>
    ///     The display name of the priority
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    ///     The emoji associated with the priority
    /// </summary>
    public string Emoji { get; set; } = null!;

    /// <summary>
    ///     The priority level (1-5)
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    ///     Whether to ping staff for tickets with this priority
    /// </summary>
    public bool PingStaff { get; set; }

    /// <summary>
    ///     The required response time for this priority level
    /// </summary>
    public TimeSpan ResponseTime { get; set; }

    /// <summary>
    ///     The color associated with this priority, as a 24-bit RGB value
    /// </summary>
    public uint Color { get; set; }
}