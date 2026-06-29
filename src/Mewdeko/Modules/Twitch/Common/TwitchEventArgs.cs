namespace Mewdeko.Modules.Twitch.Common;

/// <summary>
///     Fired when a monitored Twitch channel goes live. Includes stream metadata sourced from the Helix API.
/// </summary>
public class TwitchStreamOnlineArgs
{
    /// <summary>Gets the broadcaster's Twitch user ID.</summary>
    public string BroadcasterUserId { get; init; } = "";

    /// <summary>Gets the broadcaster's lowercased Twitch login name.</summary>
    public string BroadcasterUserLogin { get; init; } = "";

    /// <summary>Gets the broadcaster's display name.</summary>
    public string BroadcasterUserName { get; init; } = "";

    /// <summary>Gets the stream ID assigned by Twitch for this broadcast session.</summary>
    public string StreamId { get; init; } = "";

    /// <summary>Gets the UTC time at which the stream started.</summary>
    public DateTime StartedAt { get; init; }

    /// <summary>Gets the Discord guild ID that has this Twitch channel configured.</summary>
    public ulong GuildId { get; init; }
}

/// <summary>
///     Fired when a monitored Twitch channel goes offline.
/// </summary>
public class TwitchStreamOfflineArgs
{
    /// <summary>Gets the broadcaster's Twitch user ID.</summary>
    public string BroadcasterUserId { get; init; } = "";

    /// <summary>Gets the broadcaster's lowercased Twitch login name.</summary>
    public string BroadcasterUserLogin { get; init; } = "";

    /// <summary>Gets the broadcaster's display name.</summary>
    public string BroadcasterUserName { get; init; } = "";

    /// <summary>Gets the Discord guild ID that has this Twitch channel configured.</summary>
    public ulong GuildId { get; init; }
}

/// <summary>
///     Fired when a new subscription (or resub) occurs in a monitored Twitch channel.
/// </summary>
public class TwitchNewSubArgs
{
    /// <summary>Gets the Twitch channel name where the sub occurred.</summary>
    public string Channel { get; init; } = "";

    /// <summary>Gets the subscriber's Twitch login name.</summary>
    public string Username { get; init; } = "";

    /// <summary>Gets the subscriber's display name.</summary>
    public string DisplayName { get; init; } = "";

    /// <summary>Gets the subscription plan tier (Prime, 1000, 2000, 3000).</summary>
    public string SubPlan { get; init; } = "";

    /// <summary>Gets whether this subscription was gifted by another user.</summary>
    public bool IsGift { get; init; }

    /// <summary>Gets the Discord guild ID that has this Twitch channel configured.</summary>
    public ulong GuildId { get; init; }
}

/// <summary>
///     Fired when a viewer cheers (sends bits) in a monitored Twitch channel.
/// </summary>
public class TwitchCheerArgs
{
    /// <summary>Gets the Twitch channel name where the cheer occurred.</summary>
    public string Channel { get; init; } = "";

    /// <summary>Gets the cheering viewer's Twitch login name.</summary>
    public string Username { get; init; } = "";

    /// <summary>Gets the number of bits cheered.</summary>
    public int Bits { get; init; }

    /// <summary>Gets the message accompanying the cheer, if any.</summary>
    public string Message { get; init; } = "";

    /// <summary>Gets the Discord guild ID that has this Twitch channel configured.</summary>
    public ulong GuildId { get; init; }
}

/// <summary>
///     Fired when another channel raids a monitored Twitch channel.
/// </summary>
public class TwitchRaidArgs
{
    /// <summary>Gets the name of the Twitch channel being raided.</summary>
    public string Channel { get; init; } = "";

    /// <summary>Gets the display name of the raiding broadcaster.</summary>
    public string RaiderDisplayName { get; init; } = "";

    /// <summary>Gets the number of viewers brought by the raid.</summary>
    public int ViewerCount { get; init; }

    /// <summary>Gets the Discord guild ID that has this Twitch channel configured.</summary>
    public ulong GuildId { get; init; }
}