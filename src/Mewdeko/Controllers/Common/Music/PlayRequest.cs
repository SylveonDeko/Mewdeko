using Mewdeko.Modules.Music.Common;

namespace Mewdeko.Controllers.Common.Music;

/// <summary>
///     A song request
/// </summary>
public class PlayRequest
{
    /// <summary>
    ///     The requested url or search query
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    ///     Alias for <see cref="Url" />. The mobile clients send the field under this name.
    /// </summary>
    public string? Query { get; set; }

    /// <summary>
    ///     Who requested
    /// </summary>
    public PartialUser? Requester { get; set; }

    /// <summary>
    ///     The url or query to load, whichever field the client populated.
    /// </summary>
    public string? Term
    {
        get
        {
            return string.IsNullOrWhiteSpace(Url) ? Query : Url;
        }
    }
}