namespace Mewdeko.Modules.Music.Common;

public class AdvancedTrackContext(IUser queueUser, Platform queuedPlatform = Platform.Youtube)
{
    public IUser QueueUser { get; } = queueUser;
    public Platform QueuedPlatform { get; } = queuedPlatform;
}