namespace Mewdeko.Controllers.Common.Music;

/// <summary>
///     Request to move a queued track to another queue index
/// </summary>
public class MoveQueueRequest
{
    /// <summary>
    ///     Queue index of the track to move
    /// </summary>
    public int FromIndex { get; set; }

    /// <summary>
    ///     Queue index the track should take
    /// </summary>
    public int ToIndex { get; set; }
}
