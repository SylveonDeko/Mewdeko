using System;
using Discord;
using Victoria;

namespace Mewdeko.Modules.Music.Extensions
{
    public class IndexedLavaTrack : LavaTrack

    {
        public int Index { get; }
        public IUser QueueUser { get; }

        public IndexedLavaTrack(LavaTrack track, int index, IUser queueUser)
            : base(track)
        {
            Index = index;
            QueueUser = queueUser;
        }
    }
}