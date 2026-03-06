namespace Mewdeko.Controllers.Common.Music;

/// <summary>
///     Request to update guild-wide TTS settings. All fields are optional; only provided fields are updated.
/// </summary>
public class TtsGuildSettingsRequest
{
    /// <summary>
    ///     TTS volume (0-100)
    /// </summary>
    public int? Volume { get; set; }

    /// <summary>
    ///     TTS playback speed (0.5-2.0)
    /// </summary>
    public float? Speed { get; set; }

    /// <summary>
    ///     Default TTS voice name. Empty string to reset to system default.
    /// </summary>
    public string? DefaultVoice { get; set; }

    /// <summary>
    ///     Whether to read reply context
    /// </summary>
    public bool? ReplyContext { get; set; }

    /// <summary>
    ///     Whether to narrate attachments
    /// </summary>
    public bool? AttachmentNarration { get; set; }

    /// <summary>
    ///     Whether to group consecutive messages from the same user
    /// </summary>
    public bool? ConsecutiveGrouping { get; set; }

    /// <summary>
    ///     Maximum TTS queue size (1-50)
    /// </summary>
    public int? MaxQueueSize { get; set; }

    /// <summary>
    ///     Role ID required to use TTS. Set to 0 to remove restriction.
    /// </summary>
    public ulong? RoleId { get; set; }
}