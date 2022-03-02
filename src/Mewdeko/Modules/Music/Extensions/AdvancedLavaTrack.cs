using Discord;
using Mewdeko.Database.Models;
using Victoria;

namespace Mewdeko.Modules.Music.Extensions;

public class AdvancedLavaTrack : LavaTrack

{

    public AdvancedLavaTrack(LavaTrack track, IUser queueUser, Platform queuedPlatform = Platform.Youtube)
        : base(track)
    {
        QueueUser = queueUser;
        QueuedPlatform = queuedPlatform;
    }
    
    public IUser QueueUser { get; }
    public Platform QueuedPlatform { get; }
}