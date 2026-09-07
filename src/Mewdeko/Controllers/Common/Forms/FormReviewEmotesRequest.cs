namespace Mewdeko.Controllers.Common.Forms;

/// <summary>
///     Request model for setting a guild's default review button emotes.
/// </summary>
public class FormReviewEmotesRequest
{
    /// <summary>
    ///     Emote for the approve button. Null or empty restores the built-in tick.
    /// </summary>
    public string? ApproveEmote { get; set; }

    /// <summary>
    ///     Emote for the reject button. Null or empty restores the built-in cross.
    /// </summary>
    public string? RejectEmote { get; set; }
}