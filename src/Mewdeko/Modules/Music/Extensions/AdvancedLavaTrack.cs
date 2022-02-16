using Discord;
using Mewdeko.Database.Models;
using Victoria;

namespace Mewdeko.Modules.Music.Extensions;

public class AdvancedLavaTrack : LavaTrack

{

    public AdvancedLavaTrack(LavaTrack track, int index, IUser queueUser, Platform queuedPlatform = Platform.Youtube)
        : base(track)
    {
        Index = index;
        QueueUser = queueUser;
        QueuedPlatform = queuedPlatform;
    }

    public int Index { get; set; }
    public IUser QueueUser { get; }
    public Platform QueuedPlatform { get; }
}