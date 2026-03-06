namespace Mewdeko.Controllers.Common.Music;

/// <summary>
///     Request to create or update TTS settings for a voice channel
/// </summary>
public class TtsVcSettingRequest
{
    /// <summary>
    ///     The voice channel ID
    /// </summary>
    public ulong VoiceChannelId { get; set; }

    /// <summary>
    ///     Whether TTS is enabled for this voice channel
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     Optional linked text channel ID
    /// </summary>
    public ulong? LinkedTextChannelId { get; set; }

    /// <summary>
    ///     Whether to announce join/leave events
    /// </summary>
    public bool AnnounceJoinLeave { get; set; }

    /// <summary>
    ///     Custom join announcement format. Use {user} placeholder.
    /// </summary>
    public string? JoinFormat { get; set; }

    /// <summary>
    ///     Custom leave announcement format. Use {user} placeholder.
    /// </summary>
    public string? LeaveFormat { get; set; }
}