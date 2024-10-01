namespace Mewdeko.Database.Models;

/// <summary>
///     Class for reviewing the bot, can be done from either dashboard or bot itself
/// </summary>
public class BotReviews : DbEntity
{
    /// <summary>
    ///     The user that is reviewing
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     The users name
    /// </summary>
    public string Username { get; set; }

    /// <summary>
    ///     The avatar url so we dont have to fetch it every damn time
    /// </summary>
    public string AvatarUrl { get; set; } = string.Empty;

    /// <summary>
    ///     The stars the user gave
    /// </summary>
    public int Stars { get; set; }

    /// <summary>
    ///     The reason why they gave the review, supports markdown
    /// </summary>
    public string Review { get; set; } = string.Empty;
}