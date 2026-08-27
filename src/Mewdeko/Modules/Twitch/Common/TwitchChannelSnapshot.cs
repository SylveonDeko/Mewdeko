namespace Mewdeko.Modules.Twitch.Common;

/// <summary>
///     A point in time view of a guild's linked Twitch channel, cached and shared by consumers such as stat channels.
/// </summary>
public class TwitchChannelSnapshot
{
    /// <summary>
    ///     Gets or sets the broadcaster display name.
    /// </summary>
    public string DisplayName { get; set; } = "";

    /// <summary>
    ///     Gets or sets the broadcaster login name.
    /// </summary>
    public string Login { get; set; } = "";

    /// <summary>
    ///     Gets or sets whether the channel is currently live.
    /// </summary>
    public bool IsLive { get; set; }

    /// <summary>
    ///     Gets or sets the current viewer count, or zero when offline.
    /// </summary>
    public int Viewers { get; set; }

    /// <summary>
    ///     Gets or sets the category currently being streamed.
    /// </summary>
    public string Game { get; set; } = "";

    /// <summary>
    ///     Gets or sets the current stream title.
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    ///     Gets or sets when the current stream went live.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    ///     Gets or sets the total follower count, or null when the token lacks the scope.
    /// </summary>
    public int? Followers { get; set; }

    /// <summary>
    ///     Gets or sets the total subscriber count, or null when the token lacks the scope.
    /// </summary>
    public int? Subscribers { get; set; }
}