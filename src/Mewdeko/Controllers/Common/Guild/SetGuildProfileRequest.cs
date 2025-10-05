namespace Mewdeko.Controllers.Common.Guild;

/// <summary>
///     Request model for setting the bot's guild profile
/// </summary>
public class SetGuildProfileRequest
{
    /// <summary>
    ///     URL to the avatar image or base64 data URI (optional)
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    ///     URL to the banner image or base64 data URI (optional)
    /// </summary>
    public string? BannerUrl { get; set; }

    /// <summary>
    ///     Bio text for the bot in this guild (optional)
    /// </summary>
    public string? Bio { get; set; }
}