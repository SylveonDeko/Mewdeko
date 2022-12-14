namespace Mewdeko.Modules.Music.Common;

public class AdvancedTrackContext
{
    public AdvancedTrackContext(IUser queueUser, Platform queuedPlatform = Platform.Youtube)
    {
        QueueUser = queueUser;
        QueuedPlatform = queuedPlatform;
    }

    public IUser QueueUser { get; }
    public Platform QueuedPlatform { get; }
}