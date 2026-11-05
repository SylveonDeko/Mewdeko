namespace Mewdeko.Controllers.Common.CustomVoice;

/// <summary>
///     Request to hide a custom voice channel or reveal it again.
/// </summary>
public class SetCustomVoiceChannelHiddenRequest
{
    /// <summary>
    ///     True to hide the channel from members who are not in it, false to reveal it.
    /// </summary>
    public bool Hidden { get; set; }
}
