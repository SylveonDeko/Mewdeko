namespace Mewdeko.Modules.Music.Common;

/// <summary>
/// Context for tracking advanced track information.
/// </summary>
/// <param name="queueUser">The user who queued the track</param>
/// <param name="queuedPlatform">The platform the track was queued from</param>
public class AdvancedTrackContext(IUser queueUser, Platform queuedPlatform = Platform.Youtube)
{
    /// <summary>
    /// The user who queued the track.
    /// </summary>
    public IUser QueueUser { get; } = queueUser;

    /// <summary>
    /// The platform the track was queued from.
    /// </summary>
    public Platform QueuedPlatform { get; } = queuedPlatform;
}