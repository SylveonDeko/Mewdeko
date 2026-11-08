namespace Mewdeko.Controllers.Common.Guild;

/// <summary>
///     The general bot settings of a guild. On a request, a field left out is not changed.
/// </summary>
public class BotSettings
{
    /// <summary>
    ///     Command prefix
    /// </summary>
    public string? Prefix { get; set; }

    /// <summary>
    ///     Channel command usage is logged in, 0 for none
    /// </summary>
    public ulong? CommandLogChannel { get; set; }

    /// <summary>
    ///     Emoji shown for the server's currency
    /// </summary>
    public string? CurrencyEmoji { get; set; }

    /// <summary>
    ///     Channel warnings are logged in, 0 for none
    /// </summary>
    public ulong? WarnlogChannelId { get; set; }

    /// <summary>
    ///     Hours until a warning expires, 0 for never
    /// </summary>
    public int? WarnExpireHours { get; set; }

    /// <summary>
    ///     What happens to an expired warning: 0 clears it, 1 deletes it
    /// </summary>
    public int? WarnExpireAction { get; set; }

    /// <summary>
    ///     Language code, empty for the bot's default
    /// </summary>
    public string? Locale { get; set; }

    /// <summary>
    ///     Whether the snipe commands are on
    /// </summary>
    public bool? Snipeset { get; set; }

    /// <summary>
    ///     Whether links are previewed
    /// </summary>
    public bool? PreviewLinks { get; set; }

    /// <summary>
    ///     Name of the role given to muted members, empty for the default
    /// </summary>
    public string? MuteRoleName { get; set; }

    /// <summary>
    ///     Whether a mute removes the member's other roles
    /// </summary>
    public bool? RemoveRolesOnMute { get; set; }

    /// <summary>
    ///     Whether command errors explain what went wrong
    /// </summary>
    public bool? VerboseErrors { get; set; }
}
