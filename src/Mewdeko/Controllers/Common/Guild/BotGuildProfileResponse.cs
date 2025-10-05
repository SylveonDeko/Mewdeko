namespace Mewdeko.Controllers.Common.Guild;

/// <summary>
///     Response model for the bot's guild profile
/// </summary>
public class BotGuildProfileResponse
{
    /// <summary>
    ///     The bot's guild-specific avatar hash
    /// </summary>
    public string? Avatar { get; set; }

    /// <summary>
    ///     The bot's guild-specific avatar URL
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    ///     The bot's guild-specific banner hash
    /// </summary>
    public string? Banner { get; set; }

    /// <summary>
    ///     The bot's guild-specific banner URL
    /// </summary>
    public string? BannerUrl { get; set; }

    /// <summary>
    ///     The bot's guild-specific bio
    /// </summary>
    public string? Bio { get; set; }

    /// <summary>
    ///     The bot's nickname in the guild
    /// </summary>
    public string? Nickname { get; set; }
}