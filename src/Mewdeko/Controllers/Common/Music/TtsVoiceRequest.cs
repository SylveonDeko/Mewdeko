namespace Mewdeko.Controllers.Common.Music;

/// <summary>
///     Request to set a TTS voice
/// </summary>
public class TtsVoiceRequest
{
    /// <summary>
    ///     The voice name to use, or null to reset to default
    /// </summary>
    public string? Voice { get; set; }
}