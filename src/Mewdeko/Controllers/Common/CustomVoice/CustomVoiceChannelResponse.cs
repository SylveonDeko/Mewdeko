namespace Mewdeko.Controllers.Common.CustomVoice;

/// <summary>
///     Response model for custom voice channel details
/// </summary>
public class CustomVoiceChannelResponse
{
    /// <summary>
    ///     The Discord channel ID of the custom voice channel
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     The Discord user ID of the channel owner
    /// </summary>
    public ulong OwnerId { get; set; }

    /// <summary>
    ///     When the custom voice channel was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    ///     When the custom voice channel was last active (had users)
    /// </summary>
    public DateTime LastActive { get; set; }

    /// <summary>
    ///     Whether the channel is locked (only allowed users can join)
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>
    ///     Whether the channel should be kept alive even when empty
    /// </summary>
    public bool KeepAlive { get; set; }

    /// <summary>
    ///     List of user IDs explicitly allowed to join the channel
    /// </summary>
    public List<ulong> AllowedUsers { get; set; } = new();

    /// <summary>
    ///     List of user IDs explicitly denied from joining the channel
    /// </summary>
    public List<ulong> DeniedUsers { get; set; } = new();

    /// <summary>
    ///     Whether the channel is hidden from members who are not in it
    /// </summary>
    public bool IsHidden { get; set; }

    /// <summary>
    ///     The hub voice channel this channel was created from, if known
    /// </summary>
    public ulong? HubVoiceChannelId { get; set; }

    /// <summary>
    ///     When the owner last left the channel, or null while they are in it
    /// </summary>
    public DateTime? OwnerLeftAt { get; set; }
}