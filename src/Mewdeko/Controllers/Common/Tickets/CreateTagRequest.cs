using Color = Discord.Color;

namespace Mewdeko.Controllers.Common.Tickets;

/// <summary>
///     Request model for creating a tag
/// </summary>
public class CreateTagRequest
{
    /// <summary>
    ///     The unique identifier for the tag
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>
    ///     The display name of the tag
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    ///     The description of the tag
    /// </summary>
    public string Description { get; set; } = null!;

    /// <summary>
    ///     The color associated with the tag, as a 24-bit RGB value
    /// </summary>
    public uint Color { get; set; }
}