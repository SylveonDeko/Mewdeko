namespace Mewdeko.Controllers.Common.Giveaways;

/// <summary>
///     A guild's giveaway defaults.
/// </summary>
public class GiveawaySettings
{
    /// <summary>
    ///     The emote members react with, or null to keep the default.
    /// </summary>
    public string? Emote { get; set; }

    /// <summary>
    ///     The role pinged when a giveaway starts, or 0 for none.
    /// </summary>
    public ulong PingRole { get; set; }

    /// <summary>
    ///     The image shown on giveaway messages, or empty for none.
    /// </summary>
    public string? Banner { get; set; }

    /// <summary>
    ///     The hex colour of giveaway messages, or empty for the default.
    /// </summary>
    public string? EmbedColor { get; set; }

    /// <summary>
    ///     Whether winners get a direct message.
    /// </summary>
    public bool DmOnWin { get; set; }

    /// <summary>
    ///     The direct message sent to winners, or empty for the default.
    /// </summary>
    public string? EndMessage { get; set; }
}
