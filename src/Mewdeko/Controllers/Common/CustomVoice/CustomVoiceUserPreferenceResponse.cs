namespace Mewdeko.Controllers.Common.CustomVoice;

/// <summary>
///     Response model for a member's saved custom voice channel preferences
/// </summary>
public class CustomVoiceUserPreferenceResponse
{
    /// <summary>
    ///     The Discord user ID the preferences belong to
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     The Discord guild ID the preferences apply to
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     The member's preferred channel name format, or null to use the guild default
    /// </summary>
    public string? DefaultName { get; set; }

    /// <summary>
    ///     The member's preferred user limit (0 means unlimited), or null to use the guild default
    /// </summary>
    public int? DefaultUserLimit { get; set; }

    /// <summary>
    ///     The member's preferred bitrate in kbps, or null to use the guild default
    /// </summary>
    public int? DefaultBitrate { get; set; }
}
