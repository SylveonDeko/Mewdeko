using System;
using Discord;
using Victoria;

namespace Mewdeko.Modules.Music.Extensions
{
    public class IndexedLavaTrack : LavaTrack

    {
        public int Index { get; set; }

        public IndexedLavaTrack(string hash, string id, string title, string author, string url, TimeSpan position, long duration, bool canSeek, bool isStream, string source)
            : base(hash, id, title, author, url, position, duration, canSeek, isStream, source )
        {
            
        }
    }
}