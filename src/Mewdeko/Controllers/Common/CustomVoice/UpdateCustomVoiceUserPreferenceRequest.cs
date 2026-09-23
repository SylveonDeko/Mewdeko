namespace Mewdeko.Controllers.Common.CustomVoice;

/// <summary>
///     Request model for replacing a member's saved custom voice channel preferences.
///     Each field is written as sent, so a null or missing value clears that preference.
/// </summary>
public class UpdateCustomVoiceUserPreferenceRequest
{
    /// <summary>
    ///     The preferred channel name format, or null/blank to clear it
    /// </summary>
    public string? DefaultName { get; set; }

    /// <summary>
    ///     The preferred user limit (0 means unlimited, must not be negative), or null to clear it
    /// </summary>
    public int? DefaultUserLimit { get; set; }

    /// <summary>
    ///     The preferred bitrate in kbps (must be positive), or null to clear it
    /// </summary>
    public int? DefaultBitrate { get; set; }
}
