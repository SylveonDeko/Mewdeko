using Mewdeko.Modules.WordOfTheDay.Common;

namespace Mewdeko.Controllers.Common.WordOfTheDay;

/// <summary>
///     The result of posting a word immediately.
/// </summary>
public class WordOfTheDayPostResponse
{
    /// <summary>
    ///     The word that was posted.
    /// </summary>
    public WordEntry Entry { get; set; } = null!;

    /// <summary>
    ///     True when the guild's custom template rendered an empty message and the default embed was sent instead.
    /// </summary>
    public bool UsedFallback { get; set; }

    /// <summary>
    ///     Human readable explanation of the fallback, when one happened.
    /// </summary>
    public string? Warning { get; set; }
}
