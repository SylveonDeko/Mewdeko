namespace Mewdeko.Controllers.Common.Music;

/// <summary>
///     Request to set or clear a TTS channel
/// </summary>
public class TtsChannelRequest
{
    /// <summary>
    ///     The channel ID to set as TTS channel, or null to disable
    /// </summary>
    public ulong? ChannelId { get; set; }
}