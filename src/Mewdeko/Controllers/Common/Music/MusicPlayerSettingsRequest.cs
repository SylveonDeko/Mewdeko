namespace Mewdeko.Controllers.Common.Music;

/// <summary>
///     Request to update guild-wide music player settings. All fields are optional; only provided fields are updated.
/// </summary>
public class MusicPlayerSettingsRequest
{
    /// <summary>
    ///     Player volume (0-100)
    /// </summary>
    public int? Volume { get; set; }

    /// <summary>
    ///     Repeat mode (0=Off, 1=Track, 2=Queue)
    /// </summary>
    public int? PlayerRepeat { get; set; }

    /// <summary>
    ///     Number of tracks to autoplay once the queue runs dry. 0 disables autoplay.
    /// </summary>
    public int? AutoPlay { get; set; }

    /// <summary>
    ///     Idle minutes before the player disconnects. 0 disables auto-disconnect.
    /// </summary>
    public int? AutoDisconnect { get; set; }

    /// <summary>
    ///     Channel that player messages are posted to. Set to 0 to clear.
    /// </summary>
    public ulong? MusicChannelId { get; set; }

    /// <summary>
    ///     Role required to use DJ-only commands. Set to 0 to clear.
    /// </summary>
    public ulong? DjRoleId { get; set; }

    /// <summary>
    ///     Whether skipping requires a vote
    /// </summary>
    public bool? VoteSkipEnabled { get; set; }

    /// <summary>
    ///     Percentage of listeners required to pass a skip vote
    /// </summary>
    public int? VoteSkipThreshold { get; set; }
}